using System.Runtime.CompilerServices;
using GoldsrcFramework.Engine.Native;
using GoldsrcFramework.LinearMath;
using NativeInterop;
using GoldsrcVector3 = GoldsrcFramework.LinearMath.Vector3;

namespace GoldsrcFramework.Rendering;

/// <summary>
/// Produces the animated studio pose of a client entity on demand, outside the render pass, so a
/// kinematic physics skeleton can be driven by what the model is actually animating.
/// </summary>
/// <remarks>
/// <para>
/// This is the "setup bones" half of <see cref="StudioModelRenderer"/> written as a standalone,
/// static routine: the same animation math over the same header the engine has loaded, but with no
/// renderer instance, no renderer state, and no engine scratch buffer - the bone matrices go into a
/// buffer the caller owns. Frame the caller passes any entity of the current frame, and the result
/// is what the renderer would have put in the engine's bone array for it.
/// </para>
/// <para>
/// <b>World space, always.</b> The renderer has to emit view-space, screen-scaled bones when the
/// engine draws in software (<c>IsHardware()</c> is false), which is meaningless for physics; the
/// bones here are always composed with the entity's world transform, the same choice gsphysics'
/// pre-renderer makes (<c>PreStudioModelRenderer.cpp:970</c>).
/// </para>
/// <para>
/// <b>Two Euler conventions meet here.</b> An entity's world angles go through
/// <c>StudioMath.AngleMatrix</c>, while a studio bone's angles go through
/// <c>StudioMath.AngleQuaternion</c>. The two are <i>not</i> interchangeable: they assign the three
/// components to different axes. The entity transform therefore uses the very call the renderer
/// builds <c>m_protationmatrix</c> with, and the bones the very call the renderer builds its bone
/// matrices with.
/// </para>
/// <para>
/// <b>Deliberate omissions</b> relative to the renderer's path, in each case because the renderer
/// needs it and a pose does not:
/// light matrices (a lighting input only), client-side render effects
/// (<c>StudioFxTransform</c>, which injects random scale/offset into the bones), attachment points,
/// and the render-time bone cache (<c>StudioSaveBones</c>, which belongs to the drawing pass).
/// Entities whose bones come from another entity (<c>MoveType.FOLLOW</c>) are refused rather than
/// merged, because the merge reads that cache; a caller then has no pose and leaves the skeleton be.
/// </para>
/// <para>
/// <b>Cost.</b> No allocation: every scratch buffer is a <c>stackalloc</c>, and the only engine
/// allocation on the path - the sequence-group cache of <c>StudioGetAnim</c> - is reached solely by
/// models that keep animation data in a separate file (<c>seqgroup != 0</c>), which the stock
/// Half-Life models do not. The one engine call is <c>GetTimes</c>, for the client clock the
/// animation state is expressed in.
/// </para>
/// </remarks>
internal static unsafe class PreStudioModelRenderer
{
    /// <summary>
    /// Writes the pose of <paramref name="entity"/> into <paramref name="bones"/>: one world-space
    /// matrix per studio bone, in studio bone order.
    /// </summary>
    /// <param name="bones">
    /// Destination, indexed by studio bone. Must hold at least <c>header-&gt;numbones</c> entries;
    /// entries past the model's bone count are left untouched.
    /// </param>
    /// <returns>The number of matrices written, or 0 when the entity cannot supply a pose.</returns>
    public static int SetupBones(cl_entity_t* entity, Span<Matrix3x4> bones)
    {
        if (entity == null || bones.IsEmpty)
            return 0;

        var model = entity->model;
        if (model == null || model->type != modtype_t.mod_studio)
            return 0;

        var studio = EngineApi.PStudio;
        if (studio == null || studio->Mod_Extradata == null || studio->GetTimes == null)
            return 0;

        var header = (studiohdr_t*)studio->Mod_Extradata(model);
        if (header == null || header->numbones <= 0 || header->numbones > bones.Length)
            return 0;
        if (header->numbones > StudioConstants.MAXSTUDIOBONES || header->numseq <= 0)
            return 0;

        // An entity that follows another one borrows that entity's bones, which the drawing pass
        // leaves in the renderer's cache; there is nothing of its own to sample.
        if (entity->curstate.movetype == MoveType.FOLLOW)
            return 0;

        // Same source as the renderer: the pose is expressed in the client clock, not in the host's.
        int frameCount;
        double clTime, clOldTime;
        studio->GetTimes(&frameCount, &clTime, &clOldTime);

        if (entity->curstate.sequence < 0 || entity->curstate.sequence >= header->numseq)
            entity->curstate.sequence = 0;

        var sequence = header->GetSequences() + entity->curstate.sequence;
        var animation = GetAnim(header, model, sequence);
        if (animation == null)
            return 0;

        var f = EstimateFrame(entity, sequence, clTime);
        SetUpEntityTransform(entity, header, clTime, out var entityTransform);

        var boneCount = header->numbones;

        Span<float> pos = stackalloc float[StudioConstants.MAXSTUDIOBONES * 3];
        Span<float> q = stackalloc float[StudioConstants.MAXSTUDIOBONES * 4];
        Span<float> posAlt = stackalloc float[StudioConstants.MAXSTUDIOBONES * 3];
        Span<float> qAlt = stackalloc float[StudioConstants.MAXSTUDIOBONES * 4];
        Span<float> posThird = stackalloc float[StudioConstants.MAXSTUDIOBONES * 3];
        Span<float> qThird = stackalloc float[StudioConstants.MAXSTUDIOBONES * 4];
        Span<float> posFourth = stackalloc float[StudioConstants.MAXSTUDIOBONES * 3];
        Span<float> qFourth = stackalloc float[StudioConstants.MAXSTUDIOBONES * 4];

        fixed (float* pPos = pos, pQ = q, pPosAlt = posAlt, pQAlt = qAlt,
                      pPosThird = posThird, pQThird = qThird, pPosFourth = posFourth, pQFourth = qFourth)
        fixed (Matrix3x4* pBones = bones)
        {
            CalcRotations(header, entity, pPos, pQ, sequence, animation, f, clTime);

            // The sequence's own blends: up to four animation strips slerped together.
            if (sequence->numblends > 1)
            {
                animation += boneCount;
                CalcRotations(header, entity, pPosAlt, pQAlt, sequence, animation, f, clTime);

                var dadt = EstimateInterpolant(entity, clTime);
                var s = (entity->curstate.blending[0] * dadt + entity->latched.prevblending[0] * (1.0f - dadt)) / 255.0f;
                SlerpBones(header, pQ, pPos, pQAlt, pPosAlt, s);

                if (sequence->numblends == 4)
                {
                    animation += boneCount;
                    CalcRotations(header, entity, pPosThird, pQThird, sequence, animation, f, clTime);

                    animation += boneCount;
                    CalcRotations(header, entity, pPosFourth, pQFourth, sequence, animation, f, clTime);

                    s = (entity->curstate.blending[0] * dadt + entity->latched.prevblending[0] * (1.0f - dadt)) / 255.0f;
                    SlerpBones(header, pQThird, pPosThird, pQFourth, pPosFourth, s);

                    s = (entity->curstate.blending[1] * dadt + entity->latched.prevblending[1] * (1.0f - dadt)) / 255.0f;
                    SlerpBones(header, pQ, pPos, pQThird, pPosThird, s);
                }
            }

            // Blend out of the previous sequence while its transition window is still open.
            if (entity->latched.sequencetime != 0 &&
                entity->latched.sequencetime + 0.2 > clTime &&
                entity->latched.prevsequence < header->numseq)
            {
                Span<float> posPrev = stackalloc float[StudioConstants.MAXSTUDIOBONES * 3];
                Span<float> qPrev = stackalloc float[StudioConstants.MAXSTUDIOBONES * 4];

                fixed (float* pPosPrev = posPrev, pQPrev = qPrev)
                {
                    if (entity->latched.prevsequence >= header->numseq)
                        entity->latched.prevsequence = 0;

                    var previous = header->GetSequences() + entity->latched.prevsequence;
                    var previousAnimation = GetAnim(header, model, previous);
                    if (previousAnimation == null)
                        return 0;

                    CalcRotations(header, entity, pPosPrev, pQPrev, previous, previousAnimation,
                        entity->latched.prevframe, clTime);

                    if (previous->numblends > 1)
                    {
                        previousAnimation += boneCount;
                        CalcRotations(header, entity, pPosAlt, pQAlt, previous, previousAnimation,
                            entity->latched.prevframe, clTime);

                        var s = entity->latched.prevseqblending[0] / 255.0f;
                        SlerpBones(header, pQPrev, pPosPrev, pQAlt, pPosAlt, s);

                        if (previous->numblends == 4)
                        {
                            previousAnimation += boneCount;
                            CalcRotations(header, entity, pPosThird, pQThird, previous, previousAnimation,
                                entity->latched.prevframe, clTime);

                            previousAnimation += boneCount;
                            CalcRotations(header, entity, pPosFourth, pQFourth, previous, previousAnimation,
                                entity->latched.prevframe, clTime);

                            s = entity->latched.prevseqblending[0] / 255.0f;
                            SlerpBones(header, pQThird, pPosThird, pQFourth, pPosFourth, s);

                            s = entity->latched.prevseqblending[1] / 255.0f;
                            SlerpBones(header, pQPrev, pPosPrev, pQThird, pPosThird, s);
                        }
                    }

                    var blend = 1.0f - (float)((clTime - entity->latched.sequencetime) / 0.2);
                    SlerpBones(header, pQ, pPos, pQPrev, pPosPrev, blend);
                }
            }
            else
            {
                entity->latched.prevframe = f;
            }

            // Local bone matrices, then down the parent chain into the entity's frame.
            var boneTable = header->GetBones();
            Span<float> boneMatrix = stackalloc float[3 * 4];

            fixed (float* pBoneMatrix = boneMatrix)
            {
                for (var i = 0; i < boneCount; i++)
                {
                    StudioMath.QuaternionMatrix(pQ + (i * 4), pBoneMatrix);
                    pBoneMatrix[0 * 4 + 3] = pPos[i * 3 + 0];
                    pBoneMatrix[1 * 4 + 3] = pPos[i * 3 + 1];
                    pBoneMatrix[2 * 4 + 3] = pPos[i * 3 + 2];

                    var local = (Matrix3x4*)pBoneMatrix;
                    var parent = boneTable[i].parent;

                    if (parent == -1)
                        StudioMath.ConcatTransforms(ref entityTransform, ref *local, out pBones[i]);
                    else
                        StudioMath.ConcatTransforms(ref pBones[parent], ref *local, out pBones[i]);
                }
            }
        }

        return boneCount;
    }

    // ── Animation sampling ────────────────────────────────────────────────────────

    /// <summary>
    /// Bone rotations and positions for the frame <paramref name="f"/>, into <paramref name="pos"/>
    /// (3 floats per bone) and <paramref name="q"/> (4 floats per bone).
    /// Original: <c>CStudioModelRenderer::StudioCalcRotations</c>.
    /// </summary>
    private static void CalcRotations(
        studiohdr_t* header, cl_entity_t* entity, float* pos, float* q,
        mstudioseqdesc_t* sequence, mstudioanim_t* animation, float f, double clTime)
    {
        if (f > sequence->numframes - 1)
            f = 0;
        else if (f < -0.01f)
            f = -0.01f;

        var frame = (int)f;
        var dadt = EstimateInterpolant(entity, clTime);
        var s = f - frame;

        var bone = header->GetBones();
        Span<float> adj = stackalloc float[StudioConstants.MAXSTUDIOCONTROLLERS];

        fixed (float* pAdj = adj)
        {
            CalcBoneAdjustment(header, dadt, pAdj,
                &entity->curstate.controller.Element0, &entity->latched.prevcontroller.Element0,
                entity->mouth.mouthopen);

            for (var i = 0; i < header->numbones; i++, bone++, animation++)
            {
                CalcBoneQuaternion(frame, s, bone, animation, pAdj, q + (i * 4));
                CalcBonePosition(frame, s, bone, animation, pAdj, pos + (i * 3));
            }
        }

        // Root motion is driven by the entity, not by the animation.
        if ((sequence->motiontype & (int)StudioMotionFlags.STUDIO_X) != 0)
            pos[sequence->motionbone * 3 + 0] = 0.0f;
        if ((sequence->motiontype & (int)StudioMotionFlags.STUDIO_Y) != 0)
            pos[sequence->motionbone * 3 + 1] = 0.0f;
        if ((sequence->motiontype & (int)StudioMotionFlags.STUDIO_Z) != 0)
            pos[sequence->motionbone * 3 + 2] = 0.0f;
    }

    /// <summary>
    /// Programmatic bone controllers (the values an entity sends per frame) as per-bone offsets.
    /// Original: <c>CStudioModelRenderer::StudioCalcBoneAdj</c>.
    /// </summary>
    private static void CalcBoneAdjustment(
        studiohdr_t* header, float dadt, float* adj, byte* controller, byte* previousController, byte mouthOpen)
    {
        var boneController = header->GetBoneControllers();

        for (var j = 0; j < header->numbonecontrollers; j++)
        {
            float value;
            var i = boneController[j].index;

            if (i <= 3)
            {
                if ((boneController[j].type & (int)StudioMotionFlags.STUDIO_RLOOP) != 0)
                {
                    // A 360 degree controller wraps, so the two endpoints are blended the long way
                    // round instead of across.
                    if (Math.Abs(controller[i] - previousController[i]) > 128)
                    {
                        var a = (controller[j] + 128) % 256;
                        var b = (previousController[j] + 128) % 256;
                        value = ((a * dadt) + (b * (1 - dadt)) - 128) * (360.0f / 256.0f) + boneController[j].start;
                    }
                    else
                    {
                        value = (controller[i] * dadt + previousController[i] * (1.0f - dadt)) * (360.0f / 256.0f) + boneController[j].start;
                    }
                }
                else
                {
                    value = (controller[i] * dadt + previousController[i] * (1.0f - dadt)) / 255.0f;
                    if (value < 0)
                        value = 0;
                    if (value > 1.0f)
                        value = 1.0f;
                    value = (1.0f - value) * boneController[j].start + value * boneController[j].end;
                }
            }
            else
            {
                // Controller 4 (and up) is the mouth, driven by the client's mouth state directly.
                value = mouthOpen / 64.0f;
                if (value > 1.0f)
                    value = 1.0f;
                value = (1.0f - value) * boneController[j].start + value * boneController[j].end;
            }

            switch (boneController[j].type & (int)StudioMotionFlags.STUDIO_TYPES)
            {
                case (int)StudioMotionFlags.STUDIO_XR:
                case (int)StudioMotionFlags.STUDIO_YR:
                case (int)StudioMotionFlags.STUDIO_ZR:
                    adj[j] = value * (MathF.PI / 180.0f);
                    break;
                case (int)StudioMotionFlags.STUDIO_X:
                case (int)StudioMotionFlags.STUDIO_Y:
                case (int)StudioMotionFlags.STUDIO_Z:
                    adj[j] = value;
                    break;
            }
        }
    }

    /// <summary>
    /// One bone's rotation for the frame, interpolating between the two animation values that
    /// straddle it. Original: <c>CStudioModelRenderer::StudioCalcBoneQuaterion</c>.
    /// </summary>
    private static void CalcBoneQuaternion(
        int frame, float s, mstudiobone_t* bone, mstudioanim_t* animation, float* adj, float* q)
    {
        Span<float> angle1 = stackalloc float[3];
        Span<float> angle2 = stackalloc float[3];
        Span<float> q1 = stackalloc float[4];
        Span<float> q2 = stackalloc float[4];

        for (var j = 0; j < 3; j++)
        {
            if (animation->offset[j + 3] == 0)
            {
                angle2[j] = angle1[j] = bone->value[j + 3]; // no animation for this axis
            }
            else
            {
                var value = (mstudioanimvalue_t*)((byte*)animation + animation->offset[j + 3]);
                var k = frame;

                if (value->num.total < value->num.valid)
                    k = 0;

                while (value->num.total <= k)
                {
                    k -= value->num.total;
                    value += value->num.valid + 1;

                    if (value->num.total < value->num.valid)
                        k = 0;
                }

                if (value->num.valid > k)
                {
                    angle1[j] = value[k + 1].value;

                    if (value->num.valid > k + 1)
                        angle2[j] = value[k + 2].value;
                    else if (value->num.total > k + 1)
                        angle2[j] = angle1[j];
                    else
                        angle2[j] = value[value->num.valid + 2].value;
                }
                else
                {
                    angle1[j] = value[value->num.valid].value;

                    if (value->num.total > k + 1)
                        angle2[j] = angle1[j];
                    else
                        angle2[j] = value[value->num.valid + 2].value;
                }

                angle1[j] = bone->value[j + 3] + angle1[j] * bone->scale[j + 3];
                angle2[j] = bone->value[j + 3] + angle2[j] * bone->scale[j + 3];
            }

            if (bone->bonecontroller[j + 3] != -1)
            {
                angle1[j] += adj[bone->bonecontroller[j + 3]];
                angle2[j] += adj[bone->bonecontroller[j + 3]];
            }
        }

        fixed (float* pAngle1 = angle1, pAngle2 = angle2, pQ1 = q1, pQ2 = q2)
        {
            if (!VectorEquals(pAngle1, pAngle2))
            {
                StudioMath.AngleQuaternion(pAngle1, pQ1);
                StudioMath.AngleQuaternion(pAngle2, pQ2);
                StudioMath.QuaternionSlerp(pQ1, pQ2, s, q);
            }
            else
            {
                StudioMath.AngleQuaternion(pAngle1, q);
            }
        }
    }

    /// <summary>
    /// One bone's position for the frame. Original:
    /// <c>CStudioModelRenderer::StudioCalcBonePosition</c>.
    /// </summary>
    private static void CalcBonePosition(
        int frame, float s, mstudiobone_t* bone, mstudioanim_t* animation, float* adj, float* pos)
    {
        for (var j = 0; j < 3; j++)
        {
            pos[j] = bone->value[j]; // rest position unless the animation overrides it

            if (animation->offset[j] != 0)
            {
                var value = (mstudioanimvalue_t*)((byte*)animation + animation->offset[j]);
                var k = frame;

                if (value->num.total < value->num.valid)
                    k = 0;

                while (value->num.total <= k)
                {
                    k -= value->num.total;
                    value += value->num.valid + 1;

                    if (value->num.total < value->num.valid)
                        k = 0;
                }

                if (value->num.valid > k)
                {
                    if (value->num.valid > k + 1)
                        pos[j] += (value[k + 1].value * (1.0f - s) + s * value[k + 2].value) * bone->scale[j];
                    else
                        pos[j] += value[k + 1].value * bone->scale[j];
                }
                else
                {
                    if (value->num.total <= k + 1)
                        pos[j] += (value[value->num.valid].value * (1.0f - s) + s * value[value->num.valid + 2].value) * bone->scale[j];
                    else
                        pos[j] += value[value->num.valid].value * bone->scale[j];
                }
            }

            if (bone->bonecontroller[j] != -1 && adj != null)
                pos[j] += adj[bone->bonecontroller[j]];
        }
    }

    /// <summary>
    /// Interpolates a whole skeleton between two sampled strips, in place on the first.
    /// Original: <c>CStudioModelRenderer::StudioSlerpBones</c>.
    /// </summary>
    private static void SlerpBones(studiohdr_t* header, float* q1, float* pos1, float* q2, float* pos2, float s)
    {
        if (s < 0)
            s = 0;
        else if (s > 1.0f)
            s = 1.0f;

        var s1 = 1.0f - s;
        Span<float> q3 = stackalloc float[4];

        for (var i = 0; i < header->numbones; i++)
        {
            var q1i = q1 + (i * 4);
            var q2i = q2 + (i * 4);
            var pos1i = pos1 + (i * 3);
            var pos2i = pos2 + (i * 3);

            fixed (float* pQ3 = q3)
            {
                StudioMath.QuaternionSlerp(q1i, q2i, s, pQ3);
                q1i[0] = pQ3[0];
                q1i[1] = pQ3[1];
                q1i[2] = pQ3[2];
                q1i[3] = pQ3[3];
            }

            pos1i[0] = pos1i[0] * s1 + pos2i[0] * s;
            pos1i[1] = pos1i[1] * s1 + pos2i[1] * s;
            pos1i[2] = pos1i[2] * s1 + pos2i[2] * s;
        }
    }

    // ── Time ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// How far the current animation frame is between two client frames, used to blend controllers.
    /// Original: <c>CStudioModelRenderer::StudioEstimateInterpolant</c>.
    /// </summary>
    private static float EstimateInterpolant(cl_entity_t* entity, double clTime)
    {
        if (entity->curstate.animtime >= entity->latched.prevanimtime + 0.01)
        {
            var dadt = (float)((clTime - entity->curstate.animtime) / 0.1);
            return dadt > 2.0f ? 2.0f : dadt;
        }

        return 1.0f;
    }

    /// <summary>
    /// The animation frame to sample, extrapolated from the client clock since the sequence last
    /// started and wrapped or clamped according to the sequence's looping flag.
    /// Original: <c>CStudioModelRenderer::StudioEstimateFrame</c>.
    /// </summary>
    private static float EstimateFrame(cl_entity_t* entity, mstudioseqdesc_t* sequence, double clTime)
    {
        double dfdt;

        if (clTime < entity->curstate.animtime)
            dfdt = 0;
        else
            dfdt = (clTime - entity->curstate.animtime) * entity->curstate.framerate * sequence->fps;

        double f = sequence->numframes <= 1
            ? 0
            : (entity->curstate.frame * (sequence->numframes - 1)) / 256.0;

        f += dfdt;

        if ((sequence->flags & (int)StudioSequenceFlags.STUDIO_LOOPING) != 0)
        {
            if (sequence->numframes > 1)
                f -= (int)(f / (sequence->numframes - 1)) * (sequence->numframes - 1);
            if (f < 0)
                f += sequence->numframes - 1;
        }
        else
        {
            if (f >= sequence->numframes - 1.001)
                f = sequence->numframes - 1.001;
            if (f < 0.0)
                f = 0.0;
        }

        return (float)f;
    }

    /// <summary>
    /// Where a sequence's animation strip lives. Most models keep it inside the model file (a plain
    /// offset from the header); the rest keep it in a separate sequence-group file the engine caches.
    /// Original: <c>CStudioModelRenderer::StudioGetAnim</c>.
    /// </summary>
    private static mstudioanim_t* GetAnim(studiohdr_t* header, model_t* model, mstudioseqdesc_t* sequence)
    {
        if (sequence->seqgroup == 0)
            return (mstudioanim_t*)((byte*)header + sequence->animindex);

        var studio = EngineApi.PStudio;
        if (studio == null || studio->Cache_Check == null || studio->LoadCacheFile == null)
            return null;

        var sequenceGroup = (mstudioseqgroup_t*)((byte*)header + header->seqgroupindex) + sequence->seqgroup;
        var groups = (cache_user_t*)model->submodels;

        if (groups == null)
            return null; // the engine has not cached this model's sequence groups yet

        if (studio->Cache_Check(&groups[sequence->seqgroup]) == null)
        {
            NChar* name = (NChar*)Unsafe.AsPointer(ref sequenceGroup->name[0]);
            studio->LoadCacheFile(NCharPtr.From(name), &groups[sequence->seqgroup]);
        }

        var data = groups[sequence->seqgroup].data;
        return data == null ? null : (mstudioanim_t*)((byte*)data + sequence->animindex);
    }

    // ── Entity frame ──────────────────────────────────────────────────────────────

    /// <summary>
    /// The entity's world transform for this frame, which is what the bones hang off.
    /// Original: <c>CStudioModelRenderer::StudioSetUpTransform</c>, world-space path only.
    /// </summary>
    /// <remarks>
    /// A model that moves in steps (<c>MoveType.STEP</c>, i.e. most monsters) has its origin and
    /// angles interpolated between the previous and the current client frame: the engine reaches the
    /// new value 0.1 s after it was sent, so the pose has to catch up over that window instead of
    /// snapping. This is why the routine needs the client clock at all.
    /// </remarks>
    private static void SetUpEntityTransform(
        cl_entity_t* entity, studiohdr_t* header, double clTime, out Matrix3x4 transform)
    {
        var origin = entity->origin;
        var angles = new GoldsrcVector3(
            entity->curstate.angles.X,
            entity->curstate.angles.Y,
            entity->curstate.angles.Z);

        if (entity->curstate.movetype == MoveType.STEP)
        {
            float f = 0;

            if (clTime < entity->curstate.animtime + 1.0f &&
                entity->curstate.animtime != entity->latched.prevanimtime)
            {
                f = (float)((clTime - entity->curstate.animtime) /
                    (entity->curstate.animtime - entity->latched.prevanimtime));
            }

            // The current value is only reached 0.1 s after it was set, so the blend runs one step
            // behind: the engine expresses the same thing by subtracting one from the fraction.
            f -= 1.0f;

            var seqdesc = header->GetSequences() + entity->curstate.sequence;

            if ((seqdesc->motiontype & (int)StudioMotionFlags.STUDIO_LX) != 0 ||
                (entity->curstate.eflags & EntityStateFlags.SLERP) != 0)
            {
                origin.X += (entity->origin.X - entity->latched.prevorigin.X) * f;
                origin.Y += (entity->origin.Y - entity->latched.prevorigin.Y) * f;
                origin.Z += (entity->origin.Z - entity->latched.prevorigin.Z) * f;
            }

            for (var i = 0; i < 3; i++)
            {
                var current = i == 0 ? entity->angles.X : i == 1 ? entity->angles.Y : entity->angles.Z;
                var previous = i == 0 ? entity->latched.prevangles.X : i == 1 ? entity->latched.prevangles.Y : entity->latched.prevangles.Z;

                var d = current - previous;
                if (d > 180)
                    d -= 360;
                else if (d < -180)
                    d += 360;

                if (i == 0)
                    angles.X += d * f;
                else if (i == 1)
                    angles.Y += d * f;
                else
                    angles.Z += d * f;
            }
        }
        else if (entity->curstate.movetype != MoveType.NONE)
        {
            angles = entity->angles;
        }

        // The model faces the opposite way round from the entity's pitch.
        angles.X = -angles.X;

        // World Euler angles (pitch, yaw, roll) must go through AngleMatrix, which is what the
        // renderer builds m_protationmatrix with. AngleQuaternion is a *different* convention: it is
        // the one studio bone angles use (see CalcBoneQuaternion), and it maps the three components
        // onto the X, Y and Z axes where world Euler maps them onto Y, Z and X. Feeding an entity's
        // angles to it turns the whole skeleton about the wrong axes - most visibly, yaw stops being
        // a rotation about the world up axis.
        var placement = default(Matrix3x4);
        StudioMath.AngleMatrix(ref angles, ref placement);
        placement.Origin = origin;
        transform = placement;
    }

    private static bool VectorEquals(float* a, float* b)
        => a[0] == b[0] && a[1] == b[1] && a[2] == b[2];
}

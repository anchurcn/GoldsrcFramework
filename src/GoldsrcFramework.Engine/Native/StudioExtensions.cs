using System.Runtime.CompilerServices;

namespace GoldsrcFramework.Engine.Native;

/// <summary>
/// Extension methods for studio structures to provide convenient pointer-based access
/// </summary>
public static unsafe class StudioExtensions
{
    /// <summary>
    /// Get bone controller array from studio header
    /// Original: pbonecontroller = (mstudiobonecontroller_t*)((byte*)m_pStudioHeader + m_pStudioHeader->bonecontrollerindex);
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static mstudiobonecontroller_t* GetBoneControllers(this ref studiohdr_t header)
    {
        fixed (studiohdr_t* pHeader = &header)
        {
            return (mstudiobonecontroller_t*)((byte*)pHeader + pHeader->bonecontrollerindex);
        }
    }

    /// <summary>
    /// Get bone array from studio header
    /// Original: pbone = (mstudiobone_t*)((byte*)m_pStudioHeader + m_pStudioHeader->boneindex);
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static mstudiobone_t* GetBones(this ref studiohdr_t header)
    {
        fixed (studiohdr_t* pHeader = &header)
        {
            return (mstudiobone_t*)((byte*)pHeader + pHeader->boneindex);
        }
    }

    /// <summary>
    /// Get sequence array from studio header
    /// Original: pseqdesc = (mstudioseqdesc_t*)((byte*)m_pStudioHeader + m_pStudioHeader->seqindex);
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static mstudioseqdesc_t* GetSequences(this ref studiohdr_t header)
    {
        fixed (studiohdr_t* pHeader = &header)
        {
            return (mstudioseqdesc_t*)((byte*)pHeader + pHeader->seqindex);
        }
    }

    /// <summary>
    /// Get sequence group array from studio header
    /// Original: pseqgroup = (mstudioseqgroup_t*)((byte*)m_pStudioHeader + m_pStudioHeader->seqgroupindex);
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static mstudioseqgroup_t* GetSequenceGroups(this ref studiohdr_t header)
    {
        fixed (studiohdr_t* pHeader = &header)
        {
            return (mstudioseqgroup_t*)((byte*)pHeader + pHeader->seqgroupindex);
        }
    }

    /// <summary>
    /// Get body part array from studio header
    /// Original: pbodypart = (mstudiobodyparts_t*)((byte*)m_pStudioHeader + m_pStudioHeader->bodypartindex);
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static mstudiobodyparts_t* GetBodyParts(this ref studiohdr_t header)
    {
        fixed (studiohdr_t* pHeader = &header)
        {
            return (mstudiobodyparts_t*)((byte*)pHeader + pHeader->bodypartindex);
        }
    }

    /// <summary>
    /// Get attachment array from studio header
    /// Original: pattachment = (mstudioattachment_t*)((byte*)m_pStudioHeader + m_pStudioHeader->attachmentindex);
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static mstudioattachment_t* GetAttachments(this ref studiohdr_t header)
    {
        fixed (studiohdr_t* pHeader = &header)
        {
            return (mstudioattachment_t*)((byte*)pHeader + pHeader->attachmentindex);
        }
    }

    /// <summary>
    /// Get hitbox array from studio header
    /// Original: pbbox = (mstudiobbox_t*)((byte*)m_pStudioHeader + m_pStudioHeader->hitboxindex);
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static mstudiobbox_t* GetHitboxes(this ref studiohdr_t header)
    {
        fixed (studiohdr_t* pHeader = &header)
        {
            return (mstudiobbox_t*)((byte*)pHeader + pHeader->hitboxindex);
        }
    }

    /// <summary>
    /// Get texture array from studio header
    /// Original: ptexture = (mstudiotexture_t*)((byte*)m_pStudioHeader + m_pStudioHeader->textureindex);
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static mstudiotexture_t* GetTextures(this ref studiohdr_t header)
    {
        fixed (studiohdr_t* pHeader = &header)
        {
            return (mstudiotexture_t*)((byte*)pHeader + pHeader->textureindex);
        }
    }

    /// <summary>
    /// Get model array from body part
    /// Original: pmodel = (mstudiomodel_t*)((byte*)m_pStudioHeader + pbodypart->modelindex);
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static mstudiomodel_t* GetModels(this ref mstudiobodyparts_t bodypart, studiohdr_t* pHeader)
    {
        fixed (mstudiobodyparts_t* pBodyPart = &bodypart)
        {
            return (mstudiomodel_t*)((byte*)pHeader + pBodyPart->modelindex);
        }
    }

    /// <summary>
    /// Get mesh array from model
    /// Original: pmesh = (mstudiomesh_t*)((byte*)m_pStudioHeader + pmodel->meshindex);
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static mstudiomesh_t* GetMeshes(this ref mstudiomodel_t model, studiohdr_t* pHeader)
    {
        fixed (mstudiomodel_t* pModel = &model)
        {
            return (mstudiomesh_t*)((byte*)pHeader + pModel->meshindex);
        }
    }

    /// <summary>
    /// Get event array from sequence
    /// Original: pevent = (mstudioevent_t*)((byte*)m_pStudioHeader + pseqdesc->eventindex);
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static mstudioevent_t* GetEvents(this ref mstudioseqdesc_t seqdesc, studiohdr_t* pHeader)
    {
        fixed (mstudioseqdesc_t* pSeqDesc = &seqdesc)
        {
            return (mstudioevent_t*)((byte*)pHeader + pSeqDesc->eventindex);
        }
    }

    /// <summary>
    /// Get pivot array from sequence
    /// Original: ppivot = (mstudiopivot_t*)((byte*)m_pStudioHeader + pseqdesc->pivotindex);
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static mstudiopivot_t* GetPivots(this ref mstudioseqdesc_t seqdesc, studiohdr_t* pHeader)
    {
        fixed (mstudioseqdesc_t* pSeqDesc = &seqdesc)
        {
            return (mstudiopivot_t*)((byte*)pHeader + pSeqDesc->pivotindex);
        }
    }

    /// <summary>
    /// Get animation data from sequence
    /// Original: panim = (mstudioanim_t*)((byte*)m_pStudioHeader + pseqdesc->animindex);
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static mstudioanim_t* GetAnims(this ref mstudioseqdesc_t seqdesc, studiohdr_t* pHeader)
    {
        fixed (mstudioseqdesc_t* pSeqDesc = &seqdesc)
        {
            return (mstudioanim_t*)((byte*)pHeader + pSeqDesc->animindex);
        }
    }

    /// <summary>
    /// Get vertex array from model
    /// Original: pvertex = (Vector*)((byte*)m_pStudioHeader + pmodel->vertindex);
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static LinearMath.Vector3* GetVertices(this ref mstudiomodel_t model, studiohdr_t* pHeader)
    {
        fixed (mstudiomodel_t* pModel = &model)
        {
            return (LinearMath.Vector3*)((byte*)pHeader + pModel->vertindex);
        }
    }

    /// <summary>
    /// Get normal array from model
    /// Original: pnormal = (Vector*)((byte*)m_pStudioHeader + pmodel->normindex);
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static LinearMath.Vector3* GetNormals(this ref mstudiomodel_t model, studiohdr_t* pHeader)
    {
        fixed (mstudiomodel_t* pModel = &model)
        {
            return (LinearMath.Vector3*)((byte*)pHeader + pModel->normindex);
        }
    }

    /// <summary>
    /// Helper to convert fixed NChar array to string
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string GetString(NativeInterop.NChar* ptr, int maxLength)
    {
        if (ptr == null) return string.Empty;

        int length = 0;
        byte* bytePtr = (byte*)ptr;
        while (length < maxLength && bytePtr[length] != 0)
            length++;

        return System.Text.Encoding.UTF8.GetString(bytePtr, length);
    }

    /// <summary>
    /// Get name from studiohdr_t
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string GetName(this ref studiohdr_t header)
    {
        return header.name.ToString() ?? string.Empty;
    }

    /// <summary>
    /// Get name from mstudiobone_t
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string GetName(this ref mstudiobone_t bone)
    {
        return bone.name.ToString() ?? string.Empty;
    }

    /// <summary>
    /// Get label from mstudioseqdesc_t
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string GetLabel(this ref mstudioseqdesc_t seqdesc)
    {
        return seqdesc.label.ToString() ?? string.Empty;
    }

}

/// <summary>
/// Static helper methods for pointer-based studio structure access
/// (Extension methods cannot be used with pointer types in C#)
/// </summary>
public static unsafe class StudioPointerHelpers
{
    /// <summary>
    /// Get bone controller array from studio header
    /// Original: pbonecontroller = (mstudiobonecontroller_t*)((byte*)m_pStudioHeader + m_pStudioHeader->bonecontrollerindex);
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static mstudiobonecontroller_t* GetBoneControllers(studiohdr_t* pHeader)
    {
        return (mstudiobonecontroller_t*)((byte*)pHeader + pHeader->bonecontrollerindex);
    }

    /// <summary>
    /// Get bone array from studio header
    /// Original: pbone = (mstudiobone_t*)((byte*)m_pStudioHeader + m_pStudioHeader->boneindex);
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static mstudiobone_t* GetBones(studiohdr_t* pHeader)
    {
        return (mstudiobone_t*)((byte*)pHeader + pHeader->boneindex);
    }

    /// <summary>
    /// Get sequence array from studio header
    /// Original: pseqdesc = (mstudioseqdesc_t*)((byte*)m_pStudioHeader + m_pStudioHeader->seqindex);
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static mstudioseqdesc_t* GetSequences(studiohdr_t* pHeader)
    {
        return (mstudioseqdesc_t*)((byte*)pHeader + pHeader->seqindex);
    }

    /// <summary>
    /// Get sequence group array from studio header
    /// Original: pseqgroup = (mstudioseqgroup_t*)((byte*)m_pStudioHeader + m_pStudioHeader->seqgroupindex);
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static mstudioseqgroup_t* GetSequenceGroups(studiohdr_t* pHeader)
    {
        return (mstudioseqgroup_t*)((byte*)pHeader + pHeader->seqgroupindex);
    }

    /// <summary>
    /// Get a specific sequence group by index from studio header
    /// Original: pseqgroup = (mstudioseqgroup_t*)((byte*)m_pStudioHeader + m_pStudioHeader->seqgroupindex) + index;
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static mstudioseqgroup_t* GetSequenceGroup(studiohdr_t* pHeader, int index)
    {
        return (mstudioseqgroup_t*)((byte*)pHeader + pHeader->seqgroupindex) + index;
    }

    /// <summary>
    /// Get body part array from studio header
    /// Original: pbodypart = (mstudiobodyparts_t*)((byte*)m_pStudioHeader + m_pStudioHeader->bodypartindex);
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static mstudiobodyparts_t* GetBodyParts(studiohdr_t* pHeader)
    {
        return (mstudiobodyparts_t*)((byte*)pHeader + pHeader->bodypartindex);
    }

    /// <summary>
    /// Get attachment array from studio header
    /// Original: pattachment = (mstudioattachment_t*)((byte*)m_pStudioHeader + m_pStudioHeader->attachmentindex);
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static mstudioattachment_t* GetAttachments(studiohdr_t* pHeader)
    {
        return (mstudioattachment_t*)((byte*)pHeader + pHeader->attachmentindex);
    }

    /// <summary>
    /// Get hitbox array from studio header
    /// Original: pbbox = (mstudiobbox_t*)((byte*)m_pStudioHeader + m_pStudioHeader->hitboxindex);
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static mstudiobbox_t* GetHitboxes(studiohdr_t* pHeader)
    {
        return (mstudiobbox_t*)((byte*)pHeader + pHeader->hitboxindex);
    }

    /// <summary>
    /// Get texture array from studio header
    /// Original: ptexture = (mstudiotexture_t*)((byte*)m_pStudioHeader + m_pStudioHeader->textureindex);
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static mstudiotexture_t* GetTextures(studiohdr_t* pHeader)
    {
        return (mstudiotexture_t*)((byte*)pHeader + pHeader->textureindex);
    }

    /// <summary>
    /// Get model array from body part
    /// Original: pmodel = (mstudiomodel_t*)((byte*)m_pStudioHeader + pbodypart->modelindex);
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static mstudiomodel_t* GetModels(mstudiobodyparts_t* pBodyPart, studiohdr_t* pHeader)
    {
        return (mstudiomodel_t*)((byte*)pHeader + pBodyPart->modelindex);
    }

    /// <summary>
    /// Get mesh array from model
    /// Original: pmesh = (mstudiomesh_t*)((byte*)m_pStudioHeader + pmodel->meshindex);
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static mstudiomesh_t* GetMeshes(mstudiomodel_t* pModel, studiohdr_t* pHeader)
    {
        return (mstudiomesh_t*)((byte*)pHeader + pModel->meshindex);
    }

    /// <summary>
    /// Get event array from sequence
    /// Original: pevent = (mstudioevent_t*)((byte*)m_pStudioHeader + pseqdesc->eventindex);
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static mstudioevent_t* GetEvents(mstudioseqdesc_t* pSeqDesc, studiohdr_t* pHeader)
    {
        return (mstudioevent_t*)((byte*)pHeader + pSeqDesc->eventindex);
    }

    /// <summary>
    /// Get pivot array from sequence
    /// Original: ppivot = (mstudiopivot_t*)((byte*)m_pStudioHeader + pseqdesc->pivotindex);
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static mstudiopivot_t* GetPivots(mstudioseqdesc_t* pSeqDesc, studiohdr_t* pHeader)
    {
        return (mstudiopivot_t*)((byte*)pHeader + pSeqDesc->pivotindex);
    }

    /// <summary>
    /// Get animation data from sequence
    /// Original: panim = (mstudioanim_t*)((byte*)m_pStudioHeader + pseqdesc->animindex);
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static mstudioanim_t* GetAnims(mstudioseqdesc_t* pSeqDesc, studiohdr_t* pHeader)
    {
        return (mstudioanim_t*)((byte*)pHeader + pSeqDesc->animindex);
    }

    /// <summary>
    /// Get vertex array from model
    /// Original: pvertex = (Vector*)((byte*)m_pStudioHeader + pmodel->vertindex);
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static LinearMath.Vector3* GetVertices(mstudiomodel_t* pModel, studiohdr_t* pHeader)
    {
        return (LinearMath.Vector3*)((byte*)pHeader + pModel->vertindex);
    }

    /// <summary>
    /// Get normal array from model
    /// Original: pnormal = (Vector*)((byte*)m_pStudioHeader + pmodel->normindex);
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static LinearMath.Vector3* GetNormals(mstudiomodel_t* pModel, studiohdr_t* pHeader)
    {
        return (LinearMath.Vector3*)((byte*)pHeader + pModel->normindex);
    }
}


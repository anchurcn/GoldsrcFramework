using GoldsrcFramework.Engine.Native;
using GoldsrcFramework.LinearMath;
using NativeInterop;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace GoldsrcFramework.Rendering;

/// <summary>
/// Studio Model Renderer - handles rendering of Half-Life studio models (.mdl files)
/// This is a C# port of the original C++ CStudioModelRenderer class
/// </summary>
public unsafe class StudioModelRenderer
{
    #region Constants and Enums

    // Move type enumeration
    private enum MoveType
    {
        MOVETYPE_NONE = 0,
        MOVETYPE_WALK = 3,
        MOVETYPE_STEP = 4,
        MOVETYPE_FLY = 5,
        MOVETYPE_TOSS = 6,
        MOVETYPE_PUSH = 7,
        MOVETYPE_NOCLIP = 8,
        MOVETYPE_FLYMISSILE = 9,
        MOVETYPE_BOUNCE = 10,
        MOVETYPE_BOUNCEMISSILE = 11,
        MOVETYPE_FOLLOW = 12,
        MOVETYPE_PUSHSTEP = 13
    }

    // Entity flags
    [Flags]
    private enum EntityFlags
    {
        EFLAG_SLERP = 1
    }

    private const int STUDIO_RENDER = 0x0001;
    private const int STUDIO_EVENTS = 0x0002;

    private const float ZISCALE = 0x8000;

    #endregion

    #region Member Variables - Time and Interpolation

    // Client clock
    private double m_clTime;
    // Old Client clock
    private double m_clOldTime;

    // Do interpolation?
    private bool m_fDoInterp;
    // Do gait estimation?
    private bool m_fGaitEstimation;

    // Current render frame #
    private int m_nFrameCount;

    #endregion

    #region Member Variables - CVars

    // Cvars that studio model code needs to reference
    // Use high quality models?
    private cvar_s* m_pCvarHiModels;
    // Developer debug output desired?
    private cvar_s* m_pCvarDeveloper;
    // Draw entities bone hit boxes, etc?
    private cvar_s* m_pCvarDrawEntities;

    #endregion

    #region Member Variables - Current Entity and Model

    // The entity which we are currently rendering.
    private cl_entity_s* m_pCurrentEntity;

    // The model for the entity being rendered
    private model_s* m_pRenderModel;

    // Player info for current player, if drawing a player
    private player_info_s* m_pPlayerInfo;

    // The index of the player being drawn
    private int m_nPlayerIndex;

    // The player's gait movement
    private float m_flGaitMovement;

    // Pointer to header block for studio model data
    private studiohdr_t* m_pStudioHeader;

    // Pointers to current body part and submodel
    private mstudiobodyparts_t* m_pBodyPart;
    private mstudiomodel_t* m_pSubModel;

    // Palette substition for top and bottom of model
    private int m_nTopColor;
    private int m_nBottomColor;

    // Sprite model used for drawing studio model chrome
    private model_s* m_pChromeSprite;

    #endregion

    #region Member Variables - Caching

    // Number of bones in bone cache
    private int m_nCachedBones;
    // Names of cached bones
    private NChar[,] m_nCachedBoneNames = new NChar[StudioConstants.MAXSTUDIOBONES, 32];
    // Cached bone & light transformation matrices
    private Matrix3x4[] m_rgCachedBoneTransform = new Matrix3x4[StudioConstants.MAXSTUDIOBONES];
    private Matrix3x4[] m_rgCachedLightTransform = new Matrix3x4[StudioConstants.MAXSTUDIOBONES];

    #endregion

    #region Member Variables - Rendering State

    // Software renderer scale factors
    private float m_fSoftwareXScale;
    private float m_fSoftwareYScale;

    // Current view vectors and render origin
    private Vector3 m_vUp;
    private Vector3 m_vRight;
    private Vector3 m_vNormal;
    private Vector3 m_vRenderOrigin;

    // Model render counters (from engine)
    private int* m_pStudioModelCount;
    private int* m_pModelsDrawn;

    // Matrices
    // Model to world transformation (pointer to Matrix3x4)
    private Matrix3x4* m_protationmatrix;
    // Model to view transformation (pointer to Matrix3x4)
    private Matrix3x4* m_paliastransform;

    // Concatenated bone and light transforms (pointer to Matrix3x4[MAXSTUDIOBONES])
    private Matrix3x4* m_pbonetransform;
    private Matrix3x4* m_plighttransform;

    #endregion

    #region Static Members

    // Engine Studio API
    private static engine_studio_api_s* IEngineStudio;

    // Global instance
    private static StudioModelRenderer? _instance;

    #endregion

    #region Constructor and Initialization

    /// <summary>
    /// Constructor
    /// </summary>
    public StudioModelRenderer()
    {
        m_fDoInterp = true;
        m_fGaitEstimation = true;
        m_pCurrentEntity = null;
        m_pCvarHiModels = null;
        m_pCvarDeveloper = null;
        m_pCvarDrawEntities = null;
        m_pChromeSprite = null;
        m_pStudioModelCount = null;
        m_pModelsDrawn = null;
        m_protationmatrix = null;
        m_paliastransform = null;
        m_pbonetransform = null;
        m_plighttransform = null;
        m_pStudioHeader = null;
        m_pBodyPart = null;
        m_pSubModel = null;
        m_pPlayerInfo = null;
        m_pRenderModel = null;
    }

    /// <summary>
    /// Initialization
    /// </summary>
    public virtual void Init()
    {
        // Set up some variables shared with engine
        m_pCvarHiModels = IEngineStudio->GetCvar((NChar*)Marshal.StringToHGlobalAnsi("cl_himodels"));
        m_pCvarDeveloper = IEngineStudio->GetCvar((NChar*)Marshal.StringToHGlobalAnsi("developer"));
        m_pCvarDrawEntities = IEngineStudio->GetCvar((NChar*)Marshal.StringToHGlobalAnsi("r_drawentities"));

        m_pChromeSprite = IEngineStudio->GetChromeSprite();

        int* pModelCount = null;
        int* pModelsDrawn = null;
        IEngineStudio->GetModelCounters(&pModelCount, &pModelsDrawn);

        m_pStudioModelCount = pModelCount;
        m_pModelsDrawn = pModelsDrawn;

        // Get pointers to engine data structures
        m_pbonetransform = (Matrix3x4*)IEngineStudio->StudioGetBoneTransform();
        m_plighttransform = (Matrix3x4*)IEngineStudio->StudioGetLightTransform();
        m_paliastransform = (Matrix3x4*)IEngineStudio->StudioGetAliasTransform();
        m_protationmatrix = (Matrix3x4*)IEngineStudio->StudioGetRotationMatrix();
    }

    #endregion

    #region Public Interface Methods

    /// <summary>
    /// Main entry point for drawing a studio model
    /// Original: bool CStudioModelRenderer::StudioDrawModel(int flags)
    /// </summary>
    /// <param name="flags">Rendering flags (STUDIO_RENDER, STUDIO_EVENTS)</param>
    /// <returns>True if model was drawn successfully</returns>
    public virtual bool StudioDrawModel(int flags)
    {
        alight_s lighting;
        Vector3 dir;

        m_pCurrentEntity = IEngineStudio->GetCurrentEntity();

        fixed (int* pFrameCount = &m_nFrameCount)
        fixed (double* pClTime = &m_clTime, pClOldTime = &m_clOldTime)
        {
            IEngineStudio->GetTimes(pFrameCount, pClTime, pClOldTime);
        }

        fixed (Vector3* pOrigin = &m_vRenderOrigin, pUp = &m_vUp, pRight = &m_vRight, pNormal = &m_vNormal)
        fixed (float* pXScale = &m_fSoftwareXScale, pYScale = &m_fSoftwareYScale)
        {
            IEngineStudio->GetViewInfo((float*)pOrigin, (float*)pUp, (float*)pRight, (float*)pNormal);
            IEngineStudio->GetAliasScale(pXScale, pYScale);
        }

        // Special handling for dead player rendering
        if (m_pCurrentEntity->curstate.renderfx == (int)RenderFx.kRenderFxDeadPlayer)
        {
            entity_state_s deadplayer;
            bool result;
            bool save_interp;

            if (m_pCurrentEntity->curstate.renderamt <= 0 ||
                m_pCurrentEntity->curstate.renderamt > EngineApi.PClient->GetMaxClients())
                return false;

            // Get copy of player
            deadplayer = *(IEngineStudio->GetPlayerState(m_pCurrentEntity->curstate.renderamt - 1));

            // Clear weapon, movement state
            deadplayer.number = m_pCurrentEntity->curstate.renderamt;
            deadplayer.weaponmodel = 0;
            deadplayer.gaitsequence = 0;

            deadplayer.movetype = (int)MoveType.MOVETYPE_NONE;
            deadplayer.angles = m_pCurrentEntity->curstate.angles;
            deadplayer.origin = m_pCurrentEntity->curstate.origin;

            save_interp = m_fDoInterp;
            m_fDoInterp = false;

            // Draw as though it were a player
            result = StudioDrawPlayer(flags, &deadplayer);

            m_fDoInterp = save_interp;
            return result;
        }

        m_pRenderModel = m_pCurrentEntity->model;
        m_pStudioHeader = (studiohdr_t*)IEngineStudio->Mod_Extradata(m_pRenderModel);
        IEngineStudio->StudioSetHeader(m_pStudioHeader);
        IEngineStudio->SetRenderModel(m_pRenderModel);

        StudioSetUpTransform(false);

        if ((flags & STUDIO_RENDER) != 0)
        {
            Debug.WriteLine("studio ent: " + (IntPtr)m_pCurrentEntity);
            Debug.WriteLine("studio api size: " + (IntPtr)m_pStudioHeader);
            // see if the bounding box lets us trivially reject, also sets
            if (IEngineStudio->StudioCheckBBox() == 0)
                return false;

            (*m_pModelsDrawn)++;
            (*m_pStudioModelCount)++; // render data cache cookie

            if (m_pStudioHeader->numbodyparts == 0)
                return true;
        }

        if (m_pCurrentEntity->curstate.movetype == (int)MoveType.MOVETYPE_FOLLOW)
        {
            StudioMergeBones(m_pRenderModel);
        }
        else
        {
            StudioSetupBones();
        }

        StudioSaveBones();

        if ((flags & STUDIO_EVENTS) != 0)
        {
            StudioCalcAttachments();
            IEngineStudio->StudioClientEvents();
            // copy attachments into global entity array
            if (m_pCurrentEntity->index > 0)
            {
                cl_entity_s* ent = EngineApi.PClient->GetEntityByIndex(m_pCurrentEntity->index);

                // Copy 4 attachment points
                for (int i = 0; i < 4; i++)
                {
                    ent->attachment[i] = m_pCurrentEntity->attachment[i];
                }
            }
        }

        if ((flags & STUDIO_RENDER) != 0)
        {
            lighting.plightvec = (float*)&dir;
            IEngineStudio->StudioDynamicLight(m_pCurrentEntity, &lighting);

            IEngineStudio->StudioEntityLight(&lighting);

            // model and frame independant
            IEngineStudio->StudioSetupLighting(&lighting);

            // get remap colors
            m_nTopColor = m_pCurrentEntity->curstate.colormap & 0xFF;
            m_nBottomColor = (m_pCurrentEntity->curstate.colormap & 0xFF00) >> 8;

            IEngineStudio->StudioSetRemapColors(m_nTopColor, m_nBottomColor);

            StudioRenderModel();
        }

        return true;
    }

    /// <summary>
    /// Draw a player model
    /// Original: bool CStudioModelRenderer::StudioDrawPlayer(int flags, entity_state_t* pplayer)
    /// </summary>
    /// <param name="flags">Rendering flags</param>
    /// <param name="pplayer">Player entity state</param>
    /// <returns>True if player was drawn successfully</returns>
    public virtual bool StudioDrawPlayer(int flags, entity_state_s* pplayer)
    {
        alight_s lighting;
        Vector3 dir;

        m_pCurrentEntity = IEngineStudio->GetCurrentEntity();

        fixed (int* pFrameCount = &m_nFrameCount)
        fixed (double* pClTime = &m_clTime, pClOldTime = &m_clOldTime)
        {
            IEngineStudio->GetTimes(pFrameCount, pClTime, pClOldTime);
        }

        fixed (Vector3* pOrigin = &m_vRenderOrigin, pUp = &m_vUp, pRight = &m_vRight, pNormal = &m_vNormal)
        fixed (float* pXScale = &m_fSoftwareXScale, pYScale = &m_fSoftwareYScale)
        {
            IEngineStudio->GetViewInfo((float*)pOrigin, (float*)pUp, (float*)pRight, (float*)pNormal);
            IEngineStudio->GetAliasScale(pXScale, pYScale);
        }

        m_nPlayerIndex = pplayer->number - 1;

        if (m_nPlayerIndex < 0 || m_nPlayerIndex >= EngineApi.PClient->GetMaxClients())
            return false;

        m_pRenderModel = IEngineStudio->SetupPlayerModel(m_nPlayerIndex);

        if (m_pRenderModel == null)
            return false;

        m_pStudioHeader = (studiohdr_t*)IEngineStudio->Mod_Extradata(m_pRenderModel);
        IEngineStudio->StudioSetHeader(m_pStudioHeader);
        IEngineStudio->SetRenderModel(m_pRenderModel);

        if (pplayer->gaitsequence != 0)
        {
            Vector3 orig_angles;
            m_pPlayerInfo = IEngineStudio->PlayerInfo(m_nPlayerIndex);

            orig_angles = m_pCurrentEntity->angles;

            StudioProcessGait(pplayer);

            m_pPlayerInfo->gaitsequence = pplayer->gaitsequence;
            m_pPlayerInfo = null;

            StudioSetUpTransform(false);
            m_pCurrentEntity->angles = orig_angles;
        }
        else
        {
            m_pCurrentEntity->curstate.controller[0] = 127;
            m_pCurrentEntity->curstate.controller[1] = 127;
            m_pCurrentEntity->curstate.controller[2] = 127;
            m_pCurrentEntity->curstate.controller[3] = 127;
            m_pCurrentEntity->latched.prevcontroller[0] = m_pCurrentEntity->curstate.controller[0];
            m_pCurrentEntity->latched.prevcontroller[1] = m_pCurrentEntity->curstate.controller[1];
            m_pCurrentEntity->latched.prevcontroller[2] = m_pCurrentEntity->curstate.controller[2];
            m_pCurrentEntity->latched.prevcontroller[3] = m_pCurrentEntity->curstate.controller[3];

            m_pPlayerInfo = IEngineStudio->PlayerInfo(m_nPlayerIndex);
            m_pPlayerInfo->gaitsequence = 0;

            StudioSetUpTransform(false);
        }

        if ((flags & STUDIO_RENDER) != 0)
        {
            // see if the bounding box lets us trivially reject, also sets
            if (IEngineStudio->StudioCheckBBox() == 0)
                return false;

            (*m_pModelsDrawn)++;
            (*m_pStudioModelCount)++; // render data cache cookie

            if (m_pStudioHeader->numbodyparts == 0)
                return true;
        }

        m_pPlayerInfo = IEngineStudio->PlayerInfo(m_nPlayerIndex);
        StudioSetupBones();
        StudioSaveBones();
        m_pPlayerInfo->renderframe = m_nFrameCount;

        m_pPlayerInfo = null;

        if ((flags & STUDIO_EVENTS) != 0)
        {
            StudioCalcAttachments();
            IEngineStudio->StudioClientEvents();
            // copy attachments into global entity array
            if (m_pCurrentEntity->index > 0)
            {
                cl_entity_s* ent = EngineApi.PClient->GetEntityByIndex(m_pCurrentEntity->index);

                // Copy 4 attachment points
                for (int i = 0; i < 4; i++)
                {
                    ent->attachment[i] = m_pCurrentEntity->attachment[i];
                }
            }
        }

        if ((flags & STUDIO_RENDER) != 0)
        {
            if (m_pCvarHiModels->value != 0 && m_pRenderModel != m_pCurrentEntity->model)
            {
                // show highest resolution multiplayer model
                m_pCurrentEntity->curstate.body = 255;
            }

            if (!(m_pCvarDeveloper->value == 0 && EngineApi.PClient->GetMaxClients() == 1) &&
                (m_pRenderModel == m_pCurrentEntity->model))
            {
                m_pCurrentEntity->curstate.body = 1; // force helmet
            }

            lighting.plightvec = (float*)&dir;
            IEngineStudio->StudioDynamicLight(m_pCurrentEntity, &lighting);

            IEngineStudio->StudioEntityLight(&lighting);

            // model and frame independant
            IEngineStudio->StudioSetupLighting(&lighting);

            m_pPlayerInfo = IEngineStudio->PlayerInfo(m_nPlayerIndex);

            // get remap colors
            m_nTopColor = m_pPlayerInfo->topcolor;
            m_nBottomColor = m_pPlayerInfo->bottomcolor;

            // bounds check
            if (m_nTopColor < 0)
                m_nTopColor = 0;
            if (m_nTopColor > 360)
                m_nTopColor = 360;
            if (m_nBottomColor < 0)
                m_nBottomColor = 0;
            if (m_nBottomColor > 360)
                m_nBottomColor = 360;

            IEngineStudio->StudioSetRemapColors(m_nTopColor, m_nBottomColor);

            StudioRenderModel();
            m_pPlayerInfo = null;

            if (pplayer->weaponmodel != 0)
            {
                cl_entity_s saveent = *m_pCurrentEntity;

                model_s* pweaponmodel = IEngineStudio->GetModelByIndex(pplayer->weaponmodel);

                m_pStudioHeader = (studiohdr_t*)IEngineStudio->Mod_Extradata(pweaponmodel);
                IEngineStudio->StudioSetHeader(m_pStudioHeader);

                StudioMergeBones(pweaponmodel);

                IEngineStudio->StudioSetupLighting(&lighting);

                StudioRenderModel();

                StudioCalcAttachments();

                *m_pCurrentEntity = saveent;
            }
        }

        return true;
    }

    #endregion

    #region Bone Calculation Methods

    /// <summary>
    /// Calculate bone controller adjustments
    /// Original: void CStudioModelRenderer::StudioCalcBoneAdj(float dadt, float* adj, const byte* pcontroller1, const byte* pcontroller2, byte mouthopen)
    /// </summary>
    private void StudioCalcBoneAdj(float dadt, float* adj, byte* pcontroller1, byte* pcontroller2, byte mouthopen)
    {
        int i, j;
        float value;
        mstudiobonecontroller_t* pbonecontroller;

        pbonecontroller = m_pStudioHeader->GetBoneControllers();

        for (j = 0; j < m_pStudioHeader->numbonecontrollers; j++)
        {
            i = pbonecontroller[j].index;
            if (i <= 3)
            {
                // check for 360% wrapping
                if ((pbonecontroller[j].type & (int)StudioMotionFlags.STUDIO_RLOOP) != 0)
                {
                    if (Math.Abs(pcontroller1[i] - pcontroller2[i]) > 128)
                    {
                        int a, b;
                        a = (pcontroller1[j] + 128) % 256;
                        b = (pcontroller2[j] + 128) % 256;
                        value = ((a * dadt) + (b * (1 - dadt)) - 128) * (360.0f / 256.0f) + pbonecontroller[j].start;
                    }
                    else
                    {
                        value = ((pcontroller1[i] * dadt + (pcontroller2[i]) * (1.0f - dadt))) * (360.0f / 256.0f) + pbonecontroller[j].start;
                    }
                }
                else
                {
                    value = (pcontroller1[i] * dadt + pcontroller2[i] * (1.0f - dadt)) / 255.0f;
                    if (value < 0)
                        value = 0;
                    if (value > 1.0f)
                        value = 1.0f;
                    value = (1.0f - value) * pbonecontroller[j].start + value * pbonecontroller[j].end;
                }
            }
            else
            {
                value = mouthopen / 64.0f;
                if (value > 1.0f)
                    value = 1.0f;
                value = (1.0f - value) * pbonecontroller[j].start + value * pbonecontroller[j].end;
            }

            switch (pbonecontroller[j].type & (int)StudioMotionFlags.STUDIO_TYPES)
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
    /// Calculate bone quaternion for animation frame
    /// Original: void CStudioModelRenderer::StudioCalcBoneQuaterion(int frame, float s, mstudiobone_t* pbone, mstudioanim_t* panim, float* adj, float* q)
    /// </summary>
    private void StudioCalcBoneQuaterion(int frame, float s, mstudiobone_t* pbone, mstudioanim_t* panim, float* adj, float* q)
    {
        int j, k;
        Span<float> q1 = stackalloc float[4];
        Span<float> q2 = stackalloc float[4];
        Span<float> angle1 = stackalloc float[3];
        Span<float> angle2 = stackalloc float[3];
        mstudioanimvalue_t* panimvalue;

        for (j = 0; j < 3; j++)
        {
            if (panim->offset[j + 3] == 0)
            {
                angle2[j] = angle1[j] = pbone->value[j + 3]; // default
            }
            else
            {
                panimvalue = (mstudioanimvalue_t*)((byte*)panim + panim->offset[j + 3]);
                k = frame;

                // DEBUG
                if (panimvalue->num.total < panimvalue->num.valid)
                    k = 0;

                while (panimvalue->num.total <= k)
                {
                    k -= panimvalue->num.total;
                    panimvalue += panimvalue->num.valid + 1;

                    // DEBUG
                    if (panimvalue->num.total < panimvalue->num.valid)
                        k = 0;
                }

                // Bah, missing blend!
                if (panimvalue->num.valid > k)
                {
                    angle1[j] = panimvalue[k + 1].value;

                    if (panimvalue->num.valid > k + 1)
                    {
                        angle2[j] = panimvalue[k + 2].value;
                    }
                    else
                    {
                        if (panimvalue->num.total > k + 1)
                            angle2[j] = angle1[j];
                        else
                            angle2[j] = panimvalue[panimvalue->num.valid + 2].value;
                    }
                }
                else
                {
                    angle1[j] = panimvalue[panimvalue->num.valid].value;
                    if (panimvalue->num.total > k + 1)
                    {
                        angle2[j] = angle1[j];
                    }
                    else
                    {
                        angle2[j] = panimvalue[panimvalue->num.valid + 2].value;
                    }
                }

                angle1[j] = pbone->value[j + 3] + angle1[j] * pbone->scale[j + 3];
                angle2[j] = pbone->value[j + 3] + angle2[j] * pbone->scale[j + 3];
            }

            if (pbone->bonecontroller[j + 3] != -1)
            {
                angle1[j] += adj[pbone->bonecontroller[j + 3]];
                angle2[j] += adj[pbone->bonecontroller[j + 3]];
            }
        }

        fixed (float* pAngle1 = angle1, pAngle2 = angle2, pQ1 = q1, pQ2 = q2)
        {
            if (!StudioMath.VectorCompare(pAngle1, pAngle2))
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
    /// Calculate bone position for animation frame
    /// Original: void CStudioModelRenderer::StudioCalcBonePosition(int frame, float s, mstudiobone_t* pbone, mstudioanim_t* panim, float* adj, float* pos)
    /// </summary>
    private void StudioCalcBonePosition(int frame, float s, mstudiobone_t* pbone, mstudioanim_t* panim, float* adj, float* pos)
    {
        int j, k;
        mstudioanimvalue_t* panimvalue;

        for (j = 0; j < 3; j++)
        {
            pos[j] = pbone->value[j]; // default

            if (panim->offset[j] != 0)
            {
                panimvalue = (mstudioanimvalue_t*)((byte*)panim + panim->offset[j]);

                k = frame;
                // DEBUG
                if (panimvalue->num.total < panimvalue->num.valid)
                    k = 0;

                // find span of values that includes the frame we want
                while (panimvalue->num.total <= k)
                {
                    k -= panimvalue->num.total;
                    panimvalue += panimvalue->num.valid + 1;

                    // DEBUG
                    if (panimvalue->num.total < panimvalue->num.valid)
                        k = 0;
                }

                // if we're inside the span
                if (panimvalue->num.valid > k)
                {
                    // and there's more data in the span
                    if (panimvalue->num.valid > k + 1)
                    {
                        pos[j] += (panimvalue[k + 1].value * (1.0f - s) + s * panimvalue[k + 2].value) * pbone->scale[j];
                    }
                    else
                    {
                        pos[j] += panimvalue[k + 1].value * pbone->scale[j];
                    }
                }
                else
                {
                    // are we at the end of the repeating values section and there's another section with data?
                    if (panimvalue->num.total <= k + 1)
                    {
                        pos[j] += (panimvalue[panimvalue->num.valid].value * (1.0f - s) + s * panimvalue[panimvalue->num.valid + 2].value) * pbone->scale[j];
                    }
                    else
                    {
                        pos[j] += panimvalue[panimvalue->num.valid].value * pbone->scale[j];
                    }
                }
            }

            if (pbone->bonecontroller[j] != -1 && adj != null)
            {
                pos[j] += adj[pbone->bonecontroller[j]];
            }
        }
    }

    /// <summary>
    /// Spherical linear interpolation between two sets of bone transforms
    /// Original: void CStudioModelRenderer::StudioSlerpBones(vec4_t q1[], float pos1[][3], vec4_t q2[], float pos2[][3], float s)
    /// </summary>
    private void StudioSlerpBones(float* q1, float* pos1, float* q2, float* pos2, float s)
    {
        int i;
        Span<float> q3 = stackalloc float[4];
        float s1;

        if (s < 0)
            s = 0;
        else if (s > 1.0f)
            s = 1.0f;

        s1 = 1.0f - s;

        for (i = 0; i < m_pStudioHeader->numbones; i++)
        {
            float* q1_i = q1 + (i * 4);
            float* q2_i = q2 + (i * 4);
            float* pos1_i = pos1 + (i * 3);
            float* pos2_i = pos2 + (i * 3);

            fixed (float* pQ3 = q3)
            {
                StudioMath.QuaternionSlerp(q1_i, q2_i, s, pQ3);
                q1_i[0] = pQ3[0];
                q1_i[1] = pQ3[1];
                q1_i[2] = pQ3[2];
                q1_i[3] = pQ3[3];
            }

            pos1_i[0] = pos1_i[0] * s1 + pos2_i[0] * s;
            pos1_i[1] = pos1_i[1] * s1 + pos2_i[1] * s;
            pos1_i[2] = pos1_i[2] * s1 + pos2_i[2] * s;
        }
    }

    /// <summary>
    /// Calculate bone rotations and positions for current frame
    /// Original: void CStudioModelRenderer::StudioCalcRotations(float pos[][3], vec4_t* q, mstudioseqdesc_t* pseqdesc, mstudioanim_t* panim, float f)
    /// </summary>
    private void StudioCalcRotations(float* pos, float* q, mstudioseqdesc_t* pseqdesc, mstudioanim_t* panim, float f)
    {
        int i;
        int frame;
        mstudiobone_t* pbone;

        float s;
        Span<float> adj = stackalloc float[StudioConstants.MAXSTUDIOCONTROLLERS];
        float dadt;

        if (f > pseqdesc->numframes - 1)
        {
            f = 0; // bah, fix this bug with changing sequences too fast
        }
        // BUG ( somewhere else ) but this code should validate this data.
        // This could cause a crash if the frame # is negative, so we'll go ahead
        //  and clamp it here
        else if (f < -0.01f)
        {
            f = -0.01f;
        }

        frame = (int)f;

        dadt = StudioEstimateInterpolant();
        s = (f - frame);

        // add in programtic controllers
        pbone = m_pStudioHeader->GetBones();

        fixed (float* pAdj = adj)
        {
            StudioCalcBoneAdj(dadt, pAdj, m_pCurrentEntity->curstate.controller, m_pCurrentEntity->latched.prevcontroller, m_pCurrentEntity->mouth.mouthopen);

            for (i = 0; i < m_pStudioHeader->numbones; i++, pbone++, panim++)
            {
                StudioCalcBoneQuaterion(frame, s, pbone, panim, pAdj, q + (i * 4));
                StudioCalcBonePosition(frame, s, pbone, panim, pAdj, pos + (i * 3));
            }
        }

        if ((pseqdesc->motiontype & (int)StudioMotionFlags.STUDIO_X) != 0)
        {
            pos[pseqdesc->motionbone * 3 + 0] = 0.0f;
        }
        if ((pseqdesc->motiontype & (int)StudioMotionFlags.STUDIO_Y) != 0)
        {
            pos[pseqdesc->motionbone * 3 + 1] = 0.0f;
        }
        if ((pseqdesc->motiontype & (int)StudioMotionFlags.STUDIO_Z) != 0)
        {
            pos[pseqdesc->motionbone * 3 + 2] = 0.0f;
        }

        s = 0 * ((1.0f - (f - (int)(f))) / (pseqdesc->numframes)) * m_pCurrentEntity->curstate.framerate;

        if ((pseqdesc->motiontype & (int)StudioMotionFlags.STUDIO_LX) != 0)
        {
            pos[pseqdesc->motionbone * 3 + 0] += s * pseqdesc->linearmovement.X;
        }
        if ((pseqdesc->motiontype & (int)StudioMotionFlags.STUDIO_LY) != 0)
        {
            pos[pseqdesc->motionbone * 3 + 1] += s * pseqdesc->linearmovement.Y;
        }
        if ((pseqdesc->motiontype & (int)StudioMotionFlags.STUDIO_LZ) != 0)
        {
            pos[pseqdesc->motionbone * 3 + 2] += s * pseqdesc->linearmovement.Z;
        }
    }

    /// <summary>
    /// Estimate interpolation factor for bone controllers
    /// Original: float CStudioModelRenderer::StudioEstimateInterpolant()
    /// </summary>
    private float StudioEstimateInterpolant()
    {
        float dadt = 1.0f;

        if (m_fDoInterp && (m_pCurrentEntity->curstate.animtime >= m_pCurrentEntity->latched.prevanimtime + 0.01))
        {
            dadt = (float)((m_clTime - m_pCurrentEntity->curstate.animtime) / 0.1);
            if (dadt > 2.0f)
            {
                dadt = 2.0f;
            }
        }
        return dadt;
    }

    /// <summary>
    /// Estimate current animation frame
    /// Original: float CStudioModelRenderer::StudioEstimateFrame(mstudioseqdesc_t* pseqdesc)
    /// </summary>
    private float StudioEstimateFrame(mstudioseqdesc_t* pseqdesc)
    {
        double dfdt, f;

        if (m_fDoInterp)
        {
            if (m_clTime < m_pCurrentEntity->curstate.animtime)
            {
                dfdt = 0;
            }
            else
            {
                dfdt = (m_clTime - m_pCurrentEntity->curstate.animtime) * m_pCurrentEntity->curstate.framerate * pseqdesc->fps;
            }
        }
        else
        {
            dfdt = 0;
        }

        if (pseqdesc->numframes <= 1)
        {
            f = 0;
        }
        else
        {
            f = (m_pCurrentEntity->curstate.frame * (pseqdesc->numframes - 1)) / 256.0;
        }

        f += dfdt;

        if ((pseqdesc->flags & (int)StudioSequenceFlags.STUDIO_LOOPING) != 0)
        {
            if (pseqdesc->numframes > 1)
            {
                f -= (int)(f / (pseqdesc->numframes - 1)) * (pseqdesc->numframes - 1);
            }
            if (f < 0)
            {
                f += (pseqdesc->numframes - 1);
            }
        }
        else
        {
            if (f >= pseqdesc->numframes - 1.001)
            {
                f = pseqdesc->numframes - 1.001;
            }
            if (f < 0.0)
            {
                f = 0.0;
            }
        }
        return (float)f;
    }

    #endregion

    #region Animation and Bone Setup Methods

    /// <summary>
    /// Look up animation data for sequence
    /// Original: mstudioanim_t* CStudioModelRenderer::StudioGetAnim(model_t* m_pSubModel, mstudioseqdesc_t* pseqdesc)
    /// </summary>
    private mstudioanim_t* StudioGetAnim(model_s* pSubModel, mstudioseqdesc_t* pseqdesc)
    {
        mstudioseqgroup_t* pseqgroup;
        cache_user_s* paSequences;

        pseqgroup = (mstudioseqgroup_t*)((byte*)m_pStudioHeader + m_pStudioHeader->seqgroupindex) + pseqdesc->seqgroup;

        if (pseqdesc->seqgroup == 0)
        {
            return (mstudioanim_t*)((byte*)m_pStudioHeader + pseqdesc->animindex);
        }

        paSequences = (cache_user_s*)pSubModel->submodels;

        if (paSequences == null)
        {
            paSequences = (cache_user_s*)IEngineStudio->Mem_Calloc(16, (nuint)sizeof(cache_user_s)); // UNDONE: leak!
            pSubModel->submodels = (dmodel_t*)paSequences;
        }

        if (IEngineStudio->Cache_Check((cache_user_s*)&paSequences[pseqdesc->seqgroup]) == null)
        {
            EngineApi.PClient->Con_DPrintf(pseqgroup->name.AsPointer());
            IEngineStudio->LoadCacheFile(pseqgroup->name.AsPointer(), (cache_user_s*)&paSequences[pseqdesc->seqgroup]);
        }

        return (mstudioanim_t*)((byte*)paSequences[pseqdesc->seqgroup].data + pseqdesc->animindex);
    }

    /// <summary>
    /// Interpolate model position and angles and set up matrices
    /// Original: void CStudioModelRenderer::StudioSetUpTransform(bool trivial_accept)
    /// </summary>
    private void StudioSetUpTransform(bool trivial_accept)
    {
        int i;
        Vector3 angles;
        Vector3 modelpos;

        // Copy model origin
        modelpos = m_pCurrentEntity->origin;

        // Get entity angles
        angles.X = m_pCurrentEntity->curstate.angles.X; // PITCH
        angles.Y = m_pCurrentEntity->curstate.angles.Y; // YAW
        angles.Z = m_pCurrentEntity->curstate.angles.Z; // ROLL

        // Handle MOVETYPE_STEP interpolation
        if (m_pCurrentEntity->curstate.movetype == (int)MoveType.MOVETYPE_STEP)
        {
            float f = 0;
            float d;

            // Don't do it if the goalstarttime hasn't updated in a while.
            // NOTE: Because we need to interpolate multiplayer characters, the interpolation time limit
            // was increased to 1.0 s., which is 2x the max lag we are accounting for.

            if ((m_clTime < m_pCurrentEntity->curstate.animtime + 1.0f) &&
                (m_pCurrentEntity->curstate.animtime != m_pCurrentEntity->latched.prevanimtime))
            {
                f = (float)((m_clTime - m_pCurrentEntity->curstate.animtime) /
                    (m_pCurrentEntity->curstate.animtime - m_pCurrentEntity->latched.prevanimtime));
            }

            if (m_fDoInterp)
            {
                // Ugly hack to interpolate angle, position. current is reached 0.1 seconds after being set
                f = f - 1.0f;
            }
            else
            {
                f = 0;
            }

            mstudioseqdesc_t* pseqdesc = m_pStudioHeader->GetSequences() + m_pCurrentEntity->curstate.sequence;

            if ((pseqdesc->motiontype & (int)StudioMotionFlags.STUDIO_LX) != 0 ||
                (m_pCurrentEntity->curstate.eflags & (int)EntityFlags.EFLAG_SLERP) != 0)
            {
                modelpos.X += (m_pCurrentEntity->origin.X - m_pCurrentEntity->latched.prevorigin.X) * f;
                modelpos.Y += (m_pCurrentEntity->origin.Y - m_pCurrentEntity->latched.prevorigin.Y) * f;
                modelpos.Z += (m_pCurrentEntity->origin.Z - m_pCurrentEntity->latched.prevorigin.Z) * f;
            }

            // Interpolate angles
            for (i = 0; i < 3; i++)
            {
                float ang1, ang2;

                ang1 = i == 0 ? m_pCurrentEntity->angles.X : (i == 1 ? m_pCurrentEntity->angles.Y : m_pCurrentEntity->angles.Z);
                ang2 = i == 0 ? m_pCurrentEntity->latched.prevangles.X : (i == 1 ? m_pCurrentEntity->latched.prevangles.Y : m_pCurrentEntity->latched.prevangles.Z);

                d = ang1 - ang2;
                if (d > 180)
                {
                    d -= 360;
                }
                else if (d < -180)
                {
                    d += 360;
                }

                if (i == 0)
                    angles.X += d * f;
                else if (i == 1)
                    angles.Y += d * f;
                else
                    angles.Z += d * f;
            }
        }
        else if (m_pCurrentEntity->curstate.movetype != (int)MoveType.MOVETYPE_NONE)
        {
            angles = m_pCurrentEntity->angles;
        }

        // Flip pitch
        angles.Y = -angles.Y;
        StudioMath.AngleMatrix(ref angles, ref *m_protationmatrix);

        if (IEngineStudio->IsHardware() == 0)
        {
            // Software rendering path
            Span<float> viewmatrix = stackalloc float[3 * 4];

            fixed (float* pViewMatrix = viewmatrix)
            {
                // Copy view vectors
                pViewMatrix[0 * 4 + 0] = m_vRight.X;
                pViewMatrix[0 * 4 + 1] = m_vRight.Y;
                pViewMatrix[0 * 4 + 2] = m_vRight.Z;
                pViewMatrix[0 * 4 + 3] = 0;

                pViewMatrix[1 * 4 + 0] = -m_vUp.X; // VectorInverse
                pViewMatrix[1 * 4 + 1] = -m_vUp.Y;
                pViewMatrix[1 * 4 + 2] = -m_vUp.Z;
                pViewMatrix[1 * 4 + 3] = 0;

                pViewMatrix[2 * 4 + 0] = m_vNormal.X;
                pViewMatrix[2 * 4 + 1] = m_vNormal.Y;
                pViewMatrix[2 * 4 + 2] = m_vNormal.Z;
                pViewMatrix[2 * 4 + 3] = 0;

                m_protationmatrix->M14 = modelpos.X - m_vRenderOrigin.X;
                m_protationmatrix->M24 = modelpos.Y - m_vRenderOrigin.Y;
                m_protationmatrix->M34 = modelpos.Z - m_vRenderOrigin.Z;

                StudioMath.ConcatTransforms(ref *(Matrix3x4*)pViewMatrix,ref  *m_protationmatrix, out *m_paliastransform);

                // Do the scaling up of x and y to screen coordinates as part of the transform
                // for the unclipped case (it would mess up clipping in the clipped case).
                if (trivial_accept)
                {
                    const float ZISCALE = 32768.0f;
                    for (i = 0; i < 4; i++)
                    {
                        float* pAlias = (float*)m_paliastransform;
                        pAlias[0 * 4 + i] *= m_fSoftwareXScale * (1.0f / (ZISCALE * 0x10000));
                        pAlias[1 * 4 + i] *= m_fSoftwareYScale * (1.0f / (ZISCALE * 0x10000));
                        pAlias[2 * 4 + i] *= 1.0f / (ZISCALE * 0x10000);
                    }
                }
            }
        }

        m_protationmatrix->M14 = modelpos.X;
        m_protationmatrix->M24 = modelpos.Y;
        m_protationmatrix->M34 = modelpos.Z;
    }

    /// <summary>
    /// Apply special effects to transformation matrix
    /// Original: void CStudioModelRenderer::StudioFxTransform(cl_entity_t* ent, float transform[3][4])
    /// </summary>
    private void StudioFxTransform(cl_entity_s* ent, Matrix3x4* transform)
    {
        switch ((RenderFx)ent->curstate.renderfx)
        {
            case RenderFx.kRenderFxDistort:
            case RenderFx.kRenderFxHologram:
                if (EngineApi.PClient->RandomLong(0, 49) == 0)
                {
                    int axis = EngineApi.PClient->RandomLong(0, 1);
                    if (axis == 1) // Choose between x & z
                        axis = 2;

                    float scale = EngineApi.PClient->RandomFloat(1, 1.484f);
                    float* pTransform = (float*)transform;

                    // VectorScale(transform[axis], scale, transform[axis])
                    pTransform[axis * 4 + 0] *= scale;
                    pTransform[axis * 4 + 1] *= scale;
                    pTransform[axis * 4 + 2] *= scale;
                }
                else if (EngineApi.PClient->RandomLong(0, 49) == 0)
                {
                    float offset;
                    int axis = EngineApi.PClient->RandomLong(0, 1);
                    if (axis == 1) // Choose between x & z
                        axis = 2;
                    offset = EngineApi.PClient->RandomFloat(-10, 10);

                    float* pTransform = (float*)transform;
                    int randomAxis = EngineApi.PClient->RandomLong(0, 2);
                    pTransform[randomAxis * 4 + 3] += offset;
                }
                break;

            case RenderFx.kRenderFxExplode:
                {
                    float scale;

                    scale = 1.0f + (float)(m_clTime - ent->curstate.animtime) * 10.0f;
                    if (scale > 2) // Don't blow up more than 200%
                        scale = 2;

                    float* pTransform = (float*)transform;
                    pTransform[0 * 4 + 1] *= scale;
                    pTransform[1 * 4 + 1] *= scale;
                    pTransform[2 * 4 + 1] *= scale;
                }
                break;
        }
    }

    /// <summary>
    /// Set up model bone positions
    /// Original: void CStudioModelRenderer::StudioSetupBones()
    /// </summary>
    private void StudioSetupBones()
    {
        int i;
        double f;

        mstudiobone_t* pbones;
        mstudioseqdesc_t* pseqdesc;
        mstudioanim_t* panim;

        // Allocate bone data on stack
        Span<float> pos = stackalloc float[StudioConstants.MAXSTUDIOBONES * 3];
        Span<float> q = stackalloc float[StudioConstants.MAXSTUDIOBONES * 4];
        Span<float> bonematrix = stackalloc float[3 * 4];

        Span<float> pos2 = stackalloc float[StudioConstants.MAXSTUDIOBONES * 3];
        Span<float> q2 = stackalloc float[StudioConstants.MAXSTUDIOBONES * 4];
        Span<float> pos3 = stackalloc float[StudioConstants.MAXSTUDIOBONES * 3];
        Span<float> q3 = stackalloc float[StudioConstants.MAXSTUDIOBONES * 4];
        Span<float> pos4 = stackalloc float[StudioConstants.MAXSTUDIOBONES * 3];
        Span<float> q4 = stackalloc float[StudioConstants.MAXSTUDIOBONES * 4];

        if (m_pCurrentEntity->curstate.sequence >= m_pStudioHeader->numseq)
        {
            m_pCurrentEntity->curstate.sequence = 0;
        }

        pseqdesc = m_pStudioHeader->GetSequences() + m_pCurrentEntity->curstate.sequence;

        f = StudioEstimateFrame(pseqdesc);

        if (m_pCurrentEntity->latched.prevframe > f)
        {
            // Frame wrapped
        }

        panim = StudioGetAnim(m_pRenderModel, pseqdesc);

        fixed (float* pPos = pos, pQ = q, pPos2 = pos2, pQ2 = q2, pPos3 = pos3, pQ3 = q3, pPos4 = pos4, pQ4 = q4)
        {
            StudioCalcRotations(pPos, pQ, pseqdesc, panim, (float)f);

            if (pseqdesc->numblends > 1)
            {
                float s;
                float dadt;

                panim += m_pStudioHeader->numbones;
                StudioCalcRotations(pPos2, pQ2, pseqdesc, panim, (float)f);

                dadt = StudioEstimateInterpolant();
                s = (m_pCurrentEntity->curstate.blending[0] * dadt + m_pCurrentEntity->latched.prevblending[0] * (1.0f - dadt)) / 255.0f;

                StudioSlerpBones(pQ, pPos, pQ2, pPos2, s);

                if (pseqdesc->numblends == 4)
                {
                    panim += m_pStudioHeader->numbones;
                    StudioCalcRotations(pPos3, pQ3, pseqdesc, panim, (float)f);

                    panim += m_pStudioHeader->numbones;
                    StudioCalcRotations(pPos4, pQ4, pseqdesc, panim, (float)f);

                    s = (m_pCurrentEntity->curstate.blending[0] * dadt + m_pCurrentEntity->latched.prevblending[0] * (1.0f - dadt)) / 255.0f;
                    StudioSlerpBones(pQ3, pPos3, pQ4, pPos4, s);

                    s = (m_pCurrentEntity->curstate.blending[1] * dadt + m_pCurrentEntity->latched.prevblending[1] * (1.0f - dadt)) / 255.0f;
                    StudioSlerpBones(pQ, pPos, pQ3, pPos3, s);
                }
            }

            // Blend with previous sequence if interpolating
            if (m_fDoInterp &&
                m_pCurrentEntity->latched.sequencetime != 0 &&
                (m_pCurrentEntity->latched.sequencetime + 0.2 > m_clTime) &&
                (m_pCurrentEntity->latched.prevsequence < m_pStudioHeader->numseq))
            {
                // blend from last sequence
                Span<float> pos1b = stackalloc float[StudioConstants.MAXSTUDIOBONES * 3];
                Span<float> q1b = stackalloc float[StudioConstants.MAXSTUDIOBONES * 4];
                float s;

                if (m_pCurrentEntity->latched.prevsequence >= m_pStudioHeader->numseq)
                {
                    m_pCurrentEntity->latched.prevsequence = 0;
                }

                pseqdesc = m_pStudioHeader->GetSequences() + m_pCurrentEntity->latched.prevsequence;
                panim = StudioGetAnim(m_pRenderModel, pseqdesc);

                fixed (float* pPos1b = pos1b, pQ1b = q1b)
                {
                    // clip prevframe
                    StudioCalcRotations(pPos1b, pQ1b, pseqdesc, panim, m_pCurrentEntity->latched.prevframe);

                    if (pseqdesc->numblends > 1)
                    {
                        panim += m_pStudioHeader->numbones;
                        StudioCalcRotations(pPos2, pQ2, pseqdesc, panim, m_pCurrentEntity->latched.prevframe);

                        s = (m_pCurrentEntity->latched.prevseqblending[0]) / 255.0f;
                        StudioSlerpBones(pQ1b, pPos1b, pQ2, pPos2, s);

                        if (pseqdesc->numblends == 4)
                        {
                            panim += m_pStudioHeader->numbones;
                            StudioCalcRotations(pPos3, pQ3, pseqdesc, panim, m_pCurrentEntity->latched.prevframe);

                            panim += m_pStudioHeader->numbones;
                            StudioCalcRotations(pPos4, pQ4, pseqdesc, panim, m_pCurrentEntity->latched.prevframe);

                            s = (m_pCurrentEntity->latched.prevseqblending[0]) / 255.0f;
                            StudioSlerpBones(pQ3, pPos3, pQ4, pPos4, s);

                            s = (m_pCurrentEntity->latched.prevseqblending[1]) / 255.0f;
                            StudioSlerpBones(pQ1b, pPos1b, pQ3, pPos3, s);
                        }
                    }

                    s = 1.0f - (float)((m_clTime - m_pCurrentEntity->latched.sequencetime) / 0.2);
                    StudioSlerpBones(pQ, pPos, pQ1b, pPos1b, s);
                }
            }
            else
            {
                m_pCurrentEntity->latched.prevframe = (float)f;
            }

            // TODO: Implement gait animation blending (player-specific)
            // This will be added when we implement player rendering

            // Build final bone matrices
            pbones = m_pStudioHeader->GetBones();

            fixed (float* pBonematrix = bonematrix)
            {
                for (i = 0; i < m_pStudioHeader->numbones; i++)
                {
                    StudioMath.QuaternionMatrix(pQ + (i * 4), pBonematrix);

                    pBonematrix[0 * 4 + 3] = pPos[i * 3 + 0];
                    pBonematrix[1 * 4 + 3] = pPos[i * 3 + 1];
                    pBonematrix[2 * 4 + 3] = pPos[i * 3 + 2];

                    if (pbones[i].parent == -1)
                    {
                        if (IEngineStudio->IsHardware() != 0)
                        {
                            // StudioMath.ConcatTransforms(*m_protationmatrix, *(Matrix3x4*)pBonematrix, out m_pbonetransform[i]);
                            // using ref form
                            StudioMath.ConcatTransforms(ref *m_protationmatrix, ref *(Matrix3x4*)pBonematrix, out m_pbonetransform[i]);

                            // MatrixCopy should be faster...
                            //StudioMath.ConcatTransforms(*m_protationmatrix, *(Matrix3x4*)pBonematrix, out m_plighttransform[i]);
                            m_plighttransform[i] = m_pbonetransform[i];
                        }
                        else
                        {
                            //StudioMath.ConcatTransforms(*m_paliastransform, *(Matrix3x4*)pBonematrix, out m_pbonetransform[i]);
                            //StudioMath.ConcatTransforms(*m_protationmatrix, *(Matrix3x4*)pBonematrix, out m_plighttransform[i]);
                            // using ref form
                            StudioMath.ConcatTransforms(ref *m_paliastransform, ref *(Matrix3x4*)pBonematrix, out m_pbonetransform[i]);
                            StudioMath.ConcatTransforms(ref *m_protationmatrix, ref *(Matrix3x4*)pBonematrix, out m_plighttransform[i]);
                        }

                        // Apply client-side effects to the transformation matrix
                        StudioFxTransform(m_pCurrentEntity, m_pbonetransform + i);
                    }
                    else
                    {
                        //StudioMath.ConcatTransforms(m_pbonetransform[pbones[i].parent], *(Matrix3x4*)pBonematrix, out m_pbonetransform[i]);
                        //StudioMath.ConcatTransforms(m_plighttransform[pbones[i].parent], *(Matrix3x4*)pBonematrix, out m_plighttransform[i]);

                        // using ref form

                        StudioMath.ConcatTransforms(ref m_pbonetransform[pbones[i].parent], ref *(Matrix3x4*)pBonematrix, out m_pbonetransform[i]);
                        StudioMath.ConcatTransforms(ref m_plighttransform[pbones[i].parent], ref *(Matrix3x4*)pBonematrix, out m_plighttransform[i]);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Find final attachment points
    /// Original: void CStudioModelRenderer::StudioCalcAttachments()
    /// </summary>
    private void StudioCalcAttachments()
    {
        int i;
        mstudioattachment_t* pattachment;

        if (m_pStudioHeader->numattachments > 4)
        {
            // TODO: gEngfuncs.Con_DPrintf("Too many attachments on %s\n", m_pCurrentEntity->model->name);
            // exit(-1);
            return;
        }

        // Calculate attachment points
        pattachment = m_pStudioHeader->GetAttachments();
        for (i = 0; i < m_pStudioHeader->numattachments; i++)
        {
            // VectorTransform(pattachment[i].org, (*m_plighttransform)[pattachment[i].bone], m_pCurrentEntity->attachment[i])
            StudioMath.VectorTransform(ref pattachment[i].org, m_plighttransform + pattachment[i].bone,
                ref m_pCurrentEntity->attachment[i]);
        }
    }

    /// <summary>
    /// Save bone matrices and names
    /// Original: void CStudioModelRenderer::StudioSaveBones()
    /// </summary>
    private void StudioSaveBones()
    {
        int i;

        mstudiobone_t* pbones = m_pStudioHeader->GetBones();

        m_nCachedBones = m_pStudioHeader->numbones;

        for (i = 0; i < m_pStudioHeader->numbones; i++)
        {
            // Copy bone name
            for (int j = 0; j < 32; j++)
            {
                m_nCachedBoneNames[i, j] = pbones[i].name[j];
                if (pbones[i].name[j] == 0)
                    break;
            }

            // Copy matrices
            m_rgCachedBoneTransform[i] = m_pbonetransform[i];
            m_rgCachedLightTransform[i] = m_plighttransform[i];
        }
    }

    /// <summary>
    /// Merge cached bones with current bones for model
    /// Original: void CStudioModelRenderer::StudioMergeBones(model_t* m_pSubModel)
    /// </summary>
    private void StudioMergeBones(model_s* pSubModel)
    {
        int i, j;
        double f;

        mstudiobone_t* pbones;
        mstudioseqdesc_t* pseqdesc;
        mstudioanim_t* panim;

        Span<float> pos = stackalloc float[StudioConstants.MAXSTUDIOBONES * 3];
        Span<float> bonematrix = stackalloc float[3 * 4];
        Span<float> q = stackalloc float[StudioConstants.MAXSTUDIOBONES * 4];

        if (m_pCurrentEntity->curstate.sequence >= m_pStudioHeader->numseq)
        {
            m_pCurrentEntity->curstate.sequence = 0;
        }

        pseqdesc = m_pStudioHeader->GetSequences() + m_pCurrentEntity->curstate.sequence;

        f = StudioEstimateFrame(pseqdesc);

        if (m_pCurrentEntity->latched.prevframe > f)
        {
            // Frame wrapped
        }

        panim = StudioGetAnim(pSubModel, pseqdesc);

        fixed (float* pPos = pos, pQ = q, pBonematrix = bonematrix)
        {
            StudioCalcRotations(pPos, pQ, pseqdesc, panim, (float)f);

            pbones = m_pStudioHeader->GetBones();

            for (i = 0; i < m_pStudioHeader->numbones; i++)
            {
                // Try to find cached bone
                for (j = 0; j < m_nCachedBones; j++)
                {
                    // Compare bone names
                    bool match = true;
                    for (int k = 0; k < 32; k++)
                    {
                        if (pbones[i].name[k] != m_nCachedBoneNames[j, k])
                        {
                            if (pbones[i].name[k] == 0 && m_nCachedBoneNames[j, k] == 0)
                                break;
                            match = false;
                            break;
                        }
                        if (pbones[i].name[k] == 0)
                            break;
                    }

                    if (match)
                    {
                        // Use cached bone
                        m_pbonetransform[i] = m_rgCachedBoneTransform[j];
                        m_plighttransform[i] = m_rgCachedLightTransform[j];
                        break;
                    }
                }

                if (j >= m_nCachedBones)
                {
                    // Bone not cached, calculate it
                    StudioMath.QuaternionMatrix(pQ + (i * 4), pBonematrix);

                    pBonematrix[0 * 4 + 3] = pPos[i * 3 + 0];
                    pBonematrix[1 * 4 + 3] = pPos[i * 3 + 1];
                    pBonematrix[2 * 4 + 3] = pPos[i * 3 + 2];

                    if (pbones[i].parent == -1)
                    {
                        if (IEngineStudio->IsHardware() != 0)
                        {
                            StudioMath.ConcatTransforms(ref *m_protationmatrix, ref *(Matrix3x4*)pBonematrix, out m_pbonetransform[i]);
                            // MatrixCopy should be faster...
                            //StudioMath.ConcatTransforms(*m_protationmatrix, *(Matrix3x4*)pBonematrix, out m_plighttransform[i]);
                            m_plighttransform[i] = m_pbonetransform[i];
                        }
                        else
                        {
                            StudioMath.ConcatTransforms(ref *m_paliastransform, ref *(Matrix3x4*)pBonematrix, out m_pbonetransform[i]);
                            StudioMath.ConcatTransforms(ref *m_protationmatrix, ref *(Matrix3x4*)pBonematrix, out m_plighttransform[i]);
                        }
                        // Apply client-side effects to the transformation matrix
                        StudioFxTransform(m_pCurrentEntity, m_pbonetransform + i);
                    }
                    else
                    {
                        StudioMath.ConcatTransforms(ref m_pbonetransform[pbones[i].parent], ref *(Matrix3x4*)pBonematrix, out m_pbonetransform[i]);
                        StudioMath.ConcatTransforms(ref m_plighttransform[pbones[i].parent], ref *(Matrix3x4*)pBonematrix, out m_plighttransform[i]);
                    }
                }
            }
        }
    }

    #endregion

    #region Rendering Methods

    /// <summary>
    /// Render the model
    /// Original: void CStudioModelRenderer::StudioRenderModel()
    /// </summary>
    private void StudioRenderModel()
    {
        IEngineStudio->SetChromeOrigin();
        IEngineStudio->SetForceFaceFlags(0);

        if (m_pCurrentEntity->curstate.renderfx == (int)RenderFx.kRenderFxGlowShell)
        {
            m_pCurrentEntity->curstate.renderfx = (int)RenderFx.kRenderFxNone;
            StudioRenderFinal();

            if (IEngineStudio->IsHardware() == 0)
            {
                // TODO: Set render mode to kRenderTransAdd via TriAPI
            }

            IEngineStudio->SetForceFaceFlags((int)StudioNormalFlags.STUDIO_NF_CHROME);

            // TODO: Set sprite texture via TriAPI
            m_pCurrentEntity->curstate.renderfx = (int)RenderFx.kRenderFxGlowShell;

            StudioRenderFinal();

            if (IEngineStudio->IsHardware() == 0)
            {
                // TODO: Set render mode to kRenderNormal via TriAPI
            }
        }
        else
        {
            StudioRenderFinal();
        }
    }

    /// <summary>
    /// Finalize rendering
    /// Original: void CStudioModelRenderer::StudioRenderFinal()
    /// </summary>
    private void StudioRenderFinal()
    {
        if (IEngineStudio->IsHardware() != 0)
        {
            StudioRenderFinal_Hardware();
        }
        else
        {
            StudioRenderFinal_Software();
        }
    }

    /// <summary>
    /// Software renderer finishing function
    /// Original: void CStudioModelRenderer::StudioRenderFinal_Software()
    /// </summary>
    private void StudioRenderFinal_Software()
    {
        int i;

        // Note, rendermode set here has effect in SW
        IEngineStudio->SetupRenderer(0);

        if (m_pCvarDrawEntities->value == 2)
        {
            IEngineStudio->StudioDrawBones();
        }
        else if (m_pCvarDrawEntities->value == 3)
        {
            IEngineStudio->StudioDrawHulls();
        }
        else
        {
            for (i = 0; i < m_pStudioHeader->numbodyparts; i++)
            {
                nint pBodyPart = (nint)m_pBodyPart;
                nint pSubModel = (nint)m_pSubModel;
                IEngineStudio->StudioSetupModel(i, &pBodyPart, &pSubModel);
                m_pBodyPart = (mstudiobodyparts_t*)pBodyPart;
                m_pSubModel = (mstudiomodel_t*)pSubModel;

                IEngineStudio->StudioDrawPoints();
            }
        }

        if (m_pCvarDrawEntities->value == 4)
        {
            // TODO: gEngfuncs.pTriAPI->RenderMode(kRenderTransAdd);
            IEngineStudio->StudioDrawHulls();
            // TODO: gEngfuncs.pTriAPI->RenderMode(kRenderNormal);
        }

        if (m_pCvarDrawEntities->value == 5)
        {
            IEngineStudio->StudioDrawAbsBBox();
        }

        IEngineStudio->RestoreRenderer();
    }

    /// <summary>
    /// Hardware (OpenGL/D3D) renderer finishing function
    /// Original: void CStudioModelRenderer::StudioRenderFinal_Hardware()
    /// </summary>
    private void StudioRenderFinal_Hardware()
    {
        int i;
        int rendermode;

        rendermode = IEngineStudio->GetForceFaceFlags() != 0 ? (int)RenderMode.kRenderTransAdd : m_pCurrentEntity->curstate.rendermode;
        IEngineStudio->SetupRenderer(rendermode);

        if (m_pCvarDrawEntities->value == 2)
        {
            IEngineStudio->StudioDrawBones();
        }
        else if (m_pCvarDrawEntities->value == 3)
        {
            IEngineStudio->StudioDrawHulls();
        }
        else
        {
            for (i = 0; i < m_pStudioHeader->numbodyparts; i++)
            {
                nint pBodyPart = (nint)m_pBodyPart;
                nint pSubModel = (nint)m_pSubModel;
                IEngineStudio->StudioSetupModel(i, &pBodyPart, &pSubModel);
                m_pBodyPart = (mstudiobodyparts_t*)pBodyPart;
                m_pSubModel = (mstudiomodel_t*)pSubModel;

                if (m_fDoInterp)
                {
                    // Interpolation messes up bounding boxes.
                    m_pCurrentEntity->trivial_accept = 0;
                }

                IEngineStudio->GL_SetRenderMode(rendermode);
                IEngineStudio->StudioDrawPoints();
                IEngineStudio->GL_StudioDrawShadow();
            }
        }

        if (m_pCvarDrawEntities->value == 4)
        {
            EngineApi.PClient->pTriAPI->RenderMode((int)RenderMode.kRenderTransAdd);
            IEngineStudio->StudioDrawHulls();
            EngineApi.PClient->pTriAPI->RenderMode((int)RenderMode.kRenderNormal);
        }

        IEngineStudio->RestoreRenderer();
    }

    #endregion

    #region Static Export Functions

    /// <summary>
    /// Static wrapper for StudioDrawModel - called by engine
    /// Original: int R_StudioDrawModel(int flags)
    /// </summary>
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static int R_StudioDrawModel(int flags)
    {
        if (_instance == null)
            return 0;

        return _instance.StudioDrawModel(flags) ? 1 : 0;
    }

    /// <summary>
    /// Static wrapper for StudioDrawPlayer - called by engine
    /// Original: int R_StudioDrawPlayer(int flags, entity_state_s* pplayer)
    /// </summary>
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static int R_StudioDrawPlayer(int flags, entity_state_s* pplayer)
    {
        if (_instance == null)
            return 0;

        return _instance.StudioDrawPlayer(flags, pplayer) ? 1 : 0;
    }

    /// <summary>
    /// Get studio model interface - called by engine
    /// Original: int HUD_GetStudioModelInterface(int version, r_studio_interface_s** ppinterface, engine_studio_api_s* pstudio)
    /// </summary>
    public static int GetStudioModelInterface(int version, r_studio_interface_s** ppinterface, engine_studio_api_s* pstudio)
    {
        const int STUDIO_INTERFACE_VERSION = 1;

        if (version != STUDIO_INTERFACE_VERSION)
            return 0;

        // Store engine studio API
        IEngineStudio = pstudio;

        // Create our studio interface
        var studioInterface = (r_studio_interface_s*)Marshal.AllocHGlobal(sizeof(r_studio_interface_s));
        studioInterface->version = STUDIO_INTERFACE_VERSION;
        studioInterface->StudioDrawModel = &R_StudioDrawModel;
        studioInterface->StudioDrawPlayer = &R_StudioDrawPlayer;

        // Point the engine to our callbacks
        *ppinterface = studioInterface;

        // Create and initialize renderer instance
        _instance = new StudioModelRenderer();
        _instance.Init();

        return 1;
    }

    #endregion

    #region Player-specific Methods

    /// <summary>
    /// Determine pitch and blending amounts for players
    /// Original: void CStudioModelRenderer::StudioPlayerBlend(mstudioseqdesc_t* pseqdesc, int* pBlend, float* pPitch)
    /// </summary>
    private void StudioPlayerBlend(mstudioseqdesc_t* pseqdesc, int* pBlend, float* pPitch)
    {
        // Calc up/down pointing
        *pBlend = (int)(*pPitch * 3);

        if (*pBlend < pseqdesc->blendstart[0])
        {
            *pPitch -= pseqdesc->blendstart[0] / 3.0f;
            *pBlend = 0;
        }
        else if (*pBlend > pseqdesc->blendend[0])
        {
            *pPitch -= pseqdesc->blendend[0] / 3.0f;
            *pBlend = 255;
        }
        else
        {
            if (pseqdesc->blendend[0] - pseqdesc->blendstart[0] < 0.1f) // catch qc error
                *pBlend = 127;
            else
                *pBlend = (int)(255 * (*pBlend - pseqdesc->blendstart[0]) / (pseqdesc->blendend[0] - pseqdesc->blendstart[0]));
            *pPitch = 0;
        }
    }

    /// <summary>
    /// Estimate gait frame for player
    /// Original: void CStudioModelRenderer::StudioEstimateGait(entity_state_t* pplayer)
    /// </summary>
    private void StudioEstimateGait(entity_state_s* pplayer)
    {
        float dt;
        Vector3 est_velocity;

        dt = (float)(m_clTime - m_clOldTime);
        if (dt < 0)
            dt = 0;
        else if (dt > 1.0f)
            dt = 1;

        if (dt == 0 || m_pPlayerInfo->renderframe == m_nFrameCount)
        {
            m_flGaitMovement = 0;
            return;
        }

        if (m_fGaitEstimation)
        {
            est_velocity = m_pCurrentEntity->origin - m_pPlayerInfo->prevgaitorigin;
            m_pPlayerInfo->prevgaitorigin = m_pCurrentEntity->origin;
            m_flGaitMovement = est_velocity.Length();

            if (dt <= 0 || m_flGaitMovement / dt < 5)
            {
                m_flGaitMovement = 0;
                est_velocity.X = 0;
                est_velocity.Y = 0;
            }
        }
        else
        {
            est_velocity = pplayer->velocity;
            m_flGaitMovement = est_velocity.Length() * dt;
        }

        if (est_velocity.Y == 0 && est_velocity.X == 0)
        {
            float flYawDiff = m_pCurrentEntity->angles.Y - m_pPlayerInfo->gaityaw; // YAW
            flYawDiff = flYawDiff - (int)(flYawDiff / 360) * 360;
            if (flYawDiff > 180)
                flYawDiff -= 360;
            if (flYawDiff < -180)
                flYawDiff += 360;

            if (dt < 0.25f)
                flYawDiff *= dt * 4;
            else
                flYawDiff *= dt;

            m_pPlayerInfo->gaityaw += flYawDiff;
            m_pPlayerInfo->gaityaw = m_pPlayerInfo->gaityaw - (int)(m_pPlayerInfo->gaityaw / 360) * 360;

            m_flGaitMovement = 0;
        }
        else
        {
            m_pPlayerInfo->gaityaw = (float)(Math.Atan2(est_velocity.Y, est_velocity.X) * 180 / Math.PI);
            if (m_pPlayerInfo->gaityaw > 180)
                m_pPlayerInfo->gaityaw = 180;
            if (m_pPlayerInfo->gaityaw < -180)
                m_pPlayerInfo->gaityaw = -180;
        }
    }

    /// <summary>
    /// Process movement of player
    /// Original: void CStudioModelRenderer::StudioProcessGait(entity_state_t* pplayer)
    /// </summary>
    private void StudioProcessGait(entity_state_s* pplayer)
    {
        mstudioseqdesc_t* pseqdesc;
        float dt;
        int iBlend;
        float flYaw; // view direction relative to movement

        if (m_pCurrentEntity->curstate.sequence >= m_pStudioHeader->numseq)
        {
            m_pCurrentEntity->curstate.sequence = 0;
        }

        pseqdesc = m_pStudioHeader->GetSequences() + m_pCurrentEntity->curstate.sequence;

        float pitch = m_pCurrentEntity->angles.X; // PITCH
        StudioPlayerBlend(pseqdesc, &iBlend, &pitch);
        m_pCurrentEntity->angles.X = pitch;

        m_pCurrentEntity->latched.prevangles.X = m_pCurrentEntity->angles.X; // PITCH
        m_pCurrentEntity->curstate.blending[0] = (byte)iBlend;
        m_pCurrentEntity->latched.prevblending[0] = m_pCurrentEntity->curstate.blending[0];
        m_pCurrentEntity->latched.prevseqblending[0] = m_pCurrentEntity->curstate.blending[0];

        dt = (float)(m_clTime - m_clOldTime);
        if (dt < 0)
            dt = 0;
        else if (dt > 1.0f)
            dt = 1;

        StudioEstimateGait(pplayer);

        // Calc side to side turning
        flYaw = m_pCurrentEntity->angles.Y - m_pPlayerInfo->gaityaw; // YAW
        flYaw = flYaw - (int)(flYaw / 360) * 360;
        if (flYaw < -180)
            flYaw = flYaw + 360;
        if (flYaw > 180)
            flYaw = flYaw - 360;

        if (flYaw > 120)
        {
            m_pPlayerInfo->gaityaw = m_pPlayerInfo->gaityaw - 180;
            m_flGaitMovement = -m_flGaitMovement;
            flYaw = flYaw - 180;
        }
        else if (flYaw < -120)
        {
            m_pPlayerInfo->gaityaw = m_pPlayerInfo->gaityaw + 180;
            m_flGaitMovement = -m_flGaitMovement;
            flYaw = flYaw + 180;
        }

        // Adjust torso
        m_pCurrentEntity->curstate.controller[0] = (byte)((flYaw / 4.0f + 30) / (60.0f / 255.0f));
        m_pCurrentEntity->curstate.controller[1] = (byte)((flYaw / 4.0f + 30) / (60.0f / 255.0f));
        m_pCurrentEntity->curstate.controller[2] = (byte)((flYaw / 4.0f + 30) / (60.0f / 255.0f));
        m_pCurrentEntity->curstate.controller[3] = (byte)((flYaw / 4.0f + 30) / (60.0f / 255.0f));
        m_pCurrentEntity->latched.prevcontroller[0] = m_pCurrentEntity->curstate.controller[0];
        m_pCurrentEntity->latched.prevcontroller[1] = m_pCurrentEntity->curstate.controller[1];
        m_pCurrentEntity->latched.prevcontroller[2] = m_pCurrentEntity->curstate.controller[2];
        m_pCurrentEntity->latched.prevcontroller[3] = m_pCurrentEntity->curstate.controller[3];

        m_pCurrentEntity->angles.Y = m_pPlayerInfo->gaityaw; // YAW
        if (m_pCurrentEntity->angles.Y < 0)
            m_pCurrentEntity->angles.Y += 360;
        m_pCurrentEntity->latched.prevangles.Y = m_pCurrentEntity->angles.Y; // YAW

        if (pplayer->gaitsequence >= m_pStudioHeader->numseq)
        {
            pplayer->gaitsequence = 0;
        }

        pseqdesc = m_pStudioHeader->GetSequences() + pplayer->gaitsequence;

        // Calc gait frame
        if (pseqdesc->linearmovement.X > 0)
        {
            m_pPlayerInfo->gaitframe += (m_flGaitMovement / pseqdesc->linearmovement.X) * pseqdesc->numframes;
        }
        else
        {
            m_pPlayerInfo->gaitframe += pseqdesc->fps * dt;
        }

        // Do modulo
        m_pPlayerInfo->gaitframe = m_pPlayerInfo->gaitframe - (int)(m_pPlayerInfo->gaitframe / pseqdesc->numframes) * pseqdesc->numframes;
        if (m_pPlayerInfo->gaitframe < 0)
            m_pPlayerInfo->gaitframe += pseqdesc->numframes;
    }

    #endregion
}


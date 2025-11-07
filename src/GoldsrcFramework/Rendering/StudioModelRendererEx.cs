using GoldsrcFramework.Engine.Native;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GoldsrcFramework.Rendering
{
    /// <summary>
    /// Reimplmentaion of StudioModelRenderer.
    /// </summary>
    public unsafe class StudioModelRendererEx
    {
        private static engine_studio_api_s* pIEngineStudio;
        private static engine_studio_api_s IEngineStudio;

        public static mstudioanim_t* GetAnim(studiohdr_t* pHeader, model_s* m_pSubModel, mstudioseqdesc_t* pseqdesc)
        {
            mstudioseqgroup_t* pseqgroup = (mstudioseqgroup_t*)((byte*)pHeader + pHeader->seqgroupindex) + pseqdesc->seqgroup;

            if (pseqdesc->seqgroup == 0)
            {
                return (mstudioanim_t*)((byte*)pHeader + pseqdesc->animindex);
            }

            cache_user_s* paSequences = (cache_user_s*)m_pSubModel->submodels;

            if (paSequences == null)
            {
                paSequences = (cache_user_s*)IEngineStudio.Mem_Calloc(16, (uint)sizeof(cache_user_s)); // UNDONE: leak!
                m_pSubModel->submodels = (dmodel_t*)paSequences;
            }

            if (IEngineStudio.Cache_Check((cache_user_s*)&(paSequences[pseqdesc->seqgroup])) == 0)
            {
                // Con_Printf("loading %s\n", pseqgroup->name);
                IEngineStudio.LoadCacheFile(pseqgroup->name.AsPointer(), (cache_user_s*)&paSequences[pseqdesc->seqgroup]);
            }
            return (mstudioanim_t*)((byte*)paSequences[pseqdesc->seqgroup].data + pseqdesc->animindex);
        }

        /// <summary>
        /// Calculate bone controller adjustments
        /// Original: void CStudioModelRenderer::StudioCalcBoneAdj(float dadt, float* adj, const byte* pcontroller1, const byte* pcontroller2, byte mouthopen)
        /// </summary>
        public static void StudioCalcBoneAdj(studiohdr_t* pStudioHeader, float dadt, float* adj, byte* pcontroller1, byte* pcontroller2, byte mouthopen)
        {
            int i, j;
            float value;
            mstudiobonecontroller_t* pbonecontroller;

            pbonecontroller = (mstudiobonecontroller_t*)((byte*)pStudioHeader + pStudioHeader->bonecontrollerindex);

            for (j = 0; j < pStudioHeader->numbonecontrollers; j++)
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
                    // Con_DPrintf( "%d %d %f : %f\n", m_pCurrentEntity->curstate.controller[j], m_pCurrentEntity->latched.prevcontroller[j], value, dadt );
                }
                else
                {
                    value = mouthopen / 64.0f;
                    if (value > 1.0f)
                        value = 1.0f;
                    value = (1.0f - value) * pbonecontroller[j].start + value * pbonecontroller[j].end;
                    // Con_DPrintf("%d %f\n", mouthopen, value );
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
    }
}
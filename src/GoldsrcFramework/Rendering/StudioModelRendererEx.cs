using GoldsrcFramework.Engine.Native;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GoldsrcFramework.Rendering
{
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
    }
}

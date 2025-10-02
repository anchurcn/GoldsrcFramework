﻿using GoldsrcFramework.Engine.Native;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GoldsrcFramework
{
    public unsafe class EngineApi
    {
        public static cl_enginefunc_t* PClient { get; private set; }
        public static enginefuncs_s* PServer { get; private set; }

        internal static void ClientApiInit(cl_enginefunc_t* pEnginefuncs)
        {
            if (pEnginefuncs == null)
                throw new ArgumentNullException(nameof(pEnginefuncs));

            if (PClient != null)
                return; // Already initialized

            PClient = pEnginefuncs;
        }

        internal static void ServerApiInit(enginefuncs_s* pEnginefuncs)
        {
            if (pEnginefuncs == null)
                throw new ArgumentNullException(nameof(pEnginefuncs));
            if (PServer != null)
                return; // Already initialized
            PServer = pEnginefuncs;
        }
    }
}

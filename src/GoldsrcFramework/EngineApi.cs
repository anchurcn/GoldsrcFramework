using GoldsrcFramework.Engine.Native;
using NativeInterop;
using System;
using System.Text;

namespace GoldsrcFramework
{
    public unsafe class EngineApi
    {
        public static cl_enginefunc_t* PClient { get; private set; }
        public static enginefuncs_s* PServer { get; private set; }
        public static engine_studio_api_s* PStudio { get; private set; }

        public static void DrawStringCenter(string text)
        {
            Span<byte> msg = stackalloc byte[256];
            int _ = Encoding.UTF8.GetBytes(text, msg);
            fixed (byte* ptr = msg)
            {
                PClient->CenterPrint((NChar*)ptr);
            }
        }

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

        internal static void StudioApiInit(engine_studio_api_s* pstudio)
        {
            if (pstudio == null)
                throw new ArgumentNullException(nameof(pstudio));
            if (PStudio != null)
                return; // Already initialized
            PStudio = pstudio;
        }
    }
}

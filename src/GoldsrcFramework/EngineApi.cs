using GoldsrcFramework.Engine.Native;
using NativeInterop;
using System;
using System.Text;

namespace GoldsrcFramework
{
    public unsafe class EngineApi
    {
        public static ClientEngineFuncs* PClient { get; private set; }
        public static ServerEngineFuncs* PServer { get; private set; }
        public static globalvars_t* PGlobals { get; private set; }
        public static engine_studio_api_t* PStudio { get; private set; }

        public static void DrawStringCenter(string text)
        {
            Span<byte> msg = stackalloc byte[256];
            int _ = Encoding.UTF8.GetBytes(text, msg);
            fixed (byte* ptr = msg)
            {
                PClient->CenterPrint((NChar*)ptr);
            }
        }

        internal static void ClientApiInit(ClientEngineFuncs* pEnginefuncs)
        {
            if (pEnginefuncs == null)
                throw new ArgumentNullException(nameof(pEnginefuncs));

            if (PClient != null)
                return; // Already initialized

            PClient = pEnginefuncs;
        }

        internal static void ServerApiInit(ServerEngineFuncs* pEnginefuncs, globalvars_t* pGlobals)
        {
            if (pEnginefuncs == null)
                throw new ArgumentNullException(nameof(pEnginefuncs));
            if (pGlobals == null)
                throw new ArgumentNullException(nameof(pGlobals));
            if (PServer != null)
                return; // Already initialized
            PServer = pEnginefuncs;
            PGlobals = pGlobals;
        }

        internal static void StudioApiInit(engine_studio_api_t* pstudio)
        {
            if (pstudio == null)
                throw new ArgumentNullException(nameof(pstudio));
            if (PStudio != null)
                return; // Already initialized
            PStudio = pstudio;
        }
    }
}

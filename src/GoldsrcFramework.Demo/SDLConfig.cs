using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace GoldsrcFramework.Demo
{
    public static partial class SDLConfig
    {
        [LibraryImport("SDL2", EntryPoint = "SDL_SetHint")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool SetHint(
            [MarshalAs(UnmanagedType.LPStr)] string name,
            [MarshalAs(UnmanagedType.LPStr)] string value);

        public const string SDL_HINT_WINDOWS_DISABLE_THREAD_NAMING = "SDL_WINDOWS_DISABLE_THREAD_NAMING";
        [DllImport("SDL2.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int SDL_Init(uint flags);

        [DllImport("SDL2.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern void SDL_Quit();

        [DllImport("SDL2.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern IntPtr SDL_GetHint(string name);

        [DllImport("SDL2.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern int SDL_SetHint(string name, string value);

        private const uint SDL_INIT_VIDEO = 0x00000020;

        public static void Config()
        {

            var result = SDL_GetHint(SDL_HINT_WINDOWS_DISABLE_THREAD_NAMING);

            var s = Marshal.PtrToStringUTF8(result);

            System.Diagnostics.Debug.WriteLine("[DemoModStartup] SDL_HINT_WINDOWS_DISABLE_THREAD_NAMING setting is " + s);
        }
    }
}

using System.Runtime.InteropServices;

namespace GoldsrcFramework
{
    public static class FClass
    {
        const string LegacyClientDll = "libclient.dll";
        [DllImport(LegacyClientDll, CallingConvention = CallingConvention.Cdecl)]
        internal extern static void F(IntPtr pv);
    }
}

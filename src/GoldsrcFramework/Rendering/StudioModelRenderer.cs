using GoldsrcFramework.Engine.Native;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace GoldsrcFramework.Rendering;

public unsafe class StudioModelRenderer
{
    private static r_studio_interface_s* _studioInterface;
    private static StudioModelRenderer? _instance;

    internal static int GetStudioModelInterface(int version, r_studio_interface_s** ppinterface, engine_studio_api_s* pstudio)
    {
        throw new NotImplementedException();
    }
}


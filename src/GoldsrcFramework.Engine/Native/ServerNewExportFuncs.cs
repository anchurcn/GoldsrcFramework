using System;
using System.Runtime.InteropServices;

namespace GoldsrcFramework.Engine.Native;

/// <summary>
/// Server new export functions structure that matches NEW_DLL_FUNCTIONS layout
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public unsafe struct ServerNewExportFuncs
{
    /// <summary>
    /// Called right before the object's memory is freed. Calls its destructor.
    /// </summary>
    public delegate* unmanaged[Cdecl]<edict_t*, void> OnFreeEntPrivateData;

    /// <summary>
    /// Game shutdown function
    /// </summary>
    public delegate* unmanaged[Cdecl]<void> GameShutdown;

    /// <summary>
    /// Should two entities collide?
    /// </summary>
    public delegate* unmanaged[Cdecl]<edict_t*, edict_t*, int> ShouldCollide;

    /// <summary>
    /// Handle cvar value response
    /// </summary>
    public delegate* unmanaged[Cdecl]<edict_t*, sbyte*, void> CvarValue;

    /// <summary>
    /// Handle cvar value response with request ID
    /// </summary>
    public delegate* unmanaged[Cdecl]<edict_t*, int, sbyte*, sbyte*, void> CvarValue2;
}

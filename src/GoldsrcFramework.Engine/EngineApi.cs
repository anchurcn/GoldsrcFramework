using System.Runtime.InteropServices;

namespace GoldsrcFramework.Engine
{
    public struct HSPRITE
    {
        public int Value;
    }
    public struct Rect
    {
        public int left;
        public int top;
        public int right;
        public int bottom;
    }

    // client_sprite_s
    public struct client_sprite_s
    {
    }
    public struct SCREENINFO_s
    {
    }
    public struct cvar_s
    {
    }

    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct EngineApi
    {
        public delegate* unmanaged[Cdecl]<sbyte*, HSPRITE> pfnSPR_Load;
        public delegate* unmanaged[Cdecl]<HSPRITE, int> pfnSPR_Frames;
        public delegate* unmanaged[Cdecl]<HSPRITE, int, int> pfnSPR_Height;
        public delegate* unmanaged[Cdecl]<HSPRITE, int, int> pfnSPR_Width;
        public delegate* unmanaged[Cdecl]<HSPRITE, int, int, int, void> pfnSPR_Set;
        public delegate* unmanaged[Cdecl]<int, int, int, Rect*, void> pfnSPR_Draw;
        public delegate* unmanaged[Cdecl]<int, int, int, Rect*, void> pfnSPR_DrawHoles;
        public delegate* unmanaged[Cdecl]<int, int, int, Rect*, void> pfnSPR_DrawAdditive;
        public delegate* unmanaged[Cdecl]<int, int, int, int, void> pfnSPR_EnableScissor;
        public delegate* unmanaged[Cdecl]<void> pfnSPR_DisableScissor;
        public delegate* unmanaged[Cdecl]<sbyte*, int*, client_sprite_s*> pfnSPR_GetList;
        public delegate* unmanaged[Cdecl]<int, int, int, int, int, int, int, void> pfnFillRGBA;
        public delegate* unmanaged[Cdecl]<SCREENINFO_s*, int> pfnGetScreenInfo;
        public delegate* unmanaged[Cdecl]<HSPRITE, Rect, int, int, int, void> pfnSetCrosshair;
        public delegate* unmanaged[Cdecl]<sbyte*, sbyte*, int, cvar_s*> pfnRegisterVariable;
        public delegate* unmanaged[Cdecl]<sbyte*, float> pfnGetCvarFloat;
        public delegate* unmanaged[Cdecl]<sbyte*, IntPtr> pfnGetCvarString;
        public delegate* unmanaged[Cdecl]<sbyte*, delegate* unmanaged[Cdecl]<void>, int> pfnAddCommand;
        public delegate* unmanaged[Cdecl]<sbyte*, delegate* unmanaged[Cdecl]<sbyte*, int, void*, int>, int> pfnHookUserMsg;
        public delegate* unmanaged[Cdecl]<sbyte*, int> pfnServerCmd;
        public delegate* unmanaged[Cdecl]<sbyte*, int> pfnClientCmd;
        public IntPtr pfnGetPlayerInfo;
        public IntPtr pfnPlaySoundByName;
        public IntPtr pfnPlaySoundByIndex;
        public IntPtr pfnAngleVectors;
        public IntPtr pfnTextMessageGet;
        public IntPtr pfnDrawCharacter;
        public IntPtr pfnDrawConsoleString;
        public IntPtr pfnDrawSetTextColor;
        public IntPtr pfnDrawConsoleStringLen;
        public delegate* unmanaged[Cdecl]</*In*/ sbyte*, void> pfnConsolePrint;
        public IntPtr pfnCenterPrint;
        public delegate* unmanaged[Cdecl]<int> GetWindowCenterX;
        public delegate* unmanaged[Cdecl]<int> GetWindowCenterY;
        public IntPtr GetViewAngles;
        public IntPtr SetViewAngles;
        public IntPtr GetMaxClients;
        public IntPtr Cvar_SetValue;
        public IntPtr Cmd_Argc;
        public IntPtr Cmd_Argv;
        public IntPtr Con_Printf;
        public IntPtr Con_DPrintf;
        public IntPtr Con_NPrintf;
        public IntPtr Con_NXPrintf;
        public IntPtr PhysInfo_ValueForKey;
        public IntPtr ServerInfo_ValueForKey;
        public IntPtr GetClientMaxspeed;
        public IntPtr CheckParm;
        public IntPtr Key_Event;
        public IntPtr GetMousePosition;
        public IntPtr IsNoClipping;
        public IntPtr GetLocalPlayer;
        public IntPtr GetViewModel;
        public IntPtr GetEntityByIndex;
        public IntPtr GetClientTime;
        public IntPtr V_CalcShake;
        public IntPtr V_ApplyShake;
        public IntPtr PM_PointContents;
        public IntPtr PM_WaterEntity;
        public IntPtr PM_TraceLine;
        public IntPtr CL_LoadModel;
        public IntPtr CL_CreateVisibleEntity;
        public IntPtr GetSpritePointer;
        public IntPtr pfnPlaySoundByNameAtLocation;
        public IntPtr pfnPrecacheEvent;
        public IntPtr pfnPlaybackEvent;
        public IntPtr pfnWeaponAnim;
        public IntPtr pfnRandomFloat;
        public IntPtr pfnRandomLong;
        public IntPtr pfnHookEvent;
        public IntPtr Con_IsVisible;
        public delegate* unmanaged[Cdecl]<sbyte*> pfnGetGameDirectory;
        public IntPtr pfnGetCvarPointer;
        public IntPtr Key_LookupBinding;
        public IntPtr pfnGetLevelName;
        public IntPtr pfnGetScreenFade;
        public IntPtr pfnSetScreenFade;
        public IntPtr VGui_GetPanel;
        public IntPtr VGui_ViewportPaintBackground;
        public IntPtr COM_LoadFile;
        public IntPtr COM_ParseFile;
        public IntPtr COM_FreeFile;
        public IntPtr triangleapi_s;
        public IntPtr efx_api_s;
        public IntPtr event_api_s;
        public IntPtr demo_api_s;
        public IntPtr net_api_s;
        public IntPtr IVoiceTweak_s;
        public IntPtr IsSpectateOnly;
        public IntPtr LoadMapSprite;
        public IntPtr COM_AddAppDirectoryToSearchPath;
        public IntPtr COM_ExpandFilename;
        public IntPtr PlayerInfo_ValueForKey;
        public IntPtr PlayerInfo_SetValueForKey;
        public IntPtr GetPlayerUniqueID;
        public IntPtr GetTrackerIDForPlayer;
        public IntPtr GetPlayerForTrackerID;
        public IntPtr pfnServerCmdUnreliable;
        public IntPtr pfnGetMousePos;
        public IntPtr pfnSetMousePos;
        public IntPtr pfnSetMouseEnable;
        public IntPtr GetFirstCvarPtr;
        public IntPtr GetFirstCmdFunctionHandle;
        public IntPtr GetNextCmdFunctionHandle;
        public IntPtr GetCmdFunctionName;
        public IntPtr hudGetClientOldTime;
        public IntPtr hudGetServerGravityValue;
        public IntPtr hudGetModelByIndex;
        public IntPtr pfnSetFilterMode;
        public IntPtr pfnSetFilterColor;
        public IntPtr pfnSetFilterBrightness;
        public IntPtr pfnSequenceGet;
        public IntPtr pfnSPR_DrawGeneric;
        public IntPtr pfnSequencePickSentence;
        public IntPtr pfnDrawString;
        public IntPtr pfnDrawStringReverse;
        public IntPtr LocalPlayerInfo_ValueForKey;
        public IntPtr pfnVGUI2DrawCharacter;
        public IntPtr pfnVGUI2DrawCharacterAdd;
        public IntPtr COM_GetApproxWavePlayLength;
        public IntPtr pfnGetCareerUI;
        public IntPtr Cvar_Set;
        public IntPtr pfnIsCareerMatch;
        public IntPtr pfnPlaySoundVoiceByName;
        public IntPtr pfnPrimeMusicStream;
        public IntPtr GetAbsoluteTime;
        public IntPtr pfnProcessTutorMessageDecayBuffer;
        public IntPtr pfnConstructTutorMessageDecayBuffer;
        public IntPtr pfnResetTutorMessageDecayData;
        public IntPtr pfnPlaySoundByNameAtPitch;
        public IntPtr pfnFillRGBABlend;
        public delegate* unmanaged[Cdecl]<int> pfnGetAppID;
        public IntPtr pfnGetAliasList;
        public IntPtr pfnVguiWrap2_GetMouseDelta;
        public IntPtr pfnFilteredClientCmd;
    }
}

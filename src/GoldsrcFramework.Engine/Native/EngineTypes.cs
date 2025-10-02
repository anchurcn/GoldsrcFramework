using System;
using System.Runtime.InteropServices;
using GoldsrcFramework.LinearMath;

namespace GoldsrcFramework.Engine.Native
{
    // Constants
    public static class EngineConstants
    {
        public const int MAX_PHYSINFO_STRING = 256;
        public const int MAX_ENT_LEAFS = 48;
        public const int HISTORY_MAX = 64;
        public const int HISTORY_MASK = HISTORY_MAX - 1;
        public const int MAX_PHYSENTS = 600;
        public const int MAX_MOVEENTS = 64;
        public const int MAX_CLIP_PLANES = 5;
    }

    // Primitive type wrappers for typedefs
    [StructLayout(LayoutKind.Sequential)]
    public struct HSPRITE { public int Value; }

    [StructLayout(LayoutKind.Sequential)]
    public struct qboolean { public int Value; }


    // Color types
    // Original: typedef struct { byte r, g, b; } color24; from const.h
    [StructLayout(LayoutKind.Sequential)]
    public struct color24
    {
        public byte r;
        public byte g;
        public byte b;
    }

    // Original: typedef struct { unsigned r, g, b, a; } colorVec; from const.h
    [StructLayout(LayoutKind.Sequential)]
    public struct colorVec
    {
        public uint r;
        public uint g;
        public uint b;
        public uint a;
    }

    // Original: typedef struct { unsigned short r, g, b, a; } PackedColorVec; from const.h
    [StructLayout(LayoutKind.Sequential)]
    public struct PackedColorVec
    {
        public ushort r;
        public ushort g;
        public ushort b;
        public ushort a;
    }

    // Rectangle type
    [StructLayout(LayoutKind.Sequential)]
    public struct Rect
    {
        public int left;
        public int top;
        public int right;
        public int bottom;
    }

    // Network address type
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct netadr_s
    {
        public netadrtype_t type;
        public fixed byte ip[4];
        public fixed byte ipx[10];
        public ushort port;
    }

    public enum netadrtype_t
    {
        NA_UNUSED,
        NA_LOOPBACK,
        NA_BROADCAST,
        NA_IP,
        NA_IPX,
        NA_BROADCAST_IPX,
    }

    // Key button structure
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct kbutton_s
    {
        public fixed int down[2]; // key nums holding it down
        public int state;         // low bit is down state
    }

    // Studio event structure
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct mstudioevent_s
    {
        public int frame;
        public int @event;
        public int type;
        public fixed sbyte options[64];
    }

    // User command structure
    [StructLayout(LayoutKind.Sequential)]
    public struct usercmd_s
    {
        public short lerp_msec;   // Interpolation time on client
        public byte msec;         // Duration in ms of command
        public Vector3 viewangles; // Command view angles.

        // intended velocities
        public float forwardmove;   // Forward velocity.
        public float sidemove;      // Sideways velocity.
        public float upmove;        // Upward velocity.
        public byte lightlevel;     // Light level at spot where we are standing.
        public ushort buttons;      // Attack buttons
        public byte impulse;        // Impulse command issued.
        public byte weaponselect;   // Current weapon id

        // Experimental player impact stuff.
        public int impact_index;
        public Vector3 impact_position;
    }

    // Client data structure (simple version from cdll_int.h)
    [StructLayout(LayoutKind.Sequential)]
    public struct client_data_t
    {
        // fields that cannot be modified  (ie. have no effect if changed)
        public Vector3 origin;

        // fields that can be changed by the cldll
        public Vector3 viewangles;
        public int iWeaponBits;
        public float fov; // field of view
    }

    // Weapon data structure
    [StructLayout(LayoutKind.Sequential)]
    public struct weapon_data_s
    {
        public int m_iId;
        public int m_iClip;

        public float m_flNextPrimaryAttack;
        public float m_flNextSecondaryAttack;
        public float m_flTimeWeaponIdle;

        public int m_fInReload;
        public int m_fInSpecialReload;
        public float m_flNextReload;
        public float m_flPumpTime;
        public float m_fReloadTime;

        public float m_fAimedDamage;
        public float m_fNextAimBonus;
        public int m_fInZoom;
        public int m_iWeaponState;

        public int iuser1;
        public int iuser2;
        public int iuser3;
        public int iuser4;
        public float fuser1;
        public float fuser2;
        public float fuser3;
        public float fuser4;
    }

    // Client data structure (extended version from entity_state.h)
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct clientdata_s
    {
        public Vector3 origin;
        public Vector3 velocity;

        public int viewmodel;
        public Vector3 punchangle;
        public int flags;
        public int waterlevel;
        public int watertype;
        public Vector3 view_ofs;
        public float health;

        public int bInDuck;

        public int weapons; // remove?

        public int flTimeStepSound;
        public int flDuckTime;
        public int flSwimTime;
        public int waterjumptime;

        public float maxspeed;

        public float fov;
        public int weaponanim;

        public int m_iId;
        public int ammo_shells;
        public int ammo_nails;
        public int ammo_cells;
        public int ammo_rockets;
        public float m_flNextAttack;

        public int tfstate;

        public int pushmsec;

        public int deadflag;

        public fixed sbyte physinfo[EngineConstants.MAX_PHYSINFO_STRING];

        // For mods
        public int iuser1;
        public int iuser2;
        public int iuser3;
        public int iuser4;
        public float fuser1;
        public float fuser2;
        public float fuser3;
        public float fuser4;
        public Vector3 vuser1;
        public Vector3 vuser2;
        public Vector3 vuser3;
        public Vector3 vuser4;
    }

    // Entity state structure
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct entity_state_s
    {
        // Fields which are filled in by routines outside of delta compression
        public int entityType;
        // Index into cl_entities array for this entity.
        public int number;
        public float msg_time;

        // Message number last time the player/entity state was updated.
        public int messagenum;

        // Fields which can be transitted and reconstructed over the network stream
        public Vector3 origin;
        public Vector3 angles;

        public int modelindex;
        public int sequence;
        public float frame;
        public int colormap;
        public short skin;
        public short solid;
        public int effects;
        public float scale;

        public byte eflags;

        // Render information
        public int rendermode;
        public int renderamt;
        public color24 rendercolor;
        public int renderfx;

        public int movetype;
        public float animtime;
        public float framerate;
        public int body;
        public fixed byte controller[4];
        public fixed byte blending[4];
        public Vector3 velocity;

        // Send bbox down to client for use during prediction.
        public Vector3 mins;
        public Vector3 maxs;

        public int aiment;
        // If owned by a player, the index of that player ( for projectiles ).
        public int owner;

        // Friction, for prediction.
        public float friction;
        // Gravity multiplier
        public float gravity;

        // PLAYER SPECIFIC
        public int team;
        public int playerclass;
        public int health;
        public qboolean spectator;
        public int weaponmodel;
        public int gaitsequence;
        // If standing on conveyor, e.g.
        public Vector3 basevelocity;
        // Use the crouched hull, or the regular player hull.
        public int usehull;
        // Latched buttons last time state updated.
        public int oldbuttons;
        // -1 = in air, else pmove entity number
        public int onground;
        public int iStepLeft;
        // How fast we are falling
        public float flFallVelocity;

        public float fov;
        public int weaponanim;

        // For mods
        public int iuser1;
        public int iuser2;
        public int iuser3;
        public int iuser4;
        public float fuser1;
        public float fuser2;
        public float fuser3;
        public float fuser4;
        public Vector3 vuser1;
        public Vector3 vuser2;
        public Vector3 vuser3;
        public Vector3 vuser4;
    }

    // Local state structure
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct local_state_s
    {
        public entity_state_s playerstate;
        public clientdata_s client;
        public fixed byte weapondata[64 * 188]; // weapon_data_s weapondata[64]; // sizeof(weapon_data_s) = 188
    }

    // Reference parameters structure
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct ref_params_s
    {
        // Output
        public fixed float vieworg[3];
        public fixed float viewangles[3];

        public fixed float forward[3];
        public fixed float right[3];
        public fixed float up[3];

        // Client frametime;
        public float frametime;
        // Client time
        public float time;

        // Misc
        public int intermission;
        public int paused;
        public int spectator;
        public int onground;
        public int waterlevel;

        public fixed float simvel[3];
        public fixed float simorg[3];

        public fixed float viewheight[3];
        public float idealpitch;

        public fixed float cl_viewangles[3];

        public int health;
        public fixed float crosshairangle[3];
        public float viewsize;

        public fixed float punchangle[3];
        public int maxclients;
        public int viewentity;
        public int playernum;
        public int max_entities;
        public int demoplayback;
        public int hardware;

        public int smoothing;

        // Last issued usercmd
        public usercmd_s* cmd;

        // Movevars
        public movevars_s* movevars;

        public fixed int viewport[4]; // the viewport coordinates x ,y , width, height
    }

    // Move variables structure
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct movevars_s
    {
        public float gravity;           // Gravity for map
        public float stopspeed;         // Deceleration when not moving
        public float maxspeed;          // Max allowed speed
        public float spectatormaxspeed;
        public float accelerate;        // Acceleration factor
        public float airaccelerate;     // Same for when in open air
        public float wateraccelerate;   // Same for when in water
        public float friction;
        public float edgefriction;      // Extra friction near dropofs
        public float waterfriction;     // Less in water
        public float entgravity;        // 1.0
        public float bounce;            // Wall bounce value. 1.0
        public float stepsize;          // sv_stepsize;
        public float maxvelocity;       // maximum server velocity.
        public float zmax;              // Max z-buffer range (for GL)
        public float waveHeight;        // Water wave height (for GL)
        public qboolean footsteps;      // Play footstep sounds
        public float rollangle;
        public float rollspeed;
        public float skycolor_r;        // Sky color
        public float skycolor_g;        //
        public float skycolor_b;        //
        public float skyvec_x;          // Sky vector
        public float skyvec_y;          //
        public float skyvec_z;          //
        public fixed sbyte skyName[32]; // Name of the sky map
    }

    // Physics entity structure
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct physent_t
    {
        public fixed sbyte name[32];    // Name of model, or "player" or "world".
        public int player;
        public Vector3 origin;         // Model's origin in world coordinates.
        public nint model;            // only for bsp models
        public nint studiomodel;      // SOLID_BBOX, but studio clip intersections.
        public Vector3 mins, maxs;     // only for non-bsp models
        public int info;                // For client or server to use to identify (index into edicts or cl_entities)
        public Vector3 angles;         // rotated entities need this info for hull testing to work.

        public int solid;               // Triggers and func_door type WATER brushes are SOLID_NOT
        public int skin;                // BSP Contents for such things like fun_door water brushes.
        public int rendermode;          // So we can ignore glass

        // Complex collision detection.
        public float frame;
        public int sequence;
        public fixed byte controller[4];
        public fixed byte blending[2];

        public int movetype;
        public int takedamage;
        public int blooddecal;
        public int team;
        public int classnumber;

        // For mods
        public int iuser1;
        public int iuser2;
        public int iuser3;
        public int iuser4;
        public float fuser1;
        public float fuser2;
        public float fuser3;
        public float fuser4;
        public Vector3 vuser1;
        public Vector3 vuser2;
        public Vector3 vuser3;
        public Vector3 vuser4;
    }

    // PM trace structure
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct pmtrace_s
    {
        public qboolean allsolid;       // if true, plane is not valid
        public qboolean startsolid;     // if true, the initial point was in a solid area
        public qboolean inopen, inwater;
        public float fraction;          // time completed, 1.0 = didn't hit anything
        public Vector3 endpos;         // final position
        public pmplane_t plane;         // surface normal at impact
        public int ent;                 // entity the surface is on
        public Vector3 deltavelocity;  // Change in player's velocity caused by impact.
                                        // Only run on server.
        public int hitgroup;
    }

    // PM plane structure
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct pmplane_t
    {
        public Vector3 normal;
        public float dist;
        public byte type;           // for fast side tests
        public byte signbits;       // signx + (signy<<1) + (signz<<1)
        public fixed byte pad[2];
    }

    // Player move structure
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct playermove_s
    {
        public int player_index;    // So we don't try to run the PM_CheckStuck nudging too quickly.
        public qboolean server;     // For debugging, are we running physics code on server side?

        public qboolean multiplayer; // 1 == multiplayer server
        public float time;           // realtime on host, for reckoning duck timing
        public float frametime;      // Duration of this frame

        public Vector3 forward, right, up; // Vectors for angles
        // player state
        public Vector3 origin;      // Movement origin.
        public Vector3 angles;      // Movement view angles.
        public Vector3 oldangles;   // Angles before movement view angles were looked at.
        public Vector3 velocity;    // Current movement direction.
        public Vector3 movedir;     // For waterjumping, a forced forward velocity so we can fly over lip of ledge.
        public Vector3 basevelocity; // Velocity of the conveyor we are standing, e.g.

        // For ducking/dead
        public Vector3 view_ofs;    // Our eye position.
        public float flDuckTime;     // Time we started duck
        public qboolean bInDuck;     // In process of ducking or ducked already?

        // For walking/falling
        public int flTimeStepSound;  // Next time we can play a step sound
        public int iStepLeft;

        public float flFallVelocity;
        public Vector3 punchangle;

        public float flSwimTime;

        public float flNextPrimaryAttack;

        public int effects;          // MUZZLE FLASH, e.g.

        public int flags;            // FL_ONGROUND, FL_DUCKING, etc.
        public int usehull;          // 0 = regular player hull, 1 = ducked player hull, 2 = point hull
        public float gravity;        // Our current gravity and friction.
        public float friction;
        public int oldbuttons;       // Buttons last usercmd
        public float waterjumptime;  // Amount of time left in jumping out of water cycle.
        public qboolean dead;        // Are we a dead player?
        public int deadflag;
        public int spectator;        // Should we use spectator physics model?
        public int movetype;         // Our movement type, NOCLIP, WALK, FLY

        public int onground;
        public int waterlevel;
        public int watertype;
        public int oldwaterlevel;

        public fixed sbyte sztexturename[256];
        public sbyte chtexturetype;

        public float maxspeed;
        public float clientmaxspeed; // Player specific maxspeed

        // For mods
        public int iuser1;
        public int iuser2;
        public int iuser3;
        public int iuser4;
        public float fuser1;
        public float fuser2;
        public float fuser3;
        public float fuser4;
        public Vector3 vuser1;
        public Vector3 vuser2;
        public Vector3 vuser3;
        public Vector3 vuser4;
        // world state
        // Number of entities to clip against.
        public int numphysent;
        public fixed byte physents[EngineConstants.MAX_PHYSENTS * 256]; // physent_t physents[MAX_PHYSENTS]; // sizeof(physent_t) = 256
        // Number of momvement entities (ladders)
        public int nummoveent;
        // just a list of ladders
        public fixed byte moveents[EngineConstants.MAX_MOVEENTS * 256]; // physent_t moveents[MAX_MOVEENTS]; // sizeof(physent_t) = 256

        // All things being rendered, for tracing against things you don't actually collide with
        public int numvisent;
        public fixed byte visents[EngineConstants.MAX_PHYSENTS * 256]; // physent_t visents[MAX_PHYSENTS]; // sizeof(physent_t) = 256

        // input to run through physics.
        public usercmd_s cmd;

        // Trace results for objects we collided with.
        public int numtouch;
        public fixed byte touchindex[EngineConstants.MAX_PHYSENTS * 64]; // pmtrace_s touchindex[MAX_PHYSENTS]; // sizeof(pmtrace_s) = 64

        public fixed sbyte physinfo[EngineConstants.MAX_PHYSINFO_STRING]; // Physics info string

        public movevars_s* movevars;
        public fixed float player_mins[4 * 3]; // 4 hulls, 3 coords each
        public fixed float player_maxs[4 * 3]; // 4 hulls, 3 coords each

        // Common functions
        public delegate* unmanaged[Cdecl]<sbyte*, sbyte> PM_Info_ValueForKey;
        public delegate* unmanaged[Cdecl]<float*, float*, int, pmtrace_s*, void> PM_Particle;
        public delegate* unmanaged[Cdecl]<float*, float*, int, pmtrace_s*, void> PM_TestPlayerPosition;
        public delegate* unmanaged[Cdecl]<int, void> Con_NPrintf;
        public delegate* unmanaged[Cdecl]<sbyte*, void> Con_DPrintf;
        public delegate* unmanaged[Cdecl]<sbyte*, void> Con_Printf;
        public delegate* unmanaged[Cdecl]<float*, float*, int, pmtrace_s*, void> PM_Trace;
        public delegate* unmanaged[Cdecl]<float*, float*, int, pmtrace_s*, void> PM_TraceLine;
        public delegate* unmanaged[Cdecl]<int, float> RandomLong;
        public delegate* unmanaged[Cdecl]<float, float, float> RandomFloat;
        public delegate* unmanaged[Cdecl]<float*, int> PM_GetModelType;
        public delegate* unmanaged[Cdecl]<int, void> PM_GetModelBounds;
        public delegate* unmanaged[Cdecl]<int, nint> PM_HullForBsp;
        public delegate* unmanaged[Cdecl]<float*, int> PM_PointContents;
        public delegate* unmanaged[Cdecl]<float*, int*, int> PM_TruePointContents;
        public delegate* unmanaged[Cdecl]<float*, int> PM_HullPointContents;
        public delegate* unmanaged[Cdecl]<float*, float*, pmtrace_s*, void> PM_PlayerTrace;
        public delegate* unmanaged[Cdecl]<float*, float*, int, int, pmtrace_s*, void> PM_TraceToss;
        public delegate* unmanaged[Cdecl]<physent_t*, int> PM_FindTextureType;

        // Add these for the new functions
        public delegate* unmanaged[Cdecl]<float*, float*, int, delegate* unmanaged[Cdecl]<physent_t*, int>, pmtrace_s> PM_PlayerTraceEx;
        public delegate* unmanaged[Cdecl]<float*, pmtrace_s*, delegate* unmanaged[Cdecl]<physent_t*, int>, int> PM_TestPlayerPositionEx;
        public delegate* unmanaged[Cdecl]<float*, float*, int, int, delegate* unmanaged[Cdecl]<physent_t*, int>, pmtrace_s*> PM_TraceLineEx;
    }

    // Mouth structure for lip sync
    [StructLayout(LayoutKind.Sequential)]
    public struct mouth_t
    {
        public byte mouthopen; // 0 = mouth closed, 255 = mouth agape
        public byte sndcount;  // counter for running average
        public int sndavg;     // running average
    }

    // Latched variables structure
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct latchedvars_t
    {
        public float prevanimtime;
        public float sequencetime;
        public fixed byte prevseqblending[2];
        public Vector3 prevorigin;
        public Vector3 prevangles;

        public int prevsequence;
        public float prevframe;

        public fixed byte prevcontroller[4];
        public fixed byte prevblending[2];
    }

    // Position history structure
    [StructLayout(LayoutKind.Sequential)]
    public struct position_history_t
    {
        // Time stamp for this movement
        public float animtime;

        public Vector3 origin;
        public Vector3 angles;
    }

    // Client entity structure
    // Original: struct cl_entity_s from cl_entity.h
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct cl_entity_s
    {
        public int index; // Index into cl_entities ( should match actual slot, but not necessarily )

        public qboolean player; // True if this entity is a "player"

        public entity_state_s baseline;  // The original state from which to delta during an uncompressed message
        public entity_state_s prevstate; // The state information from the penultimate message received from the server
        public entity_state_s curstate;  // The state information from the last message received from server

        public int current_position;                                // Last received history update index
        public NativeInterop.FixedBuffer<position_history_t, _HistoryMax> ph; // History of position and angle updates for this player

        public mouth_t mouth; // For synchronizing mouth movements.

        public latchedvars_t latched; // Variables used by studio model rendering routines

        // Information based on interplocation, extrapolation, prediction, or just copied from last msg received.
        public float lastmove;

        // Actual render position and angles
        public Vector3 origin;
        public Vector3 angles;

        // Attachment points
        public NativeInterop.FixedBuffer<Vector3, _AttachmentCount> attachment; // Vector3 attachment[4];

        // Other entity local information
        public int trivial_accept;

        public model_s* model;   // cl.model_precache[ curstate.modelindex ];  all visible entities have a model
        public efrag_t* efrag;   // linked list of efrags
        public mnode_t* topnode; // for bmodels, first world node that splits bmodel, or NULL if not split

        public float syncbase; // for client-side animations -- used by obsolete alias animation system, remove?
        public int visframe;   // last frame this entity was found in an active leaf
        public colorVec cvFloorColor; // rgba

        // Nested types for fixed buffer sizes
        public struct _HistoryMax : NativeInterop.IFixedBufferHolder
        { public fixed float Buffer[(1 + 3 * 2) * EngineConstants.HISTORY_MAX]; }
        public struct _AttachmentCount : NativeInterop.IFixedBufferHolder
        { public fixed float Buffer[3 * 4]; }
    }

    // Server-side structures

    // Link structure for entity linking
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct link_t
    {
        public nint prev; // link_t* prev;
        public nint next; // link_t* next;
    }

    // Entity variables structure
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct entvars_t
    {
        public fixed sbyte classname[32];
        public fixed sbyte globalname[32];

        public Vector3 origin;
        public Vector3 oldorigin;
        public Vector3 velocity;
        public Vector3 basevelocity;
        public Vector3 clbasevelocity;  // Base velocity that was passed in to server physics so
                                         //  client can predict conveyors correctly.  Server zeroes it, so we need to store here, too.
        public Vector3 movedir;

        public Vector3 angles;          // Model angles
        public Vector3 avelocity;       // angle velocity (degrees per second)
        public Vector3 punchangle;      // auto-decaying view angle adjustment
        public Vector3 v_angle;         // Viewing angle (player only)

        // For parametric entities
        public Vector3 endpos;
        public Vector3 startpos;
        public float impacttime;
        public float starttime;

        public int fixangle;             // 0:nothing, 1:force view angles, 2:add avelocity
        public float idealpitch;
        public float pitch_speed;
        public float ideal_yaw;
        public float yaw_speed;

        public int modelindex;
        public fixed sbyte model[64];

        public int viewmodel;            // player's viewmodel
        public int weaponmodel;          // what other players see

        public Vector3 absmin;          // BB max translated to world coord
        public Vector3 absmax;          // BB max translated to world coord
        public Vector3 mins;            // local BB min
        public Vector3 maxs;            // local BB max
        public Vector3 size;            // maxs - mins

        public float ltime;
        public float nextthink;

        public int movetype;
        public int solid;

        public int skin;
        public int body;                 // sub-model selection for studiomodels
        public int effects;

        public float gravity;            // % of "normal" gravity
        public float friction;           // inverse elasticity of MOVETYPE_BOUNCE

        public int light_level;

        public int sequence;             // animation sequence
        public int gaitsequence;         // movement animation sequence for player (0 for none)
        public float frame;              // % playback position in animation sequences (0..255)
        public float animtime;           // world time when frame was set
        public float framerate;          // animation playback rate (-8x to 8x)
        public fixed byte controller[4]; // bone controller setting (0..255)
        public fixed byte blending[2];   // blending amount between sub-sequences (0..255)

        public float scale;              // sprite rendering scale (0..255)

        public int rendermode;
        public float renderamt;
        public Vector3 rendercolor;
        public int renderfx;

        public float health;
        public float frags;
        public int weapons;              // bit mask for available weapons
        public float takedamage;

        public int deadflag;
        public Vector3 view_ofs;        // eye position

        public int button;
        public int impulse;

        public nint chain;             // Entity pointer when linked into a linked list
        public nint dmg_inflictor;
        public nint enemy;
        public nint aiment;            // entity pointer when MOVETYPE_FOLLOW
        public nint owner;
        public nint groundentity;

        public int spawnflags;
        public int flags;

        public int colormap;             // lowbyte topcolor, highbyte bottomcolor
        public int team;

        public float max_health;
        public float teleport_time;
        public float armortype;
        public float armorvalue;
        public int waterlevel;
        public int watertype;

        public fixed sbyte target[32];
        public fixed sbyte targetname[32];
        public fixed sbyte netname[32];
        public fixed sbyte message[2048];

        public float dmg_take;
        public float dmg_save;
        public float dmg;
        public float dmgtime;

        public fixed sbyte noise[64];
        public fixed sbyte noise1[64];
        public fixed sbyte noise2[64];
        public fixed sbyte noise3[64];

        public float speed;
        public float air_finished;
        public float pain_finished;
        public float radsuit_finished;

        public nint pContainingEntity;

        public int playerclass;
        public float maxspeed;

        public float fov;
        public int weaponanim;

        public int pushmsec;

        public int bInDuck;
        public int flTimeStepSound;
        public int flSwimTime;
        public int flDuckTime;
        public int iStepLeft;
        public float flFallVelocity;

        public int gamestate;

        public int oldbuttons;

        public int groupinfo;

        // For mods
        public int iuser1;
        public int iuser2;
        public int iuser3;
        public int iuser4;
        public float fuser1;
        public float fuser2;
        public float fuser3;
        public float fuser4;
        public Vector3 vuser1;
        public Vector3 vuser2;
        public Vector3 vuser3;
        public Vector3 vuser4;
        public nint euser1;
        public nint euser2;
        public nint euser3;
        public nint euser4;
    }

    // Edict structure
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct edict_t
    {
        public qboolean free;
        public int serialnumber;
        public link_t area; // linked to a division node or leaf

        public int headnode; // -1 to use normal leaf check
        public int num_leafs;
        public fixed short leafnums[EngineConstants.MAX_ENT_LEAFS];

        public float freetime; // sv.time when the object was freed

        public nint pvPrivateData; // Alloced and freed by engine, used by DLLs

        public entvars_t v; // C exported fields from progs

        // other fields from progs come immediately after
    }

    // Key-Value data structure
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct KeyValueData
    {
        public sbyte* szClassName; // in: entity classname
        public sbyte* szKeyName;   // in: name of key
        public sbyte* szValue;     // in: value of key
        public int fHandled;       // out: DLL sets to true if key-value pair was understood
    }

    // Field type enumeration
    public enum FIELDTYPE
    {
        FIELD_FLOAT = 0,        // Any floating point value
        FIELD_STRING,           // A string ID (return from ALLOC_STRING)
        FIELD_ENTITY,           // An entity offset (EOFFSET)
        FIELD_CLASSPTR,         // CBaseEntity *
        FIELD_EHANDLE,          // Entity handle
        FIELD_EVARS,            // EVARS *
        FIELD_EDICT,            // edict_t *, or edict_t *  (same thing)
        FIELD_VECTOR,           // Any vector
        FIELD_POSITION_VECTOR,  // A world coordinate (these are fixed up across level transitions automagically)
        FIELD_POINTER,          // Arbitrary data pointer... to be removed, use an array of FIELD_CHARACTER
        FIELD_INTEGER,          // Any integer or enum
        FIELD_FUNCTION,         // A class function pointer (Think, Use, etc)
        FIELD_BOOLEAN,          // boolean, implemented as an int, I may use this as a hint for compression
        FIELD_SHORT,            // 2 byte integer
        FIELD_CHARACTER,        // a byte
        FIELD_TIME,             // a floating point time (these are fixed up automatically too!)
        FIELD_MODELNAME,        // Engine string that is a model name (needs precache)
        FIELD_SOUNDNAME,        // Engine string that is a sound name (needs precache)

        FIELD_TYPECOUNT,        // MUST BE LAST
    }

    // Type description structure
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct TYPEDESCRIPTION
    {
        public FIELDTYPE fieldType;
        public sbyte* fieldName;
        public int fieldOffset;
        public short fieldSize;
        public short flags;
    }

    // Entity table structure
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct ENTITYTABLE
    {
        public int id;              // Ordinal ID of this entity (used for entity <--> pointer conversions)
        public edict_t* pent;       // Pointer to the in-game entity
        public int location;        // Offset from the base data of this entity
        public int size;            // Byte size of this entity's data
        public int flags;           // This could be a short -- bit mask of transitions that this entity is in the PVS of
        public fixed sbyte classname[64]; // entity class name
    }

    // Level list structure
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct LEVELLIST
    {
        public fixed sbyte mapName[32];
        public fixed sbyte landmarkName[32];
        public edict_t* pentLandmark;
        public Vector3 vecLandmarkOrigin;
    }

    // Save/Restore data structure
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct SAVERESTOREDATA
    {
        public sbyte* pBaseData;        // Start of all entity save data
        public sbyte* pCurrentData;     // Current buffer pointer for sequential access
        public int size;                // Current data size
        public int bufferSize;          // Total space for data
        public int tokenSize;           // Size of the linear list of tokens
        public int tokenCount;          // Number of elements in the pTokens table
        public sbyte** pTokens;         // Hash table of entity strings (sparse)
        public int currentIndex;        // Holds a global entity table ID
        public int tableCount;          // Number of elements in the entity table
        public int connectionCount;     // Number of elements in the levelList[]
        public ENTITYTABLE* pTable;     // Array of ENTITYTABLE elements (1 for each entity)
        public fixed byte levelList[16 * 80]; // LEVELLIST levelList[MAX_LEVEL_CONNECTIONS]; // List of connections from this level

        // smooth transition
        public int fUseLandmark;
        public fixed sbyte szLandmarkName[20];  // landmark we'll spawn near in next level
        public Vector3 vecLandmarkOffset;      // for landmark transitions
        public float time;
        public fixed sbyte szCurrentMapName[32]; // To check global entities
    }

    // Resource structure
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct resource_t
    {
        public fixed sbyte szFileName[64]; // File name to download/precache.
        public int type;                   // t_sound, t_skin, t_model, t_decal.
        public int nIndex;                 // For t_decals
        public int nDownloadSize;          // Size in Bytes if this must be downloaded.
        public byte ucFlags;

        // For handling client to client resource propagation
        public fixed byte rgucMD5_hash[16]; // To determine if we already have it.
        public byte playernum;              // Which player index this resource is associated with, if it's a custom resource.

        public fixed byte rguc_reserved[32]; // For future expansion
        public nint pNext;                 // resource_t* pNext; // Next in chain.
        public nint pPrev;                 // resource_t* pPrev; // Previous in chain.
    }

    // Customization structure
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct customization_t
    {
        public qboolean bInUse;         // Is this customization in use;
        public resource_t resource;     // The resource_t for this customization
        public qboolean bTranslated;    // Has the raw data been translated into a useable format?
        public int nUserData1;          // Customization specific data
        public int nUserData2;          // Customization specific data
        public nint pInfo;            // Buffer that holds the data structure that references the data
        public nint pBuffer;          // Buffer that holds the data for the customization
        public nint pNext;            // customization_t* pNext; // Next in chain
    }

    // String type
    public struct string_t { public int Value; }

    // Global variables structure
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct globalvars_t
    {
        public float time;
        public float frametime;
        public float force_retouch;
        public string_t mapname;
        public string_t startspot;
        public float deathmatch;
        public float coop;
        public float teamplay;
        public float serverflags;
        public float found_secrets;
        public Vector3 v_forward;
        public Vector3 v_up;
        public Vector3 v_right;
        public float trace_allsolid;
        public float trace_startsolid;
        public float trace_fraction;
        public Vector3 trace_endpos;
        public Vector3 trace_plane_normal;
        public float trace_plane_dist;
        public edict_t* trace_ent;
        public float trace_inopen;
        public float trace_inwater;
        public int trace_hitgroup;
        public int trace_flags;
        public int msg_entity;
        public int cdAudioTrack;
        public int maxClients;
        public int maxEntities;
        public sbyte* pStringBase;
        public nint pSaveData;        // void* pSaveData;
        public Vector3 vecLandmarkOffset;
    }

    // Temporary entity structure
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct tempent_s
    {
        public int flags;
        public float die;
        public float frameMax;
        public float x;
        public float y;
        public float z;
        public float fadeSpeed;
        public float bounceFactor;
        public int hitSound;
        public delegate* unmanaged[Cdecl]<tempent_s*, pmtrace_s*, void> hitcallback;
        public delegate* unmanaged[Cdecl]<tempent_s*, float, float, void> callback;
        public nint next;             // tempent_s* next;
        public int priority;
        public short clientIndex;       // if attached, this is the index of the client to stick to

        public Vector3 tentOffset;     // if attached, client origin + tentOffset = tent origin.
        public cl_entity_s entity;
    }

    // Client engine functions structure (cl_enginefunc_t)
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct cl_enginefunc_t
    {
        // This structure matches the ClientEngineFuncs structure exactly
        // We use the same layout as defined in ClientEngineFuncs.cs
        public delegate* unmanaged[Cdecl]<sbyte*, HSPRITE> SPR_Load;
        public delegate* unmanaged[Cdecl]<HSPRITE, int> SPR_Frames;
        public delegate* unmanaged[Cdecl]<HSPRITE, int, int> SPR_Height;
        public delegate* unmanaged[Cdecl]<HSPRITE, int, int> SPR_Width;
        public delegate* unmanaged[Cdecl]<HSPRITE, int, int, int, void> SPR_Set;
        public delegate* unmanaged[Cdecl]<int, int, int, Rect*, void> SPR_Draw;
        public delegate* unmanaged[Cdecl]<int, int, int, Rect*, void> SPR_DrawHoles;
        public delegate* unmanaged[Cdecl]<int, int, int, Rect*, void> SPR_DrawAdditive;
        public delegate* unmanaged[Cdecl]<int, int, int, int, void> SPR_EnableScissor;
        public delegate* unmanaged[Cdecl]<void> SPR_DisableScissor;
        public delegate* unmanaged[Cdecl]<sbyte*, int*, client_sprite_s*> SPR_GetList;
        public delegate* unmanaged[Cdecl]<int, int, int, int, int, int, int, int, void> FillRGBA;
        public delegate* unmanaged[Cdecl]<SCREENINFO*, int> GetScreenInfo;
        public delegate* unmanaged[Cdecl]<HSPRITE, Rect, int, int, int, void> SetCrosshair;
        public delegate* unmanaged[Cdecl]<sbyte*, sbyte*, int, cvar_s*> RegisterVariable;
        public delegate* unmanaged[Cdecl]<sbyte*, float> GetCvarFloat;
        public delegate* unmanaged[Cdecl]<sbyte*, sbyte*> GetCvarString;
        public delegate* unmanaged[Cdecl]<sbyte*, delegate* unmanaged[Cdecl]<void>, int> AddCommand;
        public delegate* unmanaged[Cdecl]<sbyte*, delegate* unmanaged[Cdecl]<sbyte*, int, void*, int>, int> HookUserMsg;
        public delegate* unmanaged[Cdecl]<sbyte*, int> ServerCmd;
        public delegate* unmanaged[Cdecl]<sbyte*, int> ClientCmd;
        public delegate* unmanaged[Cdecl]<int, hud_player_info_s*, void> GetPlayerInfo;
        public delegate* unmanaged[Cdecl]<sbyte*, float, void> PlaySoundByName;
        public delegate* unmanaged[Cdecl]<int, float, void> PlaySoundByIndex;
        public delegate* unmanaged[Cdecl]<float*, float*, float*, float*, void> AngleVectors;
        public delegate* unmanaged[Cdecl]<sbyte*, client_textmessage_s*> TextMessageGet;
        public delegate* unmanaged[Cdecl]<int, int, int, int, int, int, int> DrawCharacter;
        public delegate* unmanaged[Cdecl]<int, int, sbyte*, int> DrawConsoleString;
        public delegate* unmanaged[Cdecl]<float, float, float, void> DrawSetTextColor;
        public delegate* unmanaged[Cdecl]<sbyte*, int*, int*, void> DrawConsoleStringLen;
        public delegate* unmanaged[Cdecl]<sbyte*, void> ConsolePrint;
        public delegate* unmanaged[Cdecl]<sbyte*, void> CenterPrint;
        public delegate* unmanaged[Cdecl]<int> GetWindowCenterX;
        public delegate* unmanaged[Cdecl]<int> GetWindowCenterY;
        public delegate* unmanaged[Cdecl]<float*, void> GetViewAngles;
        public delegate* unmanaged[Cdecl]<float*, void> SetViewAngles;
        public delegate* unmanaged[Cdecl]<int> GetMaxClients;
        public delegate* unmanaged[Cdecl]<sbyte*, float, void> Cvar_SetValue;
        public delegate* unmanaged[Cdecl]<int> Cmd_Argc;
        public delegate* unmanaged[Cdecl]<int, sbyte*> Cmd_Argv;
        public delegate* unmanaged[Cdecl]<sbyte*, void> Con_Printf;
        public delegate* unmanaged[Cdecl]<sbyte*, void> Con_DPrintf;
        public delegate* unmanaged[Cdecl]<int, sbyte*, void> Con_NPrintf;
        public delegate* unmanaged[Cdecl]<con_nprint_s*, sbyte*, void> Con_NXPrintf;
        public delegate* unmanaged[Cdecl]<sbyte*, sbyte*> PhysInfo_ValueForKey;
        public delegate* unmanaged[Cdecl]<sbyte*, sbyte*> ServerInfo_ValueForKey;
        public delegate* unmanaged[Cdecl]<float> GetClientMaxspeed;
        public delegate* unmanaged[Cdecl]<sbyte*, sbyte**, int> CheckParm;
        public delegate* unmanaged[Cdecl]<int, int, void> Key_Event;
        public delegate* unmanaged[Cdecl]<int*, int*, void> GetMousePosition;
        public delegate* unmanaged[Cdecl]<int> IsNoClipping;
        public delegate* unmanaged[Cdecl]<cl_entity_s*> GetLocalPlayer;
        public delegate* unmanaged[Cdecl]<cl_entity_s*> GetViewModel;
        public delegate* unmanaged[Cdecl]<int, cl_entity_s*> GetEntityByIndex;
        public delegate* unmanaged[Cdecl]<float> GetClientTime;
        public delegate* unmanaged[Cdecl]<void> V_CalcShake;
        public delegate* unmanaged[Cdecl]<float*, float*, float, void> V_ApplyShake;
        public delegate* unmanaged[Cdecl]<float*, int*, int> PM_PointContents;
        public delegate* unmanaged[Cdecl]<float*, int> PM_WaterEntity;
        public delegate* unmanaged[Cdecl]<float*, float*, int, int, int, pmtrace_s*> PM_TraceLine;
        public delegate* unmanaged[Cdecl]<sbyte*, int*, model_s*> CL_LoadModel;
        public delegate* unmanaged[Cdecl]<int, cl_entity_s*, int> CL_CreateVisibleEntity;
        public delegate* unmanaged[Cdecl]<HSPRITE, model_s*> GetSpritePointer;
        public delegate* unmanaged[Cdecl]<sbyte*, float, float*, void> PlaySoundByNameAtLocation;
        public delegate* unmanaged[Cdecl]<int, sbyte*, ushort> PrecacheEvent;
        public delegate* unmanaged[Cdecl]<int, edict_t*, ushort, float, float*, float*, float, float, int, int, int, int, void> PlaybackEvent;
        public delegate* unmanaged[Cdecl]<int, int, void> WeaponAnim;
        public delegate* unmanaged[Cdecl]<float, float, float> RandomFloat;
        public delegate* unmanaged[Cdecl]<int, int, int> RandomLong;
        public delegate* unmanaged[Cdecl]<sbyte*, delegate* unmanaged[Cdecl]<event_args_s*, void>, void> HookEvent;
        public delegate* unmanaged[Cdecl]<int> Con_IsVisible;
        public delegate* unmanaged[Cdecl]<sbyte*> GetGameDirectory;
        public delegate* unmanaged[Cdecl]<sbyte*, cvar_s*> GetCvarPointer;
        public delegate* unmanaged[Cdecl]<sbyte*, sbyte*> Key_LookupBinding;
        public delegate* unmanaged[Cdecl]<sbyte*> GetLevelName;
        public delegate* unmanaged[Cdecl]<screenfade_s*, void> GetScreenFade;
        public delegate* unmanaged[Cdecl]<screenfade_s*, void> SetScreenFade;
        public delegate* unmanaged[Cdecl]<nint> VGui_GetPanel;
        public delegate* unmanaged[Cdecl]<int, void> VGui_ViewportPaintBackground;
        public delegate* unmanaged[Cdecl]<sbyte*, int, int*, byte*> COM_LoadFile;
        public delegate* unmanaged[Cdecl]<sbyte*, sbyte*> COM_ParseFile;
        public delegate* unmanaged[Cdecl]<nint, void> COM_FreeFile;
        public triangleapi_s* pTriAPI;
        public efx_api_s* pEfxAPI;
        public event_api_s* pEventAPI;
        public demo_api_s* pDemoAPI;
        public net_api_s* pNetAPI;
        public IVoiceTweak_s* pVoiceTweak;
        public delegate* unmanaged[Cdecl]<int> IsSpectateOnly;
        public delegate* unmanaged[Cdecl]<sbyte*, model_s*> LoadMapSprite;
        public delegate* unmanaged[Cdecl]<sbyte*, sbyte*, void> COM_AddAppDirectoryToSearchPath;
        public delegate* unmanaged[Cdecl]<sbyte*, sbyte*, int> COM_ExpandFilename;
        public delegate* unmanaged[Cdecl]<sbyte*, sbyte*> PlayerInfo_ValueForKey;
        public delegate* unmanaged[Cdecl]<sbyte*, sbyte*, void> PlayerInfo_SetValueForKey;
        public delegate* unmanaged[Cdecl]<int, qboolean> GetPlayerUniqueID;
        public delegate* unmanaged[Cdecl]<int, int> GetTrackerIDForPlayer;
        public delegate* unmanaged[Cdecl]<int, int> GetPlayerForTrackerID;
        public delegate* unmanaged[Cdecl]<sbyte*, int> ServerCmdUnreliable;
        public delegate* unmanaged[Cdecl]<int*, int*, void> GetMousePos;
        public delegate* unmanaged[Cdecl]<int, int, void> SetMousePos;
        public delegate* unmanaged[Cdecl]<qboolean, void> SetMouseEnable;
        public delegate* unmanaged[Cdecl]<cvar_s*> GetFirstCVarPtr;
        public delegate* unmanaged[Cdecl]<nint> GetFirstCmdFunctionHandle;
        public delegate* unmanaged[Cdecl]<nint, nint> GetNextCmdFunctionHandle;
        public delegate* unmanaged[Cdecl]<nint, sbyte*> GetCmdFunctionName;
        public delegate* unmanaged[Cdecl]<float> hudGetClientOldTime;
        public delegate* unmanaged[Cdecl]<float> hudGetServerGravityValue;
        public delegate* unmanaged[Cdecl]<int, model_s*> hudGetModelByIndex;
        public delegate* unmanaged[Cdecl]<int, void> SetFilterMode;
        public delegate* unmanaged[Cdecl]<float, float, float, void> SetFilterColor;
        public delegate* unmanaged[Cdecl]<float, void> SetFilterBrightness;
        public delegate* unmanaged[Cdecl]<sbyte*, sentenceEntry_s*> SequenceGet;
        public delegate* unmanaged[Cdecl]<int, int, int, int, int, int, int, void> SPR_DrawGeneric;
        public delegate* unmanaged[Cdecl]<sbyte*, sbyte*, int, sbyte*> SequencePickSentence;
        public delegate* unmanaged[Cdecl]<int, int, sbyte*, int> DrawString;
        public delegate* unmanaged[Cdecl]<int, int, sbyte*, int> DrawStringReverse;
        public delegate* unmanaged[Cdecl]<sbyte*, sbyte*> LocalPlayerInfo_ValueForKey;
        public delegate* unmanaged[Cdecl]<int, int, int, int, int, int, int> VGUI2DrawCharacter;
        public delegate* unmanaged[Cdecl]<int, int, int, int, int, int, int> VGUI2DrawCharacterAdd;
        public delegate* unmanaged[Cdecl]<sbyte*, float> COM_GetApproxWavePlayLength;
        public delegate* unmanaged[Cdecl]<nint> GetCareerUI;
        public delegate* unmanaged[Cdecl]<sbyte*, sbyte*, void> Cvar_Set;
        public delegate* unmanaged[Cdecl]<int> IsCareerMatch;
        public delegate* unmanaged[Cdecl]<sbyte*, float, int, void> PlaySoundVoiceByName;
        public delegate* unmanaged[Cdecl]<sbyte*, int, void> PrimeMusicStream;
        public delegate* unmanaged[Cdecl]<float> GetAbsoluteTime;
        public delegate* unmanaged[Cdecl]<int*, int*, void> ProcessTutorMessageDecayBuffer;
        public delegate* unmanaged[Cdecl]<int*, int*, void> ConstructTutorMessageDecayBuffer;
        public delegate* unmanaged[Cdecl]<void> ResetTutorMessageDecayData;
        public delegate* unmanaged[Cdecl]<sbyte*, float, int, void> PlaySoundByNameAtPitch;
        public delegate* unmanaged[Cdecl]<int, int, int, int, int, int, int, int, void> FillRGBABlend;
        public delegate* unmanaged[Cdecl]<int> GetAppID;
        public delegate* unmanaged[Cdecl]<cmdalias_t*> GetAliasList;
        public delegate* unmanaged[Cdecl]<int*, int*, void> VguiWrap2_GetMouseDelta;
        public delegate* unmanaged[Cdecl]<sbyte*, void> FilteredClientCmd;
    }

    // Screen info structure
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct SCREENINFO
    {
        public int iSize;
        public int iWidth;
        public int iHeight;
        public int iFlags;
        public int iCharHeight;
        public fixed short charWidths[256];
    }

    // Client sprite structure
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct client_sprite_s
    {
        public fixed sbyte szName[64];
        public fixed sbyte szSprite[64];
        public int hspr;
        public int iRes;
        public Rect rc;
    }

    // HUD player info structure (used by HUD system)
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct hud_player_info_s
    {
        public fixed sbyte name[32];
        public short ping;
        public byte thisplayer;  // TRUE if this is the calling player

        // stuff that's unused at the moment,  but should be done
        public byte spectator;
        public byte packetloss;

        public fixed sbyte model[64];
        public short topcolor;
        public short bottomcolor;

        public ulong m_nSteamID;
    }

    // Player info structure (used by Studio renderer for gait animation)
    // This is different from hud_player_info_s!
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct player_info_s
    {
        public int userid;
        public fixed sbyte userinfo[256];  // MAX_INFO_STRING
        public fixed sbyte name[32];       // MAX_SCOREBOARDNAME
        public int spectator;
        public int ping;
        public int packet_loss;
        public fixed sbyte model[64];      // MAX_QPATH
        public int topcolor;
        public int bottomcolor;
        public int renderframe;

        // Gait frame estimation (for player animation)
        public int gaitsequence;
        public float gaitframe;
        public float gaityaw;
        public Vector3 prevgaitorigin;

        // Customization data (not used in Studio renderer)
        // public customization_t customdata;
        public fixed byte customdata[32];  // Placeholder for customization_t

        public fixed byte hashedcdkey[16];
        public ulong m_nSteamID;
    }

    // Lighting structure for studio models
    // Original: typedef struct alight_s from com_model.h
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct alight_s
    {
        public int ambientlight;  // clip at 128
        public int shadelight;    // clip at 192 - ambientlight
        public Vector3 color;
        public float* plightvec;
    }

    // Client text message structure
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct client_textmessage_s
    {
        public int effect;
        public byte r1, g1, b1, a1;         // 2 colors for effects
        public byte r2, g2, b2, a2;
        public float x;
        public float y;
        public float fadein;
        public float fadeout;
        public float holdtime;
        public float fxtime;
        public fixed sbyte pName[32];
        public fixed sbyte pMessage[512];
    }

    // Console print structure
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct con_nprint_s
    {
        public int index;           // Row #
        public float time_to_live;  // # of seconds before it disappears
        public fixed float color[3]; // RGB colors ( 0.0 -> 1.0 scale )
    }

    // Model types and enums
    // Original: typedef enum { mod_brush, mod_sprite, mod_alias, mod_studio } modtype_t; from com_model.h
    public enum modtype_t
    {
        mod_brush,
        mod_sprite,
        mod_alias,
        mod_studio
    }

    // Original: typedef enum { ST_SYNC = 0, ST_RAND } synctype_t; from com_model.h
    public enum synctype_t
    {
        ST_SYNC = 0,
        ST_RAND
    }

    // BSP/Model related structures
    // Original: typedef struct { float mins[3], maxs[3]; float origin[3]; int headnode[MAX_MAP_HULLS]; int visleafs; int firstface, numfaces; } dmodel_t; from com_model.h
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct dmodel_t
    {
        public fixed float mins[3];
        public fixed float maxs[3];
        public fixed float origin[3];
        public fixed int headnode[4]; // MAX_MAP_HULLS
        public int visleafs; // not including the solid leaf 0
        public int firstface;
        public int numfaces;
    }

    // Original: typedef struct mplane_s from com_model.h
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct mplane_t
    {
        public Vector3 normal; // surface normal
        public float dist;     // closest approach to origin
        public byte type;      // for texture axis selection and fast side tests
        public byte signbits;  // signx + signy<<1 + signz<<2
        public fixed byte pad[2];
    }

    // Original: typedef struct { Vector position; } mvertex_t; from com_model.h
    [StructLayout(LayoutKind.Sequential)]
    public struct mvertex_t
    {
        public Vector3 position;
    }

    // Original: typedef struct { unsigned short v[2]; unsigned int cachededgeoffset; } medge_t; from com_model.h
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct medge_t
    {
        public fixed ushort v[2];
        public uint cachededgeoffset;
    }

    // Original: typedef struct texture_s from com_model.h
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct texture_t
    {
        public const int MIPLEVELS = 4;

        public fixed sbyte name[16];
        public uint width;
        public uint height;
        public int anim_total;                // total tenths in sequence (0 = no)
        public int anim_min;
        public int anim_max;                  // time for this frame min <=time< max
        public texture_t* anim_next;          // in the animation sequence
        public texture_t* alternate_anims;    // bmodels in frame 1 use these
        public fixed uint offsets[MIPLEVELS]; // four mip maps stored
        public uint paloffset;
    }

    // Original: typedef struct { float vecs[2][4]; float mipadjust; texture_t* texture; int flags; } mtexinfo_t; from com_model.h
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct mtexinfo_t
    {
        public fixed float vecs[2 * 4]; // [s/t] unit vectors in world space. [i][3] is the s/t offset relative to the origin.
        public float mipadjust;         // mipmap limits for very small surfaces
        public texture_t* texture;
        public int flags;               // sky or slime, no lightmap or 256 subdivision
    }

    // Original: typedef struct { int planenum; short children[2]; } dclipnode_t; from com_model.h
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct dclipnode_t
    {
        public int planenum;
        public fixed short children[2]; // negative numbers are contents
    }

    // Original: typedef struct hull_s from com_model.h
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct hull_t
    {
        public dclipnode_t* clipnodes;
        public mplane_t* planes;
        public int firstclipnode;
        public int lastclipnode;
        public Vector3 clip_mins;
        public Vector3 clip_maxs;
    }

    // Forward declarations for circular references
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct mnode_t
    {
        // Common with leaf
        public int contents;  // 0, to differentiate from leafs
        public int visframe; // node needs to be traversed if current

        public fixed short minmaxs[6]; // for bounding box culling

        public mnode_t* parent;

        // Node specific
        public mplane_t* plane;
        public mnode_t* children0;
        public mnode_t* children1;

        public ushort firstsurface;
        public ushort numsurfaces;
    }

    // Original: typedef struct mleaf_s from com_model.h
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct mleaf_t
    {
        public const int NUM_AMBIENTS = 4;

        // Common with node
        public int contents;  // will be a negative contents number
        public int visframe; // node needs to be traversed if current

        public fixed short minmaxs[6]; // for bounding box culling

        public mnode_t* parent;

        // Leaf specific
        public byte* compressed_vis;
        public efrag_t* efrags;

        public msurface_t** firstmarksurface;
        public int nummarksurfaces;
        public int key; // BSP sequence number for leaf's contents
        public fixed byte ambient_sound_level[NUM_AMBIENTS];
    }

    // Original: typedef struct efrag_s from cl_entity.h
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct efrag_t
    {
        public mleaf_t* leaf;
        public efrag_t* leafnext;
        public cl_entity_s* entity;
        public efrag_t* entnext;
    }

    // Forward declaration for decal
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct decal_t
    {
        public decal_t* pnext;        // linked list for each surface
        public msurface_t* psurface;  // Surface id for persistence / unlinking
        public short dx;              // Offsets into surface texture (in texture coordinates)
        public short dy;
        public short texture;         // Decal texture
        public byte scale;            // Pixel scale
        public byte flags;            // Decal flags
        public short entityIndex;     // Entity this is attached to
    }

    // Original: struct msurface_s from com_model.h
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct msurface_t
    {
        public const int MIPLEVELS = 4;
        public const int MAXLIGHTMAPS = 4;

        public int visframe; // should be drawn when node is crossed

        public int dlightframe; // last frame the surface was checked by an animated light
        public int dlightbits;  // dynamically generated. Indicates if the surface illumination is modified by an animated light.

        public mplane_t* plane; // pointer to shared plane
        public int flags;       // see SURF_ #defines

        public int firstedge; // look up in model->surfedges[], negative numbers
        public int numedges;  // are backwards edges

        // Surface generation data
        public nint cachespots0; // surfcache_s* cachespots[MIPLEVELS];
        public nint cachespots1;
        public nint cachespots2;
        public nint cachespots3;

        public fixed short texturemins[2]; // smallest s/t position on the surface.
        public fixed short extents[2];     // s/t texture size, 1..256 for all non-sky surfaces

        public mtexinfo_t* texinfo;

        // Lighting info
        public fixed byte styles[MAXLIGHTMAPS]; // index into d_lightstylevalue[] for animated lights
        public color24* samples;

        public decal_t* pdecals;
    }

    // Model structure
    // Original: typedef struct model_s from com_model.h
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct model_s
    {
        public const int MAX_MODEL_NAME = 64;
        public const int MAX_MAP_HULLS = 4;

        public fixed sbyte name[MAX_MODEL_NAME];
        public qboolean needload; // bmodels and sprites don't cache normally

        public modtype_t type;
        public int numframes;
        public synctype_t synctype;

        public int flags;

        // Volume occupied by the model
        public Vector3 mins;
        public Vector3 maxs;
        public float radius;

        // Brush model
        public int firstmodelsurface;
        public int nummodelsurfaces;

        public int numsubmodels;
        public dmodel_t* submodels;

        public int numplanes;
        public mplane_t* planes;

        public int numleafs; // number of visible leafs, not counting 0
        public mleaf_t* leafs;

        public int numvertexes;
        public mvertex_t* vertexes;

        public int numedges;
        public medge_t* edges;

        public int numnodes;
        public mnode_t* nodes;

        public int numtexinfo;
        public mtexinfo_t* texinfo;

        public int numsurfaces;
        public msurface_t* surfaces;

        public int numsurfedges;
        public int* surfedges;

        public int numclipnodes;
        public dclipnode_t* clipnodes;

        public int nummarksurfaces;
        public msurface_t** marksurfaces;

        public NativeInterop.FixedBuffer<hull_t, _MaxMapHulls> hulls;

        public int numtextures;
        public texture_t** textures;

        public byte* visdata;

        public color24* lightdata;

        public sbyte* entities;

        // Additional model data
        public cache_user_s cache; // only access through Mod_Extradata

        // Nested type for hull array size
        public struct _MaxMapHulls : NativeInterop.IFixedBufferHolder
        { public fixed byte Buffer[40 * MAX_MAP_HULLS]; }
    }

    // Event args structure
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct event_args_s
    {
        public int flags;

        // Transmitted
        public int entindex;

        public fixed float origin[3];
        public fixed float angles[3];
        public fixed float velocity[3];

        public int ducking;

        public float fparam1;
        public float fparam2;

        public int iparam1;
        public int iparam2;

        public int bparam1;
        public int bparam2;
    }

    // Screen fade structure
    [StructLayout(LayoutKind.Sequential)]
    public struct screenfade_s
    {
        public float fadeSpeed;     // How fast to fade (tics / second) (+ fade in, - fade out)
        public float fadeEnd;       // When the fading hits maximum
        public float fadeTotalEnd;  // Total End Time of the fade (used for FFADE_OUT)
        public float fadeReset;     // When to reset to not fading (for fadeout and hold)
        public byte fader, fadeg, fadeb, fadealpha; // Fade color
        public int fadeFlags;       // Fading flags
    }

    // Triangle API structure
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct triangleapi_s
    {
        public int version;
        public delegate* unmanaged[Cdecl]<int, void> RenderMode;
        public delegate* unmanaged[Cdecl]<int, void> Begin;
        public delegate* unmanaged[Cdecl]<void> End;
        public delegate* unmanaged[Cdecl]<float, float, float, void> Color4f;
        public delegate* unmanaged[Cdecl]<byte, byte, byte, byte, void> Color4ub;
        public delegate* unmanaged[Cdecl]<float, float, void> TexCoord2f;
        public delegate* unmanaged[Cdecl]<float, float, float, void> Vertex3fv;
        public delegate* unmanaged[Cdecl]<float*, void> Vertex3f;
        public delegate* unmanaged[Cdecl]<float, void> Brightness;
        public delegate* unmanaged[Cdecl]<int, void> CullFace;
        public delegate* unmanaged[Cdecl]<int, int, void> SpriteTexture;
        public delegate* unmanaged[Cdecl]<int, int, void> WorldToScreen;
        public delegate* unmanaged[Cdecl]<float, void> Fog;
        public delegate* unmanaged[Cdecl]<int, int, int, int, void> ScreenToWorld;
        public delegate* unmanaged[Cdecl]<float*, void> GetMatrix;
        public delegate* unmanaged[Cdecl]<int, int> BoxInPVS;
        public delegate* unmanaged[Cdecl]<float*, void> LightAtPoint;
        public delegate* unmanaged[Cdecl]<float*, float*, void> Color4fRendermode;
        public delegate* unmanaged[Cdecl]<float*, float*, float*, void> FogParams;
    }

    // Effects API structure - 按照 HLSDK 原始定义
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct efx_api_s
    {
        // 粒子相关函数 - 返回 void
        public delegate* unmanaged[Cdecl]<void*, nint> R_AllocParticle; // particle_t* (*R_AllocParticle)(void (*callback)(struct particle_s* particle, float frametime));
        public delegate* unmanaged[Cdecl]<float*, void> R_BlobExplosion;
        public delegate* unmanaged[Cdecl]<float*, float*, int, int, void> R_Blood;
        public delegate* unmanaged[Cdecl]<float*, int, int, int, float, void> R_BloodSprite;
        public delegate* unmanaged[Cdecl]<float*, float*, int, int, void> R_BloodStream;
        public delegate* unmanaged[Cdecl]<float*, float*, float*, float, float, int, int, sbyte, void> R_BreakModel;
        public delegate* unmanaged[Cdecl]<float*, float*, float, int, int, float, void> R_Bubbles;
        public delegate* unmanaged[Cdecl]<float*, float*, float, int, int, float, void> R_BubbleTrail;
        public delegate* unmanaged[Cdecl]<float*, void> R_BulletImpactParticles;
        public delegate* unmanaged[Cdecl]<cl_entity_s*, void> R_EntityParticles;
        public delegate* unmanaged[Cdecl]<float*, int, float, float, int, void> R_Explosion;
        public delegate* unmanaged[Cdecl]<cl_entity_s*, int, int, void> R_FizzEffect;
        public delegate* unmanaged[Cdecl]<float*, int, int, int, int, float, void> R_FireField;
        public delegate* unmanaged[Cdecl]<float*, void> R_FlickerParticles;
        public delegate* unmanaged[Cdecl]<float*, int, int, void> R_FunnelSprite;
        public delegate* unmanaged[Cdecl]<float*, float, int, float, void> R_Implosion;
        public delegate* unmanaged[Cdecl]<float*, int, void> R_LargeFunnel;
        public delegate* unmanaged[Cdecl]<float*, void> R_LavaSplash;
        public delegate* unmanaged[Cdecl]<float*, float*, float*, int, int, int*, void> R_MultiGunshot;
        public delegate* unmanaged[Cdecl]<float*, int, void> R_MuzzleFlash;
        public delegate* unmanaged[Cdecl]<float*, float*, byte, byte, byte, float, void> R_ParticleBox;
        public delegate* unmanaged[Cdecl]<float*, int, int, float, void> R_ParticleBurst;
        public delegate* unmanaged[Cdecl]<float*, void> R_ParticleExplosion;
        public delegate* unmanaged[Cdecl]<float*, int, int, void> R_ParticleExplosion2;
        public delegate* unmanaged[Cdecl]<float*, float*, byte, byte, byte, float, void> R_ParticleLine;
        public delegate* unmanaged[Cdecl]<int, int, int, int, void> R_PlayerSprites;
        public delegate* unmanaged[Cdecl]<float*, float*, int, int, int, delegate* unmanaged[Cdecl]<tempent_s*, pmtrace_s*, void>, void> R_Projectile;
        public delegate* unmanaged[Cdecl]<float*, void> R_RicochetSound;
        public delegate* unmanaged[Cdecl]<float*, model_s*, float, float, void> R_RicochetSprite;
        public delegate* unmanaged[Cdecl]<float*, void> R_RocketFlare;
        public delegate* unmanaged[Cdecl]<float*, float*, int, void> R_RocketTrail;
        public delegate* unmanaged[Cdecl]<float*, float*, int, int, void> R_RunParticleEffect;
        public delegate* unmanaged[Cdecl]<float*, float*, void> R_ShowLine;
        public delegate* unmanaged[Cdecl]<float*, int, int, int, void> R_SparkEffect;
        public delegate* unmanaged[Cdecl]<float*, void> R_SparkShower;
        public delegate* unmanaged[Cdecl]<float*, int, int, int, void> R_SparkStreaks;
        public delegate* unmanaged[Cdecl]<float*, float*, int, int, int, int, int, void> R_Spray;
        public delegate* unmanaged[Cdecl]<tempent_s*, float, int, void> R_Sprite_Explode;
        public delegate* unmanaged[Cdecl]<tempent_s*, float, void> R_Sprite_Smoke;
        public delegate* unmanaged[Cdecl]<float*, void> R_StreakSplash;
        public delegate* unmanaged[Cdecl]<float*, void> R_TeleportSplash;
        public delegate* unmanaged[Cdecl]<float*, float, float, int, int, void> R_TempSphereModel;

        // 返回 TEMPENTITY* 的函数
        public delegate* unmanaged[Cdecl]<float*, float*, float*, float, int, int, tempent_s*> R_TempModel;
        public delegate* unmanaged[Cdecl]<float*, int, float, tempent_s*> R_DefaultSprite;
        public delegate* unmanaged[Cdecl]<float*, float*, float, int, int, int, float, float, int, tempent_s*> R_TempSprite;

        // 贴花相关
        public delegate* unmanaged[Cdecl]<int, int> Draw_DecalIndex;
        public delegate* unmanaged[Cdecl]<sbyte*, int> Draw_DecalIndexFromName;
        public delegate* unmanaged[Cdecl]<int, int, int, float*, int, void> R_DecalShoot;

        // 附着和光束相关
        public delegate* unmanaged[Cdecl]<int, int, float, float, void> R_AttachTentToPlayer;
        public delegate* unmanaged[Cdecl]<int, void> R_KillAttachedTents;
        public delegate* unmanaged[Cdecl]<int, int, int, float, float, float, float, float, int, float, float, float, float, nint> R_BeamCirclePoints; // BEAM*
        public delegate* unmanaged[Cdecl]<int, float*, int, float, float, float, float, float, nint> R_BeamEntPoint; // BEAM*
        public delegate* unmanaged[Cdecl]<int, int, int, float, float, float, float, float, nint> R_BeamEnts; // BEAM*
        public delegate* unmanaged[Cdecl]<int, int, float, float, float, float, float, nint> R_BeamFollow; // BEAM*
        public delegate* unmanaged[Cdecl]<int, void> R_BeamKill;
        public delegate* unmanaged[Cdecl]<float*, float*, int, float, float, float, float, float, nint> R_BeamLightning; // BEAM*
        public delegate* unmanaged[Cdecl]<float*, float*, int, float, float, float, float, float, int, float, float, float, float, nint> R_BeamPoints; // BEAM*
        public delegate* unmanaged[Cdecl]<int, int, int, float, float, float, float, float, int, float, float, float, float, nint> R_BeamRing; // BEAM*

        // 动态光源
        public delegate* unmanaged[Cdecl]<int, nint> CL_AllocDlight; // dlight_t*
        public delegate* unmanaged[Cdecl]<int, nint> CL_AllocElight; // dlight_t*

        // 临时实体分配 - 返回 TEMPENTITY*
        public delegate* unmanaged[Cdecl]<float*, model_s*, tempent_s*> CL_TempEntAlloc;
        public delegate* unmanaged[Cdecl]<float*, tempent_s*> CL_TempEntAllocNoModel;
        public delegate* unmanaged[Cdecl]<float*, model_s*, tempent_s*> CL_TempEntAllocHigh;
        public delegate* unmanaged[Cdecl]<float*, model_s*, int, delegate* unmanaged[Cdecl]<tempent_s*, float, float, void>, tempent_s*> CL_TentEntAllocCustom;

        // 颜色和贴花
        public delegate* unmanaged[Cdecl]<short*, short, void> R_GetPackedColor;
        public delegate* unmanaged[Cdecl]<byte, byte, byte, short> R_LookupColor;
        public delegate* unmanaged[Cdecl]<int, void> R_DecalRemoveAll;
        public delegate* unmanaged[Cdecl]<int, int, int, float*, int, float, void> R_FireCustomDecal;
    }

    // Event API structure
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct event_api_s
    {
        public int version;
        public delegate* unmanaged[Cdecl]<int, float*, int, sbyte*, float, float, int, int, void> EV_PlaySound;
        public delegate* unmanaged[Cdecl]<int, int, sbyte*, void> EV_StopSound;
        public delegate* unmanaged[Cdecl]<sbyte*, int> EV_FindModelIndex;
        public delegate* unmanaged[Cdecl]<int, int> EV_IsLocal;
        public delegate* unmanaged[Cdecl]<int> EV_LocalPlayerDucking;
        public delegate* unmanaged[Cdecl]<float*, void> EV_LocalPlayerViewheight;
        public delegate* unmanaged[Cdecl]<int, float*, float*, void> EV_LocalPlayerBounds;
        public delegate* unmanaged[Cdecl]<pmtrace_s*, int> EV_IndexFromTrace;
        public delegate* unmanaged[Cdecl]<int, physent_t*> EV_GetPhysent;
        public delegate* unmanaged[Cdecl]<int, int, void> EV_SetUpPlayerPrediction;
        public delegate* unmanaged[Cdecl]<void> EV_PushPMStates;
        public delegate* unmanaged[Cdecl]<void> EV_PopPMStates;
        public delegate* unmanaged[Cdecl]<int, void> EV_SetSolidPlayers;
        public delegate* unmanaged[Cdecl]<int, void> EV_SetTraceHull;
        public delegate* unmanaged[Cdecl]<float*, float*, int, int, pmtrace_s*, void> EV_PlayerTrace;
        public delegate* unmanaged[Cdecl]<int, float*, float*, float*, float*, void> EV_WeaponAnimation;
        public delegate* unmanaged[Cdecl]<int, sbyte*, int> EV_PrecacheEvent;
        public delegate* unmanaged[Cdecl]<int, int, float, event_args_s*, void> EV_PlaybackEvent;
    }

    // Demo API structure (simplified)
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct demo_api_s
    {
        public int version;
        public delegate* unmanaged[Cdecl]<int> IsRecording;
        public delegate* unmanaged[Cdecl]<int> IsPlayingback;
        public delegate* unmanaged[Cdecl]<int> IsTimeDemo;
        public delegate* unmanaged[Cdecl]<byte*, int, void> WriteBuffer;
    }

    // Network API structure (simplified)
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct net_api_s
    {
        public int version;
        public delegate* unmanaged[Cdecl]<netadr_s*, void> InitNetworking;
        public delegate* unmanaged[Cdecl]<netadr_s*, sbyte*, void> Status;
        public delegate* unmanaged[Cdecl]<netadr_s*, sbyte*, void> SendRequest;
        public delegate* unmanaged[Cdecl]<void> CancelRequest;
        public delegate* unmanaged[Cdecl]<void> ClearRequestQueue;
        public delegate* unmanaged[Cdecl]<netadr_s*, sbyte*, void> SendResponse;
        public delegate* unmanaged[Cdecl]<netadr_s*, sbyte*, void> GetResponse;
    }

    // Voice Tweak interface (simplified)
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct IVoiceTweak_s
    {
        public int version;
        public delegate* unmanaged[Cdecl]<int> StartVoiceTweakMode;
        public delegate* unmanaged[Cdecl]<void> EndVoiceTweakMode;
        public delegate* unmanaged[Cdecl]<float, void> SetControlFloat;
        public delegate* unmanaged[Cdecl]<float> GetControlFloat;
        public delegate* unmanaged[Cdecl]<int> GetSpeakingVolume;
    }

    // Sentence entry structure
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct sentenceEntry_s
    {
        public fixed sbyte data[512];
        public int length;
        public int index;
    }

    // Command alias structure
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct cmdalias_t
    {
        public nint next;         // cmdalias_t* next;
        public fixed sbyte name[32];
        public fixed sbyte value[1024];
    }

    // Server engine functions structure (enginefuncs_t)
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct enginefuncs_s
    {
        // This structure matches the ServerEngineFuncs structure exactly
        // We use the same layout as defined in ServerEngineFuncs.cs
        public delegate* unmanaged[Cdecl]<sbyte*, int> PrecacheModel;
        public delegate* unmanaged[Cdecl]<sbyte*, int> PrecacheSound;
        public delegate* unmanaged[Cdecl]<edict_t*, sbyte*, void> SetModel;
        public delegate* unmanaged[Cdecl]<sbyte*, int> ModelIndex;
        public delegate* unmanaged[Cdecl]<int, int> ModelFrames;
        public delegate* unmanaged[Cdecl]<edict_t*, float*, float*, void> SetSize;
        public delegate* unmanaged[Cdecl]<sbyte*, sbyte*, void> ChangeLevel;
        public delegate* unmanaged[Cdecl]<edict_t*, void> GetSpawnParms;
        public delegate* unmanaged[Cdecl]<edict_t*, void> SaveSpawnParms;
        public delegate* unmanaged[Cdecl]<float*, float> VecToYaw;
        public delegate* unmanaged[Cdecl]<float*, float*, void> VecToAngles;
        public delegate* unmanaged[Cdecl]<edict_t*, float*, float, int, void> MoveToOrigin;
        public delegate* unmanaged[Cdecl]<edict_t*, void> ChangeYaw;
        public delegate* unmanaged[Cdecl]<edict_t*, void> ChangePitch;
        public delegate* unmanaged[Cdecl]<sbyte*, edict_t*> FindEntityByString;
        public delegate* unmanaged[Cdecl]<edict_t*, int> GetEntityIllum;
        public delegate* unmanaged[Cdecl]<sbyte*, edict_t*> FindEntityInSphere;
        public delegate* unmanaged[Cdecl]<edict_t*, edict_t*> FindClientInPVS;
        public delegate* unmanaged[Cdecl]<edict_t*, edict_t*> EntitiesInPVS;
        public delegate* unmanaged[Cdecl]<float*, void> MakeVectors;
        public delegate* unmanaged[Cdecl]<float*, float*, void> AngleVectors;
        public delegate* unmanaged[Cdecl]<edict_t*> CreateEntity;
        public delegate* unmanaged[Cdecl]<edict_t*, void> RemoveEntity;
        public delegate* unmanaged[Cdecl]<edict_t*> CreateNamedEntity;
        public delegate* unmanaged[Cdecl]<edict_t*, void> MakeStatic;
        public delegate* unmanaged[Cdecl]<edict_t*, int> EntIsOnFloor;
        public delegate* unmanaged[Cdecl]<edict_t*, int> DropToFloor;
        public delegate* unmanaged[Cdecl]<edict_t*, edict_t*, float, int> WalkMove;
        public delegate* unmanaged[Cdecl]<float*, float*, edict_t*, void> SetOrigin;
        public delegate* unmanaged[Cdecl]<int, int, float*, float*, edict_t*, float, void> EmitSound;
        public delegate* unmanaged[Cdecl]<int, int, float*, float*, edict_t*, float, float, void> EmitAmbientSound;
        public delegate* unmanaged[Cdecl]<float*, float*, float*, TraceResult*, void> TraceLine;
        public delegate* unmanaged[Cdecl]<float*, float*, int, edict_t*, TraceResult*, void> TraceToss;
        public delegate* unmanaged[Cdecl]<float*, float*, int, float*, float*, edict_t*, TraceResult*, int> TraceMonsterHull;
        public delegate* unmanaged[Cdecl]<float*, float*, int, edict_t*, TraceResult*, void> TraceHull;
        public delegate* unmanaged[Cdecl]<edict_t*, float*, float*, TraceResult*, void> TraceModel;
        public delegate* unmanaged[Cdecl]<float*, sbyte*> TraceTexture;
        public delegate* unmanaged[Cdecl]<float*, float*, void> TraceSphere;
        public delegate* unmanaged[Cdecl]<float*, void> GetAimVector;
        public delegate* unmanaged[Cdecl]<sbyte*, void> ServerCommand;
        public delegate* unmanaged[Cdecl]<void> ServerExecute;
        public delegate* unmanaged[Cdecl]<edict_t*, sbyte*, void> ClientCommand;
        public delegate* unmanaged[Cdecl]<int, int, float*, void> ParticleEffect;
        public delegate* unmanaged[Cdecl]<float*, void> LightStyle;
        public delegate* unmanaged[Cdecl]<sbyte*, int> DecalIndex;
        public delegate* unmanaged[Cdecl]<float*, int> PointContents;
        public delegate* unmanaged[Cdecl]<edict_t*, int, sbyte*, void> MessageBegin;
        public delegate* unmanaged[Cdecl]<void> MessageEnd;
        public delegate* unmanaged[Cdecl]<byte, void> WriteByte;
        public delegate* unmanaged[Cdecl]<sbyte, void> WriteChar;
        public delegate* unmanaged[Cdecl]<short, void> WriteShort;
        public delegate* unmanaged[Cdecl]<int, void> WriteLong;
        public delegate* unmanaged[Cdecl]<float, void> WriteAngle;
        public delegate* unmanaged[Cdecl]<float, void> WriteCoord;
        public delegate* unmanaged[Cdecl]<sbyte*, void> WriteString;
        public delegate* unmanaged[Cdecl]<edict_t*, void> WriteEntity;
        public delegate* unmanaged[Cdecl]<sbyte*, void> CVarRegister;
        public delegate* unmanaged[Cdecl]<sbyte*, float> CVarGetFloat;
        public delegate* unmanaged[Cdecl]<sbyte*, sbyte*> CVarGetString;
        public delegate* unmanaged[Cdecl]<sbyte*, float, void> CVarSetFloat;
        public delegate* unmanaged[Cdecl]<sbyte*, sbyte*, void> CVarSetString;
        public delegate* unmanaged[Cdecl]<int, sbyte*, void> AlertMessage;
        public delegate* unmanaged[Cdecl]<nint, void> EngineFprintf; // void* pfile
        public delegate* unmanaged[Cdecl]<edict_t*, nint> PvAllocEntPrivateData;
        public delegate* unmanaged[Cdecl]<edict_t*, nint> PvEntPrivateData;
        public delegate* unmanaged[Cdecl]<edict_t*, void> FreeEntPrivateData;
        public delegate* unmanaged[Cdecl]<int, sbyte*> SzFromIndex;
        public delegate* unmanaged[Cdecl]<sbyte*, int> AllocString;
        public delegate* unmanaged[Cdecl]<edict_t*, entvars_t*> GetVarsOfEnt;
        public delegate* unmanaged[Cdecl]<entvars_t*, edict_t*> PEntityOfEntOffset;
        public delegate* unmanaged[Cdecl]<edict_t*, int> EntOffsetOfPEntity;
        public delegate* unmanaged[Cdecl]<edict_t*, int> IndexOfEdict;
        public delegate* unmanaged[Cdecl]<int, edict_t*> PEntityOfEntIndex;
        public delegate* unmanaged[Cdecl]<edict_t*, edict_t*> FindEntityByVars;
        public delegate* unmanaged[Cdecl]<edict_t*, nint> GetModelPtr; // model_s*
        public delegate* unmanaged[Cdecl]<sbyte*, int> RegUserMsg;
        public delegate* unmanaged[Cdecl]<void> AnimationAutomove;
        public delegate* unmanaged[Cdecl]<edict_t*, void> GetBonePosition;
        public delegate* unmanaged[Cdecl]<uint, uint> FunctionFromName;
        public delegate* unmanaged[Cdecl]<uint, sbyte*> NameForFunction;
        public delegate* unmanaged[Cdecl]<edict_t*, sbyte*, void> ClientPrintf;
        public delegate* unmanaged[Cdecl]<sbyte*, void> ServerPrint;
        public delegate* unmanaged[Cdecl]<int> Cmd_Args;
        public delegate* unmanaged[Cdecl]<int> Cmd_Argc;
        public delegate* unmanaged[Cdecl]<int, sbyte*> Cmd_Argv;
        public delegate* unmanaged[Cdecl]<edict_t*, int, void> GetAttachment;
        public delegate* unmanaged[Cdecl]<void> CRC32_Init;
        public delegate* unmanaged[Cdecl]<uint*, byte, void> CRC32_ProcessBuffer;
        public delegate* unmanaged[Cdecl]<uint, void> CRC32_ProcessByte;
        public delegate* unmanaged[Cdecl]<uint, uint> CRC32_Final;
        public delegate* unmanaged[Cdecl]<int, int> RandomLong;
        public delegate* unmanaged[Cdecl]<float, float, float> RandomFloat;
        public delegate* unmanaged[Cdecl]<edict_t*, int, void> SetView;
        public delegate* unmanaged[Cdecl]<float> Time;
        public delegate* unmanaged[Cdecl]<edict_t*, edict_t*, void> CrosshairAngle;
        public delegate* unmanaged[Cdecl]<sbyte*, int*, byte*> LoadFileForMe;
        public delegate* unmanaged[Cdecl]<nint, void> FreeFile; // void* pFile
        public delegate* unmanaged[Cdecl]<void> EndSection;
        public delegate* unmanaged[Cdecl]<sbyte*, sbyte*, int> CompareFileTime;
        public delegate* unmanaged[Cdecl]<sbyte*, void> GetGameDir;
        public delegate* unmanaged[Cdecl]<sbyte*, void> Cvar_RegisterVariable;
        public delegate* unmanaged[Cdecl]<edict_t*, float*, void> FadeClientVolume;
        public delegate* unmanaged[Cdecl]<edict_t*, int, void> SetClientMaxspeed;
        public delegate* unmanaged[Cdecl]<int, edict_t*> CreateFakeClient;
        public delegate* unmanaged[Cdecl]<edict_t*, sbyte*, float, void> RunPlayerMove;
        public delegate* unmanaged[Cdecl]<edict_t*, int> NumberOfEntities;
        public delegate* unmanaged[Cdecl]<edict_t*, sbyte*> GetInfoKeyBuffer;
        public delegate* unmanaged[Cdecl]<sbyte*, sbyte*, sbyte*> InfoKeyValue;
        public delegate* unmanaged[Cdecl]<sbyte*, sbyte*, sbyte*, void> SetKeyValue;
        public delegate* unmanaged[Cdecl]<sbyte*, sbyte*, sbyte*, void> SetClientKeyValue;
        public delegate* unmanaged[Cdecl]<sbyte*, int> IsMapValid;
        public delegate* unmanaged[Cdecl]<float*, float*, void> StaticDecal;
        public delegate* unmanaged[Cdecl]<int, int> PrecacheGeneric;
        public delegate* unmanaged[Cdecl]<edict_t*, int> GetPlayerUserId;
        public delegate* unmanaged[Cdecl]<int, float*, void> BuildSoundMsg;
        public delegate* unmanaged[Cdecl]<int> IsDedicatedServer;
        public delegate* unmanaged[Cdecl]<sbyte*, cvar_s*> CVarGetPointer;
        public delegate* unmanaged[Cdecl]<edict_t*, uint> GetPlayerWONId;
        public delegate* unmanaged[Cdecl]<sbyte*, void> Info_RemoveKey;
        public delegate* unmanaged[Cdecl]<sbyte*, sbyte*> GetPhysicsKeyValue;
        public delegate* unmanaged[Cdecl]<sbyte*, sbyte*, void> SetPhysicsKeyValue;
        public delegate* unmanaged[Cdecl]<edict_t*, sbyte*> GetPhysicsInfoString;
        public delegate* unmanaged[Cdecl]<ushort, int> PrecacheEvent;
        public delegate* unmanaged[Cdecl]<int, edict_t*, ushort, float, float*, float*, float, float, int, int, int, int, void> PlaybackEvent;
        public delegate* unmanaged[Cdecl]<byte*, int, void> SetFatPVS;
        public delegate* unmanaged[Cdecl]<byte*, int, void> SetFatPAS;
        public delegate* unmanaged[Cdecl]<float*, int> CheckVisibility;
        public delegate* unmanaged[Cdecl]<int, void> DeltaSetField;
        public delegate* unmanaged[Cdecl]<int, void> DeltaUnsetField;
        public delegate* unmanaged[Cdecl]<int, void> DeltaAddEncoder;
        public delegate* unmanaged[Cdecl]<edict_t*, int> GetCurrentPlayer;
        public delegate* unmanaged[Cdecl]<int, int> CanSkipPlayer;
        public delegate* unmanaged[Cdecl]<int, int> DeltaFindField;
        public delegate* unmanaged[Cdecl]<void> DeltaSetFieldByIndex;
        public delegate* unmanaged[Cdecl]<void> DeltaUnsetFieldByIndex;
        public delegate* unmanaged[Cdecl]<edict_t*, int, void> SetGroupMask;
        public delegate* unmanaged[Cdecl]<int, int, void> engCreateInstancedBaseline;
        public delegate* unmanaged[Cdecl]<sbyte*, void> Cvar_DirectSet;
        public delegate* unmanaged[Cdecl]<edict_t*, sbyte*, void> ForceUnmodified;
        public delegate* unmanaged[Cdecl]<edict_t*, void> GetPlayerStats;
        public delegate* unmanaged[Cdecl]<sbyte*, void> AddServerCommand;
        public delegate* unmanaged[Cdecl]<int> Voice_GetClientListening;
        public delegate* unmanaged[Cdecl]<int, int, int> Voice_SetClientListening;
        public delegate* unmanaged[Cdecl]<edict_t*, sbyte*> GetPlayerAuthId;
        public delegate* unmanaged[Cdecl]<nint, int*, nint> SequenceGet; // sentenceEntry_s*
        public delegate* unmanaged[Cdecl]<nint, sbyte*, int, sbyte*> SequencePickSentence; // sentenceEntry_s*
        public delegate* unmanaged[Cdecl]<sbyte*, int> GetFileSize;
        public delegate* unmanaged[Cdecl]<edict_t*, uint> GetApproxWavePlayLen;
        public delegate* unmanaged[Cdecl]<int> IsCareerMatch;
        public delegate* unmanaged[Cdecl]<int> GetLocalizedStringLength;
        public delegate* unmanaged[Cdecl]<edict_t*, int, void> RegisterTutorMessageShown;
        public delegate* unmanaged[Cdecl]<edict_t*, int> GetTimesTutorMessageShown;
        public delegate* unmanaged[Cdecl]<int*, int*, void> ProcessTutorMessageDecayBuffer;
        public delegate* unmanaged[Cdecl]<int*, int*, void> ConstructTutorMessageDecayBuffer;
        public delegate* unmanaged[Cdecl]<void> ResetTutorMessageDecayData;
        public delegate* unmanaged[Cdecl]<edict_t*, sbyte*, sbyte*, void> QueryClientCvarValue;
        public delegate* unmanaged[Cdecl]<edict_t*, int, sbyte*, sbyte*, void> QueryClientCvarValue2;
        public delegate* unmanaged[Cdecl]<int*, int> EngCheckParm;
    }

    // Console variable structure
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct cvar_s
    {
        public sbyte* name;
        public sbyte* @string;  // using @ to escape C# keyword
        public int flags;
        public float value;
        public nint next;     // cvar_s* next;
    }

    // Cache user structure
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct cache_user_s
    {
        public nint data;
    }

    // Studio rendering interface structure
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct r_studio_interface_s
    {
        public int version;
        public delegate* unmanaged[Cdecl]<int, int> StudioDrawModel;
        public delegate* unmanaged[Cdecl]<int, entity_state_s*, int> StudioDrawPlayer;
    }

    // Engine Studio API structure
    // Original: typedef struct engine_studio_api_s from r_studioint.h
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct engine_studio_api_s
    {
        // Memory management
        // void* (*Mem_Calloc)(int number, size_t size);
        public delegate* unmanaged[Cdecl]<int, nuint, nint> Mem_Calloc;

        // void* (*Cache_Check)(struct cache_user_s* c);
        public delegate* unmanaged[Cdecl]<cache_user_s*, nint> Cache_Check;

        // void (*LoadCacheFile)(char* path, struct cache_user_s* cu);
        public delegate* unmanaged[Cdecl]<sbyte*, cache_user_s*, void> LoadCacheFile;

        // Model management
        // struct model_s* (*Mod_ForName)(const char* name, int crash_if_missing);
        public delegate* unmanaged[Cdecl]<sbyte*, int, model_s*> Mod_ForName;

        // void* (*Mod_Extradata)(struct model_s* mod);
        public delegate* unmanaged[Cdecl]<model_s*, nint> Mod_Extradata;

        // struct model_s* (*GetModelByIndex)(int index);
        public delegate* unmanaged[Cdecl]<int, model_s*> GetModelByIndex;

        // struct cl_entity_s* (*GetCurrentEntity)(void);
        public delegate* unmanaged[Cdecl]<cl_entity_s*> GetCurrentEntity;

        // Player info
        // struct player_info_s* (*PlayerInfo)(int index);
        public delegate* unmanaged[Cdecl]<int, player_info_s*> PlayerInfo;

        // struct entity_state_s* (*GetPlayerState)(int index);
        public delegate* unmanaged[Cdecl]<int, entity_state_s*> GetPlayerState;

        // struct cl_entity_s* (*GetViewEntity)(void);
        public delegate* unmanaged[Cdecl]<cl_entity_s*> GetViewEntity;

        // Time and frame info
        // void (*GetTimes)(int* framecount, double* current, double* old);
        public delegate* unmanaged[Cdecl]<int*, double*, double*, void> GetTimes;

        // struct cvar_s* (*GetCvar)(const char* name);
        public delegate* unmanaged[Cdecl]<sbyte*, cvar_s*> GetCvar;

        // void (*GetViewInfo)(float* origin, float* upv, float* rightv, float* vpnv);
        public delegate* unmanaged[Cdecl]<float*, float*, float*, float*, void> GetViewInfo;

        // struct model_s* (*GetChromeSprite)(void);
        public delegate* unmanaged[Cdecl]<model_s*> GetChromeSprite;

        // void (*GetModelCounters)(int** s, int** a);
        public delegate* unmanaged[Cdecl]<int**, int**, void> GetModelCounters;

        // void (*GetAliasScale)(float* x, float* y);
        public delegate* unmanaged[Cdecl]<float*, float*, void> GetAliasScale;

        // Get bone, light, alias, and rotation matrices
        // float**** (*StudioGetBoneTransform)(void);
        public delegate* unmanaged[Cdecl]<nint> StudioGetBoneTransform;

        // float**** (*StudioGetLightTransform)(void);
        public delegate* unmanaged[Cdecl]<nint> StudioGetLightTransform;

        // float*** (*StudioGetAliasTransform)(void);
        public delegate* unmanaged[Cdecl]<nint> StudioGetAliasTransform;

        // float*** (*StudioGetRotationMatrix)(void);
        public delegate* unmanaged[Cdecl]<nint> StudioGetRotationMatrix;

        // Set up body part, and get submodel pointers
        // void (*StudioSetupModel)(int bodypart, void** ppbodypart, void** ppsubmodel);
        public delegate* unmanaged[Cdecl]<int, nint*, nint*, void> StudioSetupModel;

        // int (*StudioCheckBBox)(void);
        public delegate* unmanaged[Cdecl]<int> StudioCheckBBox;

        // Apply lighting effects to model
        // void (*StudioDynamicLight)(struct cl_entity_s* ent, struct alight_s* plight);
        public delegate* unmanaged[Cdecl]<cl_entity_s*, alight_s*, void> StudioDynamicLight;

        // void (*StudioEntityLight)(struct alight_s* plight);
        public delegate* unmanaged[Cdecl]<alight_s*, void> StudioEntityLight;

        // void (*StudioSetupLighting)(struct alight_s* plighting);
        public delegate* unmanaged[Cdecl]<alight_s*, void> StudioSetupLighting;

        // Draw mesh vertices
        // void (*StudioDrawPoints)(void);
        public delegate* unmanaged[Cdecl]<void> StudioDrawPoints;

        // Draw hulls around bones
        // void (*StudioDrawHulls)(void);
        public delegate* unmanaged[Cdecl]<void> StudioDrawHulls;

        // void (*StudioDrawAbsBBox)(void);
        public delegate* unmanaged[Cdecl]<void> StudioDrawAbsBBox;

        // void (*StudioDrawBones)(void);
        public delegate* unmanaged[Cdecl]<void> StudioDrawBones;

        // void (*StudioSetupSkin)(void* ptexturehdr, int index);
        public delegate* unmanaged[Cdecl]<nint, int, void> StudioSetupSkin;

        // void (*StudioSetRemapColors)(int top, int bottom);
        public delegate* unmanaged[Cdecl]<int, int, void> StudioSetRemapColors;

        // struct model_s* (*SetupPlayerModel)(int index);
        public delegate* unmanaged[Cdecl]<int, model_s*> SetupPlayerModel;

        // void (*StudioClientEvents)(void);
        public delegate* unmanaged[Cdecl]<void> StudioClientEvents;

        // Retrieve/set forced render effects flags
        // int (*GetForceFaceFlags)(void);
        public delegate* unmanaged[Cdecl]<int> GetForceFaceFlags;

        // void (*SetForceFaceFlags)(int flags);
        public delegate* unmanaged[Cdecl]<int, void> SetForceFaceFlags;

        // void (*StudioSetHeader)(void* header);
        public delegate* unmanaged[Cdecl]<nint, void> StudioSetHeader;

        // void (*SetRenderModel)(struct model_s* model);
        public delegate* unmanaged[Cdecl]<model_s*, void> SetRenderModel;

        // Final state setup and restore for rendering
        // void (*SetupRenderer)(int rendermode);
        public delegate* unmanaged[Cdecl]<int, void> SetupRenderer;

        // void (*RestoreRenderer)(void);
        public delegate* unmanaged[Cdecl]<void> RestoreRenderer;

        // Set render origin for applying chrome effect
        // void (*SetChromeOrigin)(void);
        public delegate* unmanaged[Cdecl]<void> SetChromeOrigin;

        // True if using D3D/OpenGL
        // int (*IsHardware)(void);
        public delegate* unmanaged[Cdecl]<int> IsHardware;

        // Only called by hardware interface
        // void (*GL_StudioDrawShadow)(void);
        public delegate* unmanaged[Cdecl]<void> GL_StudioDrawShadow;

        // void (*GL_SetRenderMode)(int mode);
        public delegate* unmanaged[Cdecl]<int, void> GL_SetRenderMode;

        // Counter-Strike specific additions
        // void (*StudioSetRenderamt)(int iRenderamt);
        public delegate* unmanaged[Cdecl]<int, void> StudioSetRenderamt;

        // void (*StudioSetCullState)(int iCull);
        public delegate* unmanaged[Cdecl]<int, void> StudioSetCullState;

        // void (*StudioRenderShadow)(int iSprite, float* p1, float* p2, float* p3, float* p4);
        public delegate* unmanaged[Cdecl]<int, float*, float*, float*, float*, void> StudioRenderShadow;
    }

    // Server Studio API structure (simplified)
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct server_studio_api_s
    {
        // Memory management
        public delegate* unmanaged[Cdecl]<int, nuint, nint> Mem_Calloc;
        public delegate* unmanaged[Cdecl]<cache_user_s*, nint> Cache_Check;
        public delegate* unmanaged[Cdecl]<sbyte*, cache_user_s*, void> LoadCacheFile;
        public delegate* unmanaged[Cdecl]<model_s*, nint> Mod_Extradata;
    }

    // Server blending interface
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct sv_blending_interface_s
    {
        public int version;
        public delegate* unmanaged[Cdecl]<model_s*, float, int, Vector3*, Vector3*, byte*, byte*, int, edict_t*, void> SV_StudioSetupBones;
    }

    // Trace result structure
    [StructLayout(LayoutKind.Sequential)]
    public struct TraceResult
    {
        public qboolean fAllSolid;      // if true, plane is not valid
        public qboolean fStartSolid;    // if true, the initial point was in a solid area
        public qboolean fInOpen;
        public qboolean fInWater;
        public float flFraction;        // time completed, 1.0 = didn't hit anything
        public Vector3 vecEndPos;      // final position
        public float flPlaneDist;
        public Vector3 vecPlaneNormal; // surface normal at impact
        public nint pHit;             // entity the surface is on
        public int iHitgroup;           // 0 == generic, non zero is specific body part
    }
}

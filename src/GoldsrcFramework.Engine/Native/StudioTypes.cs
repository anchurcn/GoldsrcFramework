using System.Runtime.InteropServices;
using GoldsrcFramework.LinearMath;

namespace GoldsrcFramework.Engine.Native;

/// <summary>
/// Studio model constants
/// </summary>
public static class StudioConstants
{
    public const int MAXSTUDIOTRIANGLES = 20000;
    public const int MAXSTUDIOVERTS = 2048;
    public const int MAXSTUDIOSEQUENCES = 2048;
    public const int MAXSTUDIOSKINS = 100;
    public const int MAXSTUDIOSRCBONES = 512;
    public const int MAXSTUDIOBONES = 128;
    public const int MAXSTUDIOMODELS = 32;
    public const int MAXSTUDIOBODYPARTS = 32;
    public const int MAXSTUDIOGROUPS = 16;
    public const int MAXSTUDIOANIMATIONS = 2048;
    public const int MAXSTUDIOMESHES = 256;
    public const int MAXSTUDIOEVENTS = 1024;
    public const int MAXSTUDIOPIVOTS = 256;
    public const int MAXSTUDIOCONTROLLERS = 8;

    public const float RAD_TO_STUDIO = 32768.0f / MathF.PI;
    public const float STUDIO_TO_RAD = MathF.PI / 32768.0f;

    public const int STUDIO_VERSION = 10;
    public const int IDSTUDIOHEADER = ('T' << 24) + ('S' << 16) + ('D' << 8) + 'I'; // "IDST"
    public const int IDSTUDIOSEQHEADER = ('Q' << 24) + ('S' << 16) + ('D' << 8) + 'I'; // "IDSQ"
}

/// <summary>
/// Lighting options flags
/// </summary>
[Flags]
public enum StudioNormalFlags
{
    STUDIO_NF_FLATSHADE = 0x0001,
    STUDIO_NF_CHROME = 0x0002,
    STUDIO_NF_FULLBRIGHT = 0x0004,
    STUDIO_NF_NOMIPS = 0x0008,
    STUDIO_NF_ALPHA = 0x0010,
    STUDIO_NF_ADDITIVE = 0x0020,
    STUDIO_NF_MASKED = 0x0040
}

/// <summary>
/// Motion flags
/// </summary>
[Flags]
public enum StudioMotionFlags
{
    STUDIO_X = 0x0001,
    STUDIO_Y = 0x0002,
    STUDIO_Z = 0x0004,
    STUDIO_XR = 0x0008,
    STUDIO_YR = 0x0010,
    STUDIO_ZR = 0x0020,
    STUDIO_LX = 0x0040,
    STUDIO_LY = 0x0080,
    STUDIO_LZ = 0x0100,
    STUDIO_AX = 0x0200,
    STUDIO_AY = 0x0400,
    STUDIO_AZ = 0x0800,
    STUDIO_AXR = 0x1000,
    STUDIO_AYR = 0x2000,
    STUDIO_AZR = 0x4000,
    STUDIO_TYPES = 0x7FFF,
    STUDIO_RLOOP = 0x8000 // controller that wraps shortest distance
}

/// <summary>
/// Sequence flags
/// </summary>
[Flags]
public enum StudioSequenceFlags
{
    STUDIO_LOOPING = 0x0001
}

/// <summary>
/// Bone flags
/// </summary>
[Flags]
public enum StudioBoneFlags
{
    STUDIO_HAS_NORMALS = 0x0001,
    STUDIO_HAS_VERTICES = 0x0002,
    STUDIO_HAS_BBOX = 0x0004,
    STUDIO_HAS_CHROME = 0x0008 // if any of the textures have chrome on them
}

/// <summary>
/// Render mode enumeration
/// </summary>
public enum RenderMode
{
    kRenderNormal = 0,      // src
    kRenderTransColor = 1,  // c*a+dest*(1-a)
    kRenderTransTexture = 2, // src*a+dest*(1-a)
    kRenderGlow = 3,        // src*a+dest -- No Z buffer checks
    kRenderTransAlpha = 4,  // src*srca+dest*(1-srca)
    kRenderTransAdd = 5     // src*a+dest
}

/// <summary>
/// Render FX enumeration
/// </summary>
public enum RenderFx
{
    kRenderFxNone = 0,
    kRenderFxPulseSlow = 1,
    kRenderFxPulseFast = 2,
    kRenderFxPulseSlowWide = 3,
    kRenderFxPulseFastWide = 4,
    kRenderFxFadeSlow = 5,
    kRenderFxFadeFast = 6,
    kRenderFxSolidSlow = 7,
    kRenderFxSolidFast = 8,
    kRenderFxStrobeSlow = 9,
    kRenderFxStrobeFast = 10,
    kRenderFxStrobeFaster = 11,
    kRenderFxFlickerSlow = 12,
    kRenderFxFlickerFast = 13,
    kRenderFxNoDissipation = 14,
    kRenderFxDistort = 15,          // Distort/scale/translate flicker
    kRenderFxHologram = 16,         // kRenderFxDistort + distance fade
    kRenderFxDeadPlayer = 17,       // kRenderAmt is the player index
    kRenderFxExplode = 18,          // Scale up really big!
    kRenderFxGlowShell = 19,        // Glowing Shell
    kRenderFxClampMinScale = 20,    // Keep this sprite from getting very small (SPRITES only!)
    kRenderFxLightMultiplier = 21   // CTM !!!CZERO added to tell the studiorender that the value in iuser2 is a lightmultiplier
}

/// <summary>
/// Studio model header
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public unsafe struct studiohdr_t
{
    public int id;
    public int version;

    public fixed sbyte name[64];
    public int length;

    public Vector3 eyeposition;  // ideal eye position
    public Vector3 min;          // ideal movement hull size
    public Vector3 max;

    public Vector3 bbmin;        // clipping bounding box
    public Vector3 bbmax;

    public int flags;

    public int numbones;         // bones
    public int boneindex;

    public int numbonecontrollers; // bone controllers
    public int bonecontrollerindex;

    public int numhitboxes;      // complex bounding boxes
    public int hitboxindex;

    public int numseq;           // animation sequences
    public int seqindex;

    public int numseqgroups;     // demand loaded sequences
    public int seqgroupindex;

    public int numtextures;      // raw textures
    public int textureindex;
    public int texturedataindex;

    public int numskinref;       // replaceable textures
    public int numskinfamilies;
    public int skinindex;

    public int numbodyparts;
    public int bodypartindex;

    public int numattachments;   // queryable attachable points
    public int attachmentindex;

    public int soundtable;
    public int soundindex;
    public int soundgroups;
    public int soundgroupindex;

    public int numtransitions;   // animation node to animation node transition graph
    public int transitionindex;
}

/// <summary>
/// Header for demand loaded sequence group data
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public unsafe struct studioseqhdr_t
{
    public int id;
    public int version;

    public fixed sbyte name[64];
    public int length;
}

/// <summary>
/// Bone structure
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public unsafe struct mstudiobone_t
{
    public fixed sbyte name[32];      // bone name for symbolic links
    public int parent;                // parent bone
    public int flags;                 // ??
    public fixed int bonecontroller[6]; // bone controller index, -1 == none
    public fixed float value[6];      // default DoF values
    public fixed float scale[6];      // scale for delta DoF values
}

/// <summary>
/// Bone controller structure
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public unsafe struct mstudiobonecontroller_t
{
    public int bone;   // -1 == 0
    public int type;   // X, Y, Z, XR, YR, ZR, M
    public float start;
    public float end;
    public int rest;   // byte index value at rest
    public int index;  // 0-3 user set controller, 4 mouth
}

/// <summary>
/// Intersection boxes (hitboxes)
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public unsafe struct mstudiobbox_t
{
    public int bone;
    public int group;      // intersection group
    public Vector3 bbmin;  // bounding box
    public Vector3 bbmax;
}

/// <summary>
/// Demand loaded sequence groups
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public unsafe struct mstudioseqgroup_t
{
    public fixed sbyte label[32]; // textual name
    public fixed sbyte name[64];  // file name
    public int unused1;           // was "cache" - index pointer
    public int unused2;           // was "data" - hack for group 0
}

/// <summary>
/// Sequence descriptions
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public unsafe struct mstudioseqdesc_t
{
    public fixed sbyte label[32]; // sequence label

    public float fps;      // frames per second
    public int flags;      // looping/non-looping flags

    public int activity;
    public int actweight;

    public int numevents;
    public int eventindex;

    public int numframes;  // number of frames per sequence

    public int numpivots;  // number of foot pivots
    public int pivotindex;

    public int motiontype;
    public int motionbone;
    public Vector3 linearmovement;
    public int automoveposindex;
    public int automoveangleindex;

    public Vector3 bbmin;  // per sequence bounding box
    public Vector3 bbmax;

    public int numblends;
    public int animindex;  // mstudioanim_t pointer relative to start of sequence group data
                           // [blend][bone][X, Y, Z, XR, YR, ZR]

    public fixed int blendtype[2];    // X, Y, Z, XR, YR, ZR
    public fixed float blendstart[2]; // starting value
    public fixed float blendend[2];   // ending value
    public int blendparent;

    public int seqgroup;   // sequence group for demand loading

    public int entrynode;  // transition node at entry
    public int exitnode;   // transition node at exit
    public int nodeflags;  // transition rules

    public int nextseq;    // auto advancing sequences
}

/// <summary>
/// Event structure
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public unsafe struct mstudioevent_t
{
    public int frame;
    public int @event;
    public int type;
    public fixed sbyte options[64];
}

/// <summary>
/// Pivot structure
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public unsafe struct mstudiopivot_t
{
    public Vector3 org; // pivot point
    public int start;
    public int end;
}

/// <summary>
/// Attachment structure
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public unsafe struct mstudioattachment_t
{
    public fixed sbyte name[32];
    public int type;
    public int bone;
    public Vector3 org;        // attachment point
    public Vector3 vectors_0;  // vectors[0]
    public Vector3 vectors_1;  // vectors[1]
    public Vector3 vectors_2;  // vectors[2]
}

/// <summary>
/// Animation structure
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public unsafe struct mstudioanim_t
{
    public fixed ushort offset[6];
}

/// <summary>
/// Animation frames
/// </summary>
[StructLayout(LayoutKind.Explicit)]
public unsafe struct mstudioanimvalue_t
{
    [FieldOffset(0)]
    public mstudioanimvalue_num num;

    [FieldOffset(0)]
    public short value;
}

[StructLayout(LayoutKind.Sequential)]
public struct mstudioanimvalue_num
{
    public byte valid;
    public byte total;
}

/// <summary>
/// Body part index
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public unsafe struct mstudiobodyparts_t
{
    public fixed sbyte name[64];
    public int nummodels;
    public int @base;
    public int modelindex; // index into models array
}

/// <summary>
/// Skin info
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public unsafe struct mstudiotexture_t
{
    public fixed sbyte name[64];
    public int flags;
    public int width;
    public int height;
    public int index;
}

/// <summary>
/// Studio models
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public unsafe struct mstudiomodel_t
{
    public fixed sbyte name[64];

    public int type;

    public float boundingradius;

    public int nummesh;
    public int meshindex;

    public int numverts;       // number of unique vertices
    public int vertinfoindex;  // vertex bone info
    public int vertindex;      // vertex Vector
    public int numnorms;       // number of unique surface normals
    public int norminfoindex;  // normal bone info
    public int normindex;      // normal Vector

    public int numgroups;      // deformation groups
    public int groupindex;
}

/// <summary>
/// Meshes
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public unsafe struct mstudiomesh_t
{
    public int numtris;
    public int triindex;
    public int skinref;
    public int numnorms;   // per mesh normals
    public int normindex;  // normal Vector
}


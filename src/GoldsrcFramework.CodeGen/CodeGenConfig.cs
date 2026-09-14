using System.Text.Json;
using System.Text.Json.Serialization;

namespace GoldsrcFramework.CodeGen;

/// <summary>
/// The codegen.json configuration model. Paths are relative to the repository root
/// (or, for HLSDK-internal entries, to the HLSDK checkout) unless absolute.
/// The property defaults mirror codegen.json, so the generator also works when the
/// config file is absent.
/// </summary>
internal sealed class CodeGenConfig
{
    public string Hlsdk { get; set; } = "external/hlsdk";

    public string VcxProject { get; set; } = "projects/vs2019/hl_cdll.vcxproj";
    public string VcxConfiguration { get; set; } = "Release";
    public string VcxPlatform { get; set; } = "Win32";

    public string Output { get; set; } = "src/GoldsrcFramework.Engine/Native/Generated/HlsdkNative.generated.cs";

    /// <summary>Unified humanizer rules file (all manual adjustment of generated symbols).</summary>
    public string HumanizerRules { get; set; } = "src/GoldsrcFramework.CodeGen/humanizer.rules";

    public List<string> RootStructs { get; set; } =
    [
        "cldll_func_t", "cl_enginefunc_t", "enginefuncs_t", "DLL_FUNCTIONS", "NEW_DLL_FUNCTIONS", "globalvars_t", "entvars_t"
    ];

    public List<string> ManagedNativeApiRootTypes { get; set; } =
    [
        "studiohdr_t", "mstudiobonecontroller_t", "mstudiobone_t", "mstudioseqdesc_t", "mstudioseqgroup_t",
        "mstudiobodyparts_t", "mstudioattachment_t", "mstudiobbox_t", "mstudiotexture_t", "mstudiomodel_t",
        "mstudiomesh_t", "mstudiopivot_t", "mstudioanim_t", "mstudioanimvalue_t",
        "server_studio_api_t", "sv_blending_interface_t"
    ];

    public List<string> AbiHeaders { get; set; } =
    [
        "cl_dll/cl_dll.h", "engine/cdll_int.h", "engine/APIProxy.h", "cl_dll/kbutton.h",
        "engine/eiface.h", "cl_dll/Exports.h",
        "$repo/src/GoldsrcFramework.CodeGen/HlsdkSupplementalTypes.h.txt"
    ];

    public List<string> IncludeDirs { get; set; } =
    [
        "dlls", "cl_dll", "cl_dll/particleman", "public", "common", "pm_shared", "engine", "utils/vgui/include", "game_shared", "external"
    ];

    public List<string> Defines { get; set; } =
    [
        "WIN32", "_WINDOWS", "CLIENT_DLL", "CLIENT_WEAPONS", "HL_DLL", "_WINDLL",
        "_CRT_SECURE_NO_WARNINGS", "__PRFCHWINTRIN_H", "DLLEXPORT=", "EXPORT="
    ];

    public List<string> ParserArguments { get; set; } = ["-std=c++17", "-fms-extensions"];

    static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>Loads the configuration from a JSON file; throws with a clear message on parse errors.</summary>
    public static CodeGenConfig Load(string path)
    {
        try
        {
            var config = JsonSerializer.Deserialize<CodeGenConfig>(File.ReadAllText(path), JsonOptions);
            if (config is not null) return config;
            throw new InvalidOperationException($"Configuration file is empty: {path}");
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"Invalid configuration file '{path}': {ex.Message}", ex);
        }
    }

    /// <summary>Resolves a config path entry: "$repo/"-prefixed or absolute as-is, otherwise against the given base directory.</summary>
    public static string ResolvePath(string repoRoot, string baseDir, string value)
    {
        if (Path.IsPathRooted(value)) return value;
        if (value.StartsWith("$repo/", StringComparison.OrdinalIgnoreCase)) return Path.GetFullPath(Path.Combine(repoRoot, value[6..]));
        return Path.GetFullPath(Path.Combine(baseDir, value));
    }
}

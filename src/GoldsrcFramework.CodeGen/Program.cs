using CppAst;

namespace GoldsrcFramework.CodeGen
{
    class FuncInfo
    {
        public string FuncDecl { get; set; }
        public string ReturnTypeName { get; set; }
        public string FuncName { get; set; }
        public List<(string Type,string Name)> Parameter { get; set; }

    }

    class Program
    {
        static List<string> _assignList = @"Initialize,
			HUD_Init,
			HUD_VidInit,
			HUD_Redraw,
			HUD_UpdateClientData,
			HUD_Reset,
			HUD_PlayerMove,
			HUD_PlayerMoveInit,
			HUD_PlayerMoveTexture,
			IN_ActivateMouse,
			IN_DeactivateMouse,
			IN_MouseEvent,
			IN_ClearStates,
			IN_Accumulate,
			CL_CreateMove,
			CL_IsThirdPerson,
			CL_CameraOffset,
			KB_Find,
			CAM_Think,
			V_CalcRefdef,
			HUD_AddEntity,
			HUD_CreateEntities,
			HUD_DrawNormalTriangles,
			HUD_DrawTransparentTriangles,
			HUD_StudioEvent,
			HUD_PostRunCmd,
			HUD_Shutdown,
			HUD_TxferLocalOverrides,
			HUD_ProcessPlayerState,
			HUD_TxferPredictionData,
			Demo_ReadBuffer,
			HUD_ConnectionlessPacket,
			HUD_GetHullBounds,
			HUD_Frame,
			HUD_Key_Event,
			HUD_TempEntUpdate,
			HUD_GetUserEntity,
			HUD_VoiceStatus,
			HUD_DirectorMessage,
			HUD_GetStudioModelInterface,
			HUD_ChatInputPosition".Split(",").Select(x=>x.Trim()).ToList();

        static string _repoDir = "../../../../..";
        static string _hlsdkDir = Path.Combine(_repoDir, "external/hlsdk");
        static string includes = @"..\..\dlls;..\..\cl_dll;..\..\cl_dll\particleman;..\..\public;..\..\common;..\..\pm_shared;..\..\engine;..\..\utils\vgui\include;..\..\game_shared;..\..\external;..\..\LearnOpenGL\includes;";
        static IEnumerable<string> includeList = includes.Split(';', StringSplitOptions.RemoveEmptyEntries).Select(x => Path.Combine(_hlsdkDir, "projects/vs2019", x))!;
        static List<FuncInfo> _funcDeclList;

        static void Main(string[] args)
        {
            // ClientDllExports
            var outputContent = @"using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace GoldsrcFramework
{
    /// <summary>
    /// Client dll implement this abstract class for client game logic.
    /// </summary>
    public abstract class ClientFuncs
    {
        [[ClientFuncs]]
    }

    internal unsafe static class ClientDllExportsInternal
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct clfuncs
        {
            [[clfuncs]]
        }

        static ClientFuncs s_client = null!;
        [UnmanagedCallersOnly]
        static void F(clfuncs* pv)
        {
            s_client = null!;

            clfuncs v = new clfuncs()
            {
                [[ClFuncsAssignment]]
            };

            *pv = v;
        }

        [[ClientDllExportsInternal]]
    }
}
";

            // Parse
            var options = new CppParserOptions();
            options.IncludeFolders.AddRange(includeList);
            //options.SystemIncludeFolders.Add(@"C:\Program Files\Microsoft Visual Studio\2022\Community\VC\Tools\MSVC\14.38.33130\include");
            //options.SystemIncludeFolders.Add(@"C:\Program Files (x86)\Windows Kits\10\Include\10.0.22621.0\ucrt");
            options.ParseSystemIncludes = true;
            options.ParseTokenAttributes = true;
            options.AdditionalArguments.Add("-std=c++17");
            options.AdditionalArguments.Add("CLIENT_DLL");
            options.AdditionalArguments.Add("WIN32");
            options.ParseMacros = true;
            options.ConfigureForWindowsMsvc(CppTargetCpu.X86, CppVisualStudioVersion.VS2022);

            var compilation = CppParser.ParseFiles(new List<string>() {
                Path.Combine(_hlsdkDir,"common/Platform.h"),
                Path.Combine(_hlsdkDir,"cl_dll/cdll_int.cpp"),
                Path.Combine(_hlsdkDir,"cl_dll/Exports.h"),
            }, options);

            if (compilation.HasErrors)
            {
                foreach (var i in compilation.Diagnostics.Messages)
                {
                    Console.WriteLine(i);
                }
                return;
            }

            var exportsFuncs = compilation.Functions.Where(x => x.SourceFile.Contains("Exports.h")).ToList();
            // ClientFuncs
            var clientFuncs = string.Empty;
            var funcDeclList = new List<FuncInfo>();

            //exportsFuncs.First().
            foreach (var i in exportsFuncs)
            {
                var retType = GetCSharpTypeName(i.ReturnType);
                var funcName = i.Name;
                var argDeclarations = i.Parameters.Select(x => $"{GetCSharpTypeName(x.Type)} {x.Name}");

                var funcInfo = new FuncInfo()
                {
                    FuncDecl = $"{retType} {funcName} ({string.Join(",", argDeclarations)})",
                    ReturnTypeName = retType,
                    Parameter = i.Parameters.Select(x => (GetCSharpTypeName(x.Type), x.Name)).ToList(),
                    FuncName = funcName
                };
                funcDeclList.Add(funcInfo);
                string line = $"public abstract {funcDeclList.Last().FuncDecl};";

                clientFuncs += line + Environment.NewLine;
            }
            _funcDeclList = funcDeclList;
            outputContent = outputContent.Replace("[[ClientFuncs]]", clientFuncs);

            // [[clfuncs]]
            var clFuncs = string.Empty;
            foreach (var item in _assignList)
            {
                var i = exportsFuncs.Single(x => x.Name == item);
                var retType = GetCSharpTypeName(i.ReturnType);
                var funcName = i.Name;
                var genericParamList = i.Parameters.Select(x => GetCSharpTypeName(x.Type)).ToList();
                genericParamList.Add(GetCSharpTypeName(i.ReturnType));
                string line = $"public delegate* unmanaged[Cdecl] <{string.Join(",", genericParamList)}> {funcName};";

                clFuncs += line + Environment.NewLine;
            }
            outputContent = outputContent.Replace("[[clfuncs]]", clFuncs);

            // [[ClFuncsAssignment]]
            var assignment = string.Empty;
            foreach (var i in _assignList)
            {
                assignment += $"{i} = &{i}{Environment.NewLine}";
            }
            outputContent = outputContent.Replace("[[ClFuncsAssignment]]", assignment);

            // [[ClientDllExportsInternal]]
            var exportsDecl = string.Empty;
            foreach (var i in funcDeclList)
            {
                var content = "[UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]" + Environment.NewLine
                              + $"static {i.FuncDecl}"
                              + "{" + Environment.NewLine
                              + $"{(i.ReturnTypeName == "void" ? "" : "return")} s_client.{i.FuncName}({string.Join(",", i.Parameter.Select(x => x.Name))});" + Environment.NewLine
                              + "}" + Environment.NewLine;

                exportsDecl += content;
            }
            outputContent = outputContent.Replace("[[ClientDllExportsInternal]]", exportsDecl);

            GenerateLegacyClientExports();
            GenerateLegacyClientDll();
        }

        static void GenerateLegacyClientExports()
        {
            var outputContent = @"using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GoldsrcFramework.Demo
{
    internal class LegacyClientFuncs : ClientFuncs
    {
        [[LegacyClientFuncs]]
    }
}
";

            var exportsDecl = string.Empty;
            foreach (var i in _funcDeclList)
            {
                var content = $"public override {i.FuncDecl}" + Environment.NewLine
                              + "{" + Environment.NewLine
                              + $"{(i.ReturnTypeName == "void" ? "" : "return")} LegacyClientInterop.{i.FuncName}({string.Join(",", i.Parameter.Select(x => x.Name))});" + Environment.NewLine
                              + "}" + Environment.NewLine;

                exportsDecl += content;
            }

            outputContent = outputContent.Replace("[[LegacyClientFuncs]]", exportsDecl);
        }

        static void GenerateLegacyClientDll()
        {
            var outputContent = @"using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace GoldsrcFramework.Demo
{
    internal static class LegacyClientInterop
    {
        const string LegacyClientDll = ""client.dll"";

        [[LegacyClientDll]]
    }
}
";

            var exportsDecl = string.Empty;
            foreach (var i in _funcDeclList)
            {
                var content = "[DllImport(LegacyClientDll)]" + Environment.NewLine
                            + $"internal extern static {i.FuncDecl};" + Environment.NewLine;

                exportsDecl += content;
            }

            outputContent = outputContent.Replace("[[LegacyClientDll]]", exportsDecl);
        }

        static string GetCSharpTypeName(CppType type) 
        {
            var result = string.Empty;
            if (type.TypeKind == CppTypeKind.Pointer)
            {
                result = "void*";
            }
            else if (type.SizeOf == 0)
            {
                result = "void";
            }
            else
            {
                result = type.SizeOf switch
                {
                    1 => "byte",
                    2 => "short",
                    4 => "int",
                    8 => "long",
                    _ => throw new Exception()
                };
            }
            return result;
        }
    }
}

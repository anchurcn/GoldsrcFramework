using CppAst;
using CppAst.CodeGen.Common;
using CppAst.CodeGen.CSharp;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Zio;
using Zio.FileSystems;

namespace GoldsrcFramework.CodeGen
{
    class FuncInfo
    {
        public string FuncDecl { get; set; }
        public string ReturnTypeName { get; set; }
        public string FuncName { get; set; }
        public List<(string Type,string Name)> Parameter { get; set; }

    }
    // 实现一个会忽略已经入队过的项的队列
    public class TodoList<T> : Queue<T>
    {
        private HashSet<T> _set = new HashSet<T>();
        public IEnumerable<T> GetHistory() => _set;
        public new void Enqueue(T item)
        {
            if (!_set.Contains(item))
            {
                base.Enqueue(item);
                _set.Add(item);
            }
        }
        public new T Dequeue()
        {
            var item = base.Dequeue();
            return item;
        }
    }
    class NamingConverter : ICSharpConverterPlugin
    {
        public void Register(CSharpConverter converter, CSharpConverterPipeline pipeline)
        {
            pipeline.GetCSharpNameResolvers.Add(GetName);
        }

        private string GetName(CSharpConverter converter, CppElement element, CSharpElement context)
        {
            // Process with previous converter first.
            // IF Starts with __Anonymous then we find name from typedef <anoymousType> <actualName>.
            var name = DefaultGetCSharpNamePlugin.DefaultGetCSharpName(converter, element, context);
            if (name.StartsWith("__Anonymous"))
            {
                var typedef = converter.CurrentCppCompilation.Typedefs
                    .FirstOrDefault(x=>x.ElementType == element);
                if (typedef != null)
                {
                    name = typedef.Name;
                }
            }
            return name;

        }
    }
    class TypedefForwardingConverter : ICSharpConverterPlugin
    {
        public void Register(CSharpConverter converter, CSharpConverterPipeline pipeline)
        {
            pipeline.TypedefConverters.Add(TypedefForwarding);
        }
        public CSharpElement TypedefForwarding(CSharpConverter converter, CppTypedef cppTypedef, CSharpElement context)
        {
            //throw new NotImplementedException();
            //return DefaultTypeConverter.GetCSharpType(converter, cppTypedef.ElementType, context, false);
            return converter.ConvertType(cppTypedef.ElementType, context);
        }
    }
    class DiscardConverter : ICSharpConverterPlugin
    {
        TodoList<CppElement> _todo = new TodoList<CppElement>();
        void AddNestedTypeFor(CppElement e)
        {
            var nestedTypes = e switch
            {
                CppFunction f => f.Children().Cast<CppParameter>().Select(x => x.Type).Append(f.ReturnType),
                CppClass c => c.Fields.Select(x => x.Type),
                CppTypedef d => new[] { d.ElementType },
                CppFunctionType ft => ft.Parameters.Select(x => x.Type).Append(ft.ReturnType),
                CppArrayType a => new[] { a.ElementType },
                CppPointerType p => new[] { p.ElementType },
                CppPrimitiveType i => new CppType[] { },
                CppQualifiedType q => new[] { q.ElementType },
                CppEnum cppEnum => new CppType[] {} ,
                _ => throw new NotImplementedException()
            };
            foreach (var i in nestedTypes)
            {
                _todo.Enqueue(i);
            }
        }
        public void Register(CSharpConverter converter, CSharpConverterPipeline pipeline)
        {
            pipeline.ConvertBegin.Add(FilterFunction);
        }
        public void FilterFunction(CSharpConverter converter)
        {
            return;
            var cppComp = converter.CurrentCppCompilation;
            var exportsFuncs = cppComp.Functions.Where(x => x.SourceFile.Contains("Exports.h")).ToList();
            foreach (var i in exportsFuncs)
            {
                _todo.Enqueue(i);
            }
            var eng1 = cppComp.Classes.Where(x => x.Name == "cl_enginefuncs_ss").ToList();
            _todo.Enqueue(eng1.First());
            var toBeProcessedElements = new List<CppElement>();
            while (_todo.TryDequeue(out var ele))
            {
                toBeProcessedElements.Add(ele);
                AddNestedTypeFor(ele);
            }

            var structs = toBeProcessedElements.Where(x=>x is CppClass).Cast<CppClass>()
                .Where(x=>x.Name.Contains("triangleapi")).ToList();

            var allElements = converter.CurrentCppCompilation.Children().OfType<CppElement>().Where(x=>x is not null);
            foreach (var i in allElements)
            {
                if (!toBeProcessedElements.Contains(i))
                {
                    converter.Discard(i);
                }
            }
        }
    }
    class Program
    {
        #region demo
        static string demoCppCode = @"
typedef unsigned char byte;
typedef byte BYTEA;
typedef int (*pfnUserMsgHook)(const char *pszName, int iSize, void *pbuf);
typedef struct{
    byte a;
    byte b;
    BYTEA c;
} color_t;
struct {
    int a;
    int b;
    pfnUserMsgHook pfnHook;
    void (*ptr)(int arg0, int arg1, void (*arg2)(int arg3));

    union
    {
        int c;
        int d;
    } e;
} outer;
            ";
        #endregion
        public static List<string> GetEngineDefHeaders()
        {
            var list = new List<string>() { "common", "engine" };

            return includeList.Where(x => list.Any(l => x.Contains(l)))
                .SelectMany(x => Directory.GetFiles(x, "*.h", SearchOption.AllDirectories))
                .Where(x => !x.EndsWith("anorms.h"))
                .ToList();
        }
        static void Code()
        {
            var options = new CSharpConverterOptions();

            var csCompilation = CSharpConverter.Convert(demoCppCode, options);

            Debug.Assert(csCompilation.HasErrors == false);
            var fs = new MemoryFileSystem();
            var codeWriter = new CodeWriter(new CodeWriterOptions(fs));
            csCompilation.DumpTo(codeWriter);

            var text = fs.ReadAllText(options.DefaultOutputFilePath);
            Console.WriteLine(text);
        }
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
        static string _hlsdkDir = "D:\\_Project\\GoldsrcFramework\\external\\halflife-updated"; //Path.Combine(_repoDir, "external/halflife-updated");
        static string includes = @"..\..\dlls;..\..\cl_dll;..\..\cl_dll\particleman;..\..\public;..\..\common;..\..\pm_shared;..\..\engine;..\..\utils\vgui\include;..\..\game_shared;..\..\external;..\..\LearnOpenGL\includes;";
        static IEnumerable<string> includeList = includes.Split(';', StringSplitOptions.RemoveEmptyEntries).Select(x => Path.Combine(_hlsdkDir, "projects/vs2019", x))!;
        static List<FuncInfo> _funcDeclList;

        static void Main(string[] args)
        {
            //Code();
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
            var options = new CSharpConverterOptions();
            options.IncludeFolders.AddRange(includeList);
            var msvcVersion = "14.44.35207"; // 或从注册表获取最新版本
            var windowsKitVersion = "10.0.22621.0"; // 或从注册表获取

            //options.SystemIncludeFolders.Add(Path.Combine(vsPath, "VC", "Tools", "MSVC", msvcVersion, "include"));

            options.SystemIncludeFolders.Add($@"C:\Program Files (x86)\Windows Kits\10\Include\{windowsKitVersion}\ucrt");
            options.SystemIncludeFolders.Add($@"C:\Program Files (x86)\Windows Kits\10\Include\{windowsKitVersion}\shared");
            options.SystemIncludeFolders.Add($@"C:\Program Files (x86)\Windows Kits\10\Include\{windowsKitVersion}\um");
            //options.SystemIncludeFolders.Add(@"C:\Program Files\Microsoft Visual Studio\2022\Community\VC\Tools\MSVC\14.38.33130\include");
            //C:\Program Files\Microsoft Visual Studio\2022\Community\VC\Tools\MSVC\14.29.30133\include\vcruntime.h
            options.SystemIncludeFolders.Add(@"C:\Program Files\Microsoft Visual Studio\2022\Community\VC\Tools\MSVC\14.29.30133\include");
            //options.SystemIncludeFolders.Add(@"C:\Program Files (x86)\Windows Kits\10\Include\10.0.22621.0\ucrt");
            options.ParseSystemIncludes = true;
            options.ParseTokenAttributes = true;
            options.AdditionalArguments.Add("-std=c++17");
            options.Defines.Add("WIN32");
            options.Defines.Add("_CRT_SECURE_NO_WARNINGS");
            options.Defines.Add("_DEBUG");
            options.Defines.Add("_WINDOWS");
            options.Defines.Add("CLIENT_DLL");
            options.Defines.Add("CLIENT_WEAPONS");
            options.Defines.Add("HL_DLL");
            options.Defines.Add("_WINDLL");
            options.ParseMacros = true;
            options.AutoSquashTypedef = false;
            options.ConfigureForWindowsMsvc(CppTargetCpu.X86, CppVisualStudioVersion.VS2019);

            #region Conversion config
            options.MappingRules.Add(x => x.Map<CppClass>("byte").Type("byte"));
            #endregion

            options.Plugins.Add(new DiscardConverter());
            options.Plugins.Add(new TypedefForwardingConverter());
            options.Plugins.Add(new NamingConverter());
            //var csc = CSharpConverter.Convert(new List<string>() {
            //    Path.Combine(_hlsdkDir,"common/Platform.h"),
            //    Path.Combine(_hlsdkDir,"cl_dll/cdll_int.cpp"),
            //    Path.Combine(_hlsdkDir,"cl_dll/Exports.h"),
            //    // APIProxy.h
            //    Path.Combine(_hlsdkDir,"engine/APIProxy.h"),
            //    Path.Combine(_hlsdkDir,"common/triangleapi.h"),
            //    Path.Combine(_hlsdkDir,"common/r_efx.h"),
            //    Path.Combine(_hlsdkDir, "common/com_model.h"),
            //}.Concat(GetEngineDefHeaders()).ToList(), options);
            var csc = CSharpConverter.Convert(demoCppCode, options);


            var fs = new MemoryFileSystem();
            var codeWriter = new CodeWriter(new CodeWriterOptions(fs));
            csc.DumpTo(codeWriter);

            var text = fs.ReadAllText(options.DefaultOutputFilePath);
            File.WriteAllText("goldsrc_engine_def_output.cs", text);
            Console.WriteLine(text);

            var compilation = CppParser.ParseFiles(new List<string>() {
                Path.Combine(_hlsdkDir,"common/Platform.h"),
                Path.Combine(_hlsdkDir,"cl_dll/cdll_int.cpp"),
                Path.Combine(_hlsdkDir,"cl_dll/Exports.h"),
                Path.Combine(_hlsdkDir,"engine/APIProxy.h"),
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

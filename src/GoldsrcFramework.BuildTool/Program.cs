using System.Reflection;
using System.Text;
using GoldsrcFramework.Entity;

namespace GoldsrcFramework.BuildTool
{
    internal class Program
    {
        static int Main(string[] args)
        {
            try
            {
                string dllPath;
                string outputPath;
                string outputFormat = "cpp"; // Default to cpp format

                // Parse arguments with defaults
                if (args.Length == 0)
                {
                    // Default: load from current directory, output to current directory
                    dllPath = "GoldsrcFramework.dll";
                    outputPath = "entity_exports.cpp";
                }
                else if (args.Length == 1)
                {
                    // One argument: could be output path or format
                    if (args[0] == "cs" || args[0] == "cpp")
                    {
                        // Format specified, use default paths
                        outputFormat = args[0];
                        dllPath = "GoldsrcFramework.dll";
                        outputPath = outputFormat == "cs" ? "EntityExports.cs" : "entity_exports.cpp";
                    }
                    else
                    {
                        // Output path specified, use default DLL path and format
                        dllPath = "GoldsrcFramework.dll";
                        outputPath = args[0];
                        // Determine format from extension
                        outputFormat = Path.GetExtension(outputPath).ToLower() == ".cs" ? "cs" : "cpp";
                    }
                }
                else if (args.Length == 2)
                {
                    // Two arguments: could be DLL+output or format+output
                    if (args[0] == "cs" || args[0] == "cpp")
                    {
                        // Format and output path
                        outputFormat = args[0];
                        dllPath = "GoldsrcFramework.dll";
                        outputPath = args[1];
                    }
                    else
                    {
                        // DLL path and output path
                        dllPath = args[0];
                        outputPath = args[1];
                        // Determine format from extension
                        outputFormat = Path.GetExtension(outputPath).ToLower() == ".cs" ? "cs" : "cpp";
                    }
                }
                else if (args.Length == 3)
                {
                    // Three arguments: format, DLL path, and output path
                    outputFormat = args[0];
                    dllPath = args[1];
                    outputPath = args[2];
                }
                else
                {
                    ShowUsage();
                    return 1;
                }

                // Validate format
                if (outputFormat != "cs" && outputFormat != "cpp")
                {
                    Console.Error.WriteLine($"Error: Invalid format '{outputFormat}'. Must be 'cs' or 'cpp'.");
                    return 1;
                }

                if (!File.Exists(dllPath))
                {
                    Console.Error.WriteLine($"Error: DLL file not found: {dllPath}");
                    Console.Error.WriteLine($"Make sure GoldsrcFramework.dll is in the current directory or specify the full path.");
                    return 1;
                }

                Console.WriteLine($"Loading assembly from: {dllPath}");
                Console.WriteLine($"Output file: {outputPath}");
                Console.WriteLine($"Output format: {outputFormat}");

                // Generate entity exports
                GenerateEntityExports(dllPath, outputPath, outputFormat);

                Console.WriteLine("Entity exports generated successfully!");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error: {ex.Message}");
                return 1;
            }
        }

        static void ShowUsage()
        {
            Console.WriteLine("GoldsrcFramework.BuildTool - Entity Exports Generator");
            Console.WriteLine();
            Console.WriteLine("Usage:");
            Console.WriteLine("  GoldsrcFramework.BuildTool.exe [format] [output-path]");
            Console.WriteLine("  GoldsrcFramework.BuildTool.exe [format] <dll-path> <output-path>");
            Console.WriteLine();
            Console.WriteLine("Arguments:");
            Console.WriteLine("  format      Output format: 'cs' for C# or 'cpp' for C++ (optional, auto-detected from extension)");
            Console.WriteLine("  dll-path    Path to GoldsrcFramework.dll (optional, defaults to current directory)");
            Console.WriteLine("  output-path Path to output file (optional, defaults based on format)");
            Console.WriteLine();
            Console.WriteLine("Examples:");
            Console.WriteLine("  GoldsrcFramework.BuildTool.exe");
            Console.WriteLine("    Uses: GoldsrcFramework.dll -> entity_exports.cpp (cpp format)");
            Console.WriteLine();
            Console.WriteLine("  GoldsrcFramework.BuildTool.exe cs");
            Console.WriteLine("    Uses: GoldsrcFramework.dll -> EntityExports.cs (cs format)");
            Console.WriteLine();
            Console.WriteLine("  GoldsrcFramework.BuildTool.exe cs EntityExports.cs");
            Console.WriteLine("    Uses: GoldsrcFramework.dll -> EntityExports.cs (cs format)");
            Console.WriteLine();
            Console.WriteLine("  GoldsrcFramework.BuildTool.exe my_exports.cpp");
            Console.WriteLine("    Uses: GoldsrcFramework.dll -> my_exports.cpp (cpp format, auto-detected)");
            Console.WriteLine();
            Console.WriteLine("  GoldsrcFramework.BuildTool.exe cpp path/to/GoldsrcFramework.dll entity_exports.cpp");
            Console.WriteLine("    Uses: path/to/GoldsrcFramework.dll -> entity_exports.cpp (cpp format)");
        }

        static void GenerateEntityExports(string dllPath, string outputPath, string format)
        {
            // Load the assembly dynamically
            Assembly assembly = Assembly.LoadFrom(dllPath);

            // Get the EntityContext type
            Type? entityContextType = assembly.GetType("GoldsrcFramework.Entity.EntityContext");
            if (entityContextType == null)
            {
                throw new InvalidOperationException("EntityContext type not found in the assembly");
            }

            // Get the GetEntityList method
            MethodInfo? getEntityListMethod = entityContextType.GetMethod("GetEntityList", BindingFlags.Public | BindingFlags.Static);
            if (getEntityListMethod == null)
            {
                throw new InvalidOperationException("GetEntityList method not found in EntityContext");
            }

            // Call the method to get entity list
            string[]? entityList = getEntityListMethod.Invoke(null, null) as string[];
            if (entityList == null)
            {
                throw new InvalidOperationException("Failed to get entity list");
            }

            Console.WriteLine($"Found {entityList.Length} entities");

            // Generate the code based on format
            string code = format == "cs" ? GenerateCsCode(entityList) : GenerateCppCode(entityList);

            // Write to output file
            File.WriteAllText(outputPath, code);
        }

        static string GenerateCppCode(string[] entityList)
        {
            var sb = new StringBuilder();

            // Header comment
            sb.AppendLine("// This file is generated by GoldsrcFramework.BuildTool");
            sb.AppendLine("// It contains the actual entity export functions for your mod");
            sb.AppendLine();

            // Type definitions
            sb.AppendLine("typedef struct entvars_s entvars_t;");
            sb.AppendLine("typedef void (_cdecl *PrivateDataAllocatorFunc)(entvars_t* pev);");
            sb.AppendLine("void* GetPrivateDataAllocator(const char* const pszEntityClassName);");
            sb.AppendLine();

            // Struct definition
            sb.AppendLine("// Entity private data allocation function table structure");
            sb.AppendLine("struct PrivateDataAllocFuncs");
            sb.AppendLine("{");
            sb.AppendLine("\t// Legacy Half-Life entities - populated dynamically");
            sb.AppendLine("\t// Function pointers will be set during initialization");
            sb.AppendLine("\t// GENERATED");

            foreach (string entity in entityList)
            {
                sb.AppendLine($"\tPrivateDataAllocatorFunc {entity};");
            }

            sb.AppendLine("};");
            sb.AppendLine();

            // Global instance
            sb.AppendLine("PrivateDataAllocFuncs g_allocFuncs = { 0 };");
            sb.AppendLine();

            // Initialization function
            sb.AppendLine("//InitializePrivateDataAllocators");
            sb.AppendLine("void InitializePrivateDataAllocators()");
            sb.AppendLine("{");
            sb.AppendLine("\t// Set up the function pointers for legacy Half-Life entities");
            sb.AppendLine("\t// GENERATED");

            foreach (string entity in entityList)
            {
                sb.AppendLine($"\tg_allocFuncs.{entity} = (PrivateDataAllocatorFunc)GetPrivateDataAllocator(\"{entity}\");");
            }

            sb.AppendLine("}");
            sb.AppendLine();

            // Export functions
            sb.AppendLine("// GENERATED");
            foreach (string entity in entityList)
            {
                sb.AppendLine($"extern \"C\" __declspec(dllexport) void {entity}(entvars_t* pev)");
                sb.AppendLine("{");
                sb.AppendLine($"\tg_allocFuncs.{entity}(pev);");
                sb.AppendLine("}");
                sb.AppendLine();
            }

            return sb.ToString();
        }

        static string GenerateCsCode(string[] entityList)
        {
            var sb = new StringBuilder();

            // Header comment and usings
            sb.AppendLine("// This file is generated by GoldsrcFramework.BuildTool");
            sb.AppendLine("// It contains the actual entity export functions for your mod");
            sb.AppendLine();
            sb.AppendLine("using System;");
            sb.AppendLine("using System.Diagnostics;");
            sb.AppendLine("using System.Runtime.CompilerServices;");
            sb.AppendLine("using System.Runtime.InteropServices;");
            sb.AppendLine();

            // Namespace and class declaration
            sb.AppendLine("namespace GoldsrcFramework.NetLoader");
            sb.AppendLine("{");
            sb.AppendLine("    /// <summary>");
            sb.AppendLine("    /// Generated entity exports - C# AOT version");
            sb.AppendLine("    /// This file contains entity export functions for your mod");
            sb.AppendLine("    /// </summary>");
            sb.AppendLine("    public static unsafe partial class EntityExports");
            sb.AppendLine("    {");

            // Private data allocation function table structure
            sb.AppendLine("        // Entity private data allocation function table structure");
            sb.AppendLine("        private struct PrivateDataAllocFuncs");
            sb.AppendLine("        {");
            sb.AppendLine("            // Generated entity function pointers");

            foreach (string entity in entityList)
            {
                sb.AppendLine($"            public delegate* unmanaged[Cdecl]<IntPtr, void> {entity};");
            }

            sb.AppendLine("        }");
            sb.AppendLine();

            // Static instance
            sb.AppendLine("        private static PrivateDataAllocFuncs s_allocFuncs = new PrivateDataAllocFuncs();");
            sb.AppendLine();

            // InitializePrivateDataAllocators method
            sb.AppendLine("        /// <summary>");
            sb.AppendLine("        /// Initialize private data allocators");
            sb.AppendLine("        /// This method sets up function pointers for entities");
            sb.AppendLine("        /// </summary>");
            sb.AppendLine("        public static void InitializePrivateDataAllocators()");
            sb.AppendLine("        {");
            sb.AppendLine("            // Set up the function pointers for entities");

            foreach (string entity in entityList)
            {
                sb.AppendLine($"            s_allocFuncs.{entity} = GetPrivateDataAllocatorFunctionPointer(\"{entity}\");");
            }

            sb.AppendLine("        }");
            sb.AppendLine();

            // GetPrivateDataAllocatorFunctionPointer helper method
            sb.AppendLine("        /// <summary>");
            sb.AppendLine("        /// Get a private data allocator function pointer for the specified entity class name");
            sb.AppendLine("        /// </summary>");
            sb.AppendLine("        private static delegate* unmanaged[Cdecl]<IntPtr, void> GetPrivateDataAllocatorFunctionPointer(string entityClassName)");
            sb.AppendLine("        {");
            sb.AppendLine("            try");
            sb.AppendLine("            {");
            sb.AppendLine("                // Get the allocator function pointer from the framework");
            sb.AppendLine("                IntPtr entityNamePtr = Marshal.StringToHGlobalAnsi(entityClassName);");
            sb.AppendLine("                try");
            sb.AppendLine("                {");
            sb.AppendLine("                    IntPtr allocatorPtr = Loader.GetPrivateDataAllocator(entityNamePtr);");
            sb.AppendLine("                    if (allocatorPtr != IntPtr.Zero)");
            sb.AppendLine("                    {");
            sb.AppendLine("                        // Return the function pointer directly");
            sb.AppendLine("                        return (delegate* unmanaged[Cdecl]<IntPtr, void>)allocatorPtr;");
            sb.AppendLine("                    }");
            sb.AppendLine("                }");
            sb.AppendLine("                finally");
            sb.AppendLine("                {");
            sb.AppendLine("                    Marshal.FreeHGlobal(entityNamePtr);");
            sb.AppendLine("                }");
            sb.AppendLine("            }");
            sb.AppendLine("            catch (Exception ex)");
            sb.AppendLine("            {");
            sb.AppendLine("                Debug.WriteLine($\"Failed to get allocator for {entityClassName}: {ex.Message}\");");
            sb.AppendLine("            }");
            sb.AppendLine();
            sb.AppendLine("            return null;");
            sb.AppendLine("        }");
            sb.AppendLine();

            // Entity export functions
            sb.AppendLine("        // Entity export functions - these are the actual exported functions that Half-Life will call");
            sb.AppendLine();

            foreach (string entity in entityList)
            {
                sb.AppendLine($"        [UnmanagedCallersOnly(EntryPoint = \"{entity}\", CallConvs = new[] {{ typeof(CallConvCdecl) }})]");
                sb.AppendLine($"        public static void {entity}(IntPtr pev)");
                sb.AppendLine("        {");
                sb.AppendLine($"            if (s_allocFuncs.{entity} != null)");
                sb.AppendLine($"                s_allocFuncs.{entity}(pev);");
                sb.AppendLine("        }");
                sb.AppendLine();
            }

            // Close class and namespace
            sb.AppendLine("    }");
            sb.AppendLine("}");

            return sb.ToString();
        }
    }
}

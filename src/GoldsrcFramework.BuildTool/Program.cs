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

                // Parse arguments with defaults
                if (args.Length == 0)
                {
                    // Default: load from current directory, output to current directory
                    dllPath = "GoldsrcFramework.dll";
                    outputPath = "entity_exports.cpp";
                }
                else if (args.Length == 1)
                {
                    // One argument: treat as output path, use default DLL path
                    dllPath = "GoldsrcFramework.dll";
                    outputPath = args[0];
                }
                else if (args.Length == 2)
                {
                    // Two arguments: DLL path and output path
                    dllPath = args[0];
                    outputPath = args[1];
                }
                else
                {
                    ShowUsage();
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

                // Generate entity exports
                GenerateEntityExports(dllPath, outputPath);

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
            Console.WriteLine("  GoldsrcFramework.BuildTool.exe [output-path]");
            Console.WriteLine("  GoldsrcFramework.BuildTool.exe <dll-path> <output-path>");
            Console.WriteLine();
            Console.WriteLine("Arguments:");
            Console.WriteLine("  dll-path    Path to GoldsrcFramework.dll (optional, defaults to current directory)");
            Console.WriteLine("  output-path Path to output entity_exports.cpp file (optional, defaults to entity_exports.cpp)");
            Console.WriteLine();
            Console.WriteLine("Examples:");
            Console.WriteLine("  GoldsrcFramework.BuildTool.exe");
            Console.WriteLine("    Uses: GoldsrcFramework.dll -> entity_exports.cpp");
            Console.WriteLine();
            Console.WriteLine("  GoldsrcFramework.BuildTool.exe my_exports.cpp");
            Console.WriteLine("    Uses: GoldsrcFramework.dll -> my_exports.cpp");
            Console.WriteLine();
            Console.WriteLine("  GoldsrcFramework.BuildTool.exe path/to/GoldsrcFramework.dll entity_exports.cpp");
            Console.WriteLine("    Uses: path/to/GoldsrcFramework.dll -> entity_exports.cpp");
        }

        static void GenerateEntityExports(string dllPath, string outputPath)
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

            // Generate the C++ code
            string cppCode = GenerateCppCode(entityList);

            // Write to output file
            File.WriteAllText(outputPath, cppCode);
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
            sb.AppendLine("extern \"C\" void InitializePrivateDataAllocators()");
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
    }
}

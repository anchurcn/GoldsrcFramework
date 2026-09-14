using System.Text;

namespace GoldsrcFramework.CodeGen;

/// <summary>Centralized writing of generated files with normalized line endings.</summary>
internal static class OutputWriter
{
    public static void WriteGeneratedFile(string path, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, NormalizeNewLines(content), Encoding.UTF8);
    }

    public static string NormalizeNewLines(string content)
    {
        var normalized = content.Replace("\r\n", "\n").Replace("\r", "\n");
        return Environment.NewLine == "\n" ? normalized : normalized.Replace("\n", Environment.NewLine);
    }
}

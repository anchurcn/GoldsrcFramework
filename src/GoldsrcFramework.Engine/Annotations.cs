using System;

namespace GoldsrcFramework.Engine.Annotations;

/// <summary>
/// 标记原始 C/C++ 结构体的大小信息
/// </summary>
[AttributeUsage(AttributeTargets.Struct | AttributeTargets.Class)]
public class OriginalStructSizeAttribute : Attribute
{
    public string Platform { get; }
    public int Size { get; }

    public OriginalStructSizeAttribute(string platform, int size)
    {
        Platform = platform;
        Size = size;
    }
}

/// <summary>
/// 标记原始 C/C++ 结构体的对齐信息
/// </summary>
[AttributeUsage(AttributeTargets.Struct | AttributeTargets.Class)]
public class OriginalAlignOfAttribute : Attribute
{
    public int Alignment { get; }

    public OriginalAlignOfAttribute(int alignment)
    {
        Alignment = alignment;
    }
}

/// <summary>
/// 标记原始 C/C++ 名称
/// </summary>
[AttributeUsage(AttributeTargets.All)]
public class OriginalNameAttribute : Attribute
{
    public string Name { get; }

    public OriginalNameAttribute(string name)
    {
        Name = name;
    }
}

/// <summary>
/// 标记原始 C/C++ 类型名称
/// </summary>
[AttributeUsage(AttributeTargets.Struct | AttributeTargets.Class | AttributeTargets.Enum | AttributeTargets.Field)]
public class OriginalTypeAttribute : Attribute
{
    public string TypeName { get; }

    public OriginalTypeAttribute(string typeName)
    {
        TypeName = typeName;
    }
}

/// <summary>
/// 标记原始 C/C++ 文件位置
/// </summary>
[AttributeUsage(AttributeTargets.All)]
public class OriginalSourceAttribute : Attribute
{
    public string FilePath { get; }
    public int LineNumber { get; }

    public OriginalSourceAttribute(string filePath, int lineNumber = 0)
    {
        FilePath = filePath;
        LineNumber = lineNumber;
    }
}

/// <summary>
/// 标记原始 C/C++ 函数签名
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Field)]
public class OriginalSignatureAttribute : Attribute
{
    public string Signature { get; }

    public OriginalSignatureAttribute(string signature)
    {
        Signature = signature;
    }
}

/// <summary>
/// 标记原始 C/C++ 调用约定
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Field)]
public class OriginalCallingConventionAttribute : Attribute
{
    public string Convention { get; }

    public OriginalCallingConventionAttribute(string convention)
    {
        Convention = convention;
    }
}

/// <summary>
/// 标记原始 C/C++ 常量值
/// </summary>
[AttributeUsage(AttributeTargets.Field)]
public class OriginalConstantValueAttribute : Attribute
{
    public string Value { get; }

    public OriginalConstantValueAttribute(string value)
    {
        Value = value;
    }
}

/// <summary>
/// 标记原始 C/C++ 注释
/// </summary>
[AttributeUsage(AttributeTargets.All)]
public class OriginalCommentAttribute : Attribute
{
    public string Comment { get; }

    public OriginalCommentAttribute(string comment)
    {
        Comment = comment;
    }
}

/// <summary>
/// 标记字段在原始结构体中的偏移量
/// </summary>
[AttributeUsage(AttributeTargets.Field)]
public class OriginalOffsetAttribute : Attribute
{
    public int Offset { get; }

    public OriginalOffsetAttribute(int offset)
    {
        Offset = offset;
    }
}

/// <summary>
/// 标记原始 C/C++ 预处理器定义
/// </summary>
[AttributeUsage(AttributeTargets.All)]
public class OriginalDefineAttribute : Attribute
{
    public string Define { get; }
    public string Value { get; }

    public OriginalDefineAttribute(string define, string value = "")
    {
        Define = define;
        Value = value;
    }
}

/// <summary>
/// 标记是否为原始 C/C++ 的联合体
/// </summary>
[AttributeUsage(AttributeTargets.Struct | AttributeTargets.Class)]
public class OriginalUnionAttribute : Attribute
{
    public bool IsUnion { get; }

    public OriginalUnionAttribute(bool isUnion = true)
    {
        IsUnion = isUnion;
    }
}

/// <summary>
/// 标记原始 C/C++ 数组大小
/// </summary>
[AttributeUsage(AttributeTargets.Field)]
public class OriginalArraySizeAttribute : Attribute
{
    public int Size { get; }

    public OriginalArraySizeAttribute(int size)
    {
        Size = size;
    }
}

/// <summary>
/// 标记原始 C/C++ 位域信息
/// </summary>
[AttributeUsage(AttributeTargets.Field)]
public class OriginalBitFieldAttribute : Attribute
{
    public int BitWidth { get; }
    public int BitOffset { get; }

    public OriginalBitFieldAttribute(int bitWidth, int bitOffset = 0)
    {
        BitWidth = bitWidth;
        BitOffset = bitOffset;
    }
}
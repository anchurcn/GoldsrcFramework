using System.Runtime.InteropServices;

namespace NativeInterop;

// 1 byte boolean type compatible with C++ bool (which is typically 1 byte in size)
[StructLayout(LayoutKind.Sequential, Size = 1)]
public readonly struct Bool1Byte : IEquatable<Bool1Byte>
{
    private readonly byte _value;

    public Bool1Byte(bool value)
    {
        _value = value ? (byte)1 : (byte)0;
    }

    private bool ToBoolean() => _value != 0;

    // 隐式转换操作符
    public static implicit operator Bool1Byte(bool value) => new Bool1Byte(value);
    public static implicit operator bool(Bool1Byte value) => value.ToBoolean();

    // 重写 Equals 和 GetHashCode
    public override bool Equals(object? obj)
    {
        if (obj is Bool1Byte other)
            return Equals(other);
        if (obj is bool boolValue)
            return ToBoolean() == boolValue;
        return false;
    }

    public bool Equals(Bool1Byte other)
    {
        return ToBoolean() == other.ToBoolean();
    }

    public override int GetHashCode()
    {
        return ToBoolean().GetHashCode();
    }

    public override string ToString()
    {
        return ToBoolean().ToString();
    }

    // 运算符重载
    public static bool operator ==(Bool1Byte left, Bool1Byte right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(Bool1Byte left, Bool1Byte right)
    {
        return !left.Equals(right);
    }
}

// Character type compatible with C++ char or BYTE
[StructLayout(LayoutKind.Sequential, Size = 1)]
public readonly struct NChar : IEquatable<NChar>
{
    private readonly byte _value;

    public NChar(byte value)
    {
        _value = value;
    }

    public NChar(char c)
    {
        _value = unchecked((byte)c);
    }

    // 隐式转换操作符
    public static implicit operator NChar(byte value) => new NChar(value);
    public static implicit operator byte(NChar value) => value._value;

    // 显式转换为char（因为可能有数据丢失）
    public static explicit operator char(NChar value) => (char)value._value;

    // 转换为字符串表示
    public override string ToString()
    {
        return ((char)_value).ToString();
    }

    // 重写 Equals 和 GetHashCode
    public override bool Equals(object? obj)
    {
        if (obj is NChar other)
            return Equals(other);
        if (obj is byte byteValue)
            return _value == byteValue;
        return false;
    }

    public bool Equals(NChar other)
    {
        return _value == other._value;
    }

    public override int GetHashCode()
    {
        return _value.GetHashCode();
    }

    // 运算符重载
    public static bool operator ==(NChar left, NChar right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(NChar left, NChar right)
    {
        return !left.Equals(right);
    }
}
using GoldsrcFramework.LinearMath;
using StrideMatrix = Stride.Core.Mathematics.Matrix;

namespace GoldsrcFramework.Ecs;

/// <summary>
/// Conversion helpers between GoldSrc <see cref="Matrix3x4"/> (column-vector convention,
/// translation in the last column) and Stride <see cref="StrideMatrix"/> (row-vector convention,
/// translation in the last row).
/// </summary>
public static class Matrix3x4Extensions
{
    /// <summary>
    /// Converts a GoldSrc <see cref="Matrix3x4"/> to a Stride <see cref="StrideMatrix"/>.
    /// The 3x3 rotation part is transposed (column-vector → row-vector) and the
    /// translation column becomes the translation row.
    /// </summary>
    public static StrideMatrix ToStrideMatrix(this in Matrix3x4 m)
    {
        return new StrideMatrix(
            m.M11, m.M21, m.M31, 0,
            m.M12, m.M22, m.M32, 0,
            m.M13, m.M23, m.M33, 0,
            m.M14, m.M24, m.M34, 1);
    }

    /// <summary>
    /// Converts a Stride <see cref="StrideMatrix"/> to a GoldSrc <see cref="Matrix3x4"/>.
    /// The 3x3 rotation part is transposed (row-vector → column-vector) and the
    /// translation row becomes the translation column.
    /// </summary>
    public static Matrix3x4 ToMatrix3x4(this StrideMatrix s)
    {
        var m = new Matrix3x4
        {
            M11 = s.M11, M12 = s.M21, M13 = s.M31, M14 = s.M41,
            M21 = s.M12, M22 = s.M22, M23 = s.M32, M24 = s.M42,
            M31 = s.M13, M32 = s.M23, M33 = s.M33, M34 = s.M43
        };
        return m;
    }
}

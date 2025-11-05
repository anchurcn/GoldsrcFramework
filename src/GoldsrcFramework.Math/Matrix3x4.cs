using System;
using System.Runtime.InteropServices;

namespace GoldsrcFramework.LinearMath;

/// <summary>
/// Represents a 3x4 transformation matrix (3 rows, 4 columns).
/// Used for bone transformations in GoldSrc Studio models.
/// Layout: [R00 R01 R02 Tx]
///         [R10 R11 R12 Ty]
///         [R20 R21 R22 Tz]
/// Where R is the 3x3 rotation matrix and T is the translation vector.
/// </summary>
[StructLayout(LayoutKind.Explicit)]
public unsafe struct Matrix3x4
{
    /// <summary>
    /// First row of the matrix (R00, R01, R02, Tx)
    /// </summary>
    [FieldOffset(0)]
    public Vector4 Row1;

    /// <summary>
    /// Second row of the matrix (R10, R11, R12, Ty)
    /// </summary>
    [FieldOffset(16)]
    public Vector4 Row2;

    /// <summary>
    /// Third row of the matrix (R20, R21, R22, Tz)
    /// </summary>
    [FieldOffset(32)]
    public Vector4 Row3;

    /// <summary>
    /// Raw matrix data as a flat array [3 rows × 4 columns = 12 floats]
    /// </summary>
    [FieldOffset(0)]
    public fixed float M[3 * 4];

    // Individual matrix elements (for compatibility with existing code)
    [FieldOffset(0)] public float M11;
    [FieldOffset(4)] public float M12;
    [FieldOffset(8)] public float M13;
    [FieldOffset(12)] public float M14;
    [FieldOffset(16)] public float M21;
    [FieldOffset(20)] public float M22;
    [FieldOffset(24)] public float M23;
    [FieldOffset(28)] public float M24;
    [FieldOffset(32)] public float M31;
    [FieldOffset(36)] public float M32;
    [FieldOffset(40)] public float M33;
    [FieldOffset(44)] public float M34;

    /// <summary>
    /// Gets or sets the translation component of the matrix.
    /// This corresponds to the last column: (M14, M24, M34) or (M[3], M[7], M[11])
    /// </summary>
    public Vector3 Origin
    {
        get => new Vector3 { X = M[3], Y = M[7], Z = M[11] };
        set
        {
            M[3] = value.X;
            M[7] = value.Y;
            M[11] = value.Z;
        }
    }

    /// <summary>
    /// Gets the identity matrix (rotation = identity, translation = zero)
    /// </summary>
    public static Matrix3x4 Identity
    {
        get
        {
            var result = new Matrix3x4();
            result.M[0] = 1;           // M11
            result.M[1 * 4 + 1] = 1;   // M22
            result.M[2 * 4 + 2] = 1;   // M33
            return result;
        }
    }

    /// <summary>
    /// Gets a zero matrix (all elements are zero)
    /// </summary>
    public static Matrix3x4 Zero => new Matrix3x4();

    /// <summary>
    /// Indexer to access matrix elements by linear index [0-11]
    /// </summary>
    /// <param name="i">Index (0-11)</param>
    /// <returns>Matrix element at the specified index</returns>
    public float this[int i] => M[i];

    /// <summary>
    /// Concatenates two transformation matrices.
    /// Computes: result = lhs * rhs
    /// 
    /// If lhs is the parent's world transform and rhs is the child's local transform,
    /// then result is the child's world transform.
    /// </summary>
    /// <param name="lhs">Left-hand side matrix (parent transform)</param>
    /// <param name="rhs">Right-hand side matrix (child local transform)</param>
    /// <param name="result">Output: concatenated transformation</param>
    public static void ConcatTransforms(in Matrix3x4 lhs, in Matrix3x4 rhs, out Matrix3x4 result)
    {
        Matrix3x4 res = new Matrix3x4();

        // Row 0
        res.M[0] = lhs[0 * 4 + 0] * rhs[0 * 4 + 0] + lhs[0 * 4 + 1] * rhs[1 * 4 + 0] +
            lhs[0 * 4 + 2] * rhs[2 * 4 + 0];
        res.M[1] = lhs[0 * 4 + 0] * rhs[0 * 4 + 1] + lhs[0 * 4 + 1] * rhs[1 * 4 + 1] +
            lhs[0 * 4 + 2] * rhs[2 * 4 + 1];
        res.M[2] = lhs[0 * 4 + 0] * rhs[0 * 4 + 2] + lhs[0 * 4 + 1] * rhs[1 * 4 + 2] +
            lhs[0 * 4 + 2] * rhs[2 * 4 + 2];
        res.M[3] = lhs[0 * 4 + 0] * rhs[0 * 4 + 3] + lhs[0 * 4 + 1] * rhs[1 * 4 + 3] +
            lhs[0 * 4 + 2] * rhs[2 * 4 + 3] + lhs[0 * 4 + 3];

        // Row 1
        res.M[4] = lhs[1 * 4 + 0] * rhs[0 * 4 + 0] + lhs[1 * 4 + 1] * rhs[1 * 4 + 0] +
            lhs[1 * 4 + 2] * rhs[2 * 4 + 0];
        res.M[5] = lhs[1 * 4 + 0] * rhs[0 * 4 + 1] + lhs[1 * 4 + 1] * rhs[1 * 4 + 1] +
            lhs[1 * 4 + 2] * rhs[2 * 4 + 1];
        res.M[6] = lhs[1 * 4 + 0] * rhs[0 * 4 + 2] + lhs[1 * 4 + 1] * rhs[1 * 4 + 2] +
            lhs[1 * 4 + 2] * rhs[2 * 4 + 2];
        res.M[7] = lhs[1 * 4 + 0] * rhs[0 * 4 + 3] + lhs[1 * 4 + 1] * rhs[1 * 4 + 3] +
            lhs[1 * 4 + 2] * rhs[2 * 4 + 3] + lhs[1 * 4 + 3];

        // Row 2
        res.M[8] = lhs[2 * 4 + 0] * rhs[0 * 4 + 0] + lhs[2 * 4 + 1] * rhs[1 * 4 + 0] +
            lhs[2 * 4 + 2] * rhs[2 * 4 + 0];
        res.M[9] = lhs[2 * 4 + 0] * rhs[0 * 4 + 1] + lhs[2 * 4 + 1] * rhs[1 * 4 + 1] +
            lhs[2 * 4 + 2] * rhs[2 * 4 + 1];
        res.M[10] = lhs[2 * 4 + 0] * rhs[0 * 4 + 2] + lhs[2 * 4 + 1] * rhs[1 * 4 + 2] +
            lhs[2 * 4 + 2] * rhs[2 * 4 + 2];
        res.M[11] = lhs[2 * 4 + 0] * rhs[0 * 4 + 3] + lhs[2 * 4 + 1] * rhs[1 * 4 + 3] +
            lhs[2 * 4 + 2] * rhs[2 * 4 + 3] + lhs[2 * 4 + 3];

        result = res;
    }

    /// <summary>
    /// Converts a quaternion to a 3x4 transformation matrix (rotation only, no translation).
    /// </summary>
    /// <param name="quaternion">Input quaternion</param>
    /// <param name="result">Output 3x4 matrix</param>
    public static void QuaternionMatrix(Quaternion quaternion, out Matrix3x4 result)
    {
        Matrix3x4 matrix = new Matrix3x4();

        // Row 0
        matrix.M[0 * 4 + 0] = (float)(1.0 - 2.0 * quaternion.Y * quaternion.Y - 2.0 * quaternion.Z * quaternion.Z);
        matrix.M[1 * 4 + 0] = (float)(2.0 * quaternion.X * quaternion.Y + 2.0 * quaternion.W * quaternion.Z);
        matrix.M[2 * 4 + 0] = (float)(2.0 * quaternion.X * quaternion.Z - 2.0 * quaternion.W * quaternion.Y);

        // Row 1
        matrix.M[0 * 4 + 1] = (float)(2.0 * quaternion.X * quaternion.Y - 2.0 * quaternion.W * quaternion.Z);
        matrix.M[1 * 4 + 1] = (float)(1.0 - 2.0 * quaternion.X * quaternion.X - 2.0 * quaternion.Z * quaternion.Z);
        matrix.M[2 * 4 + 1] = (float)(2.0 * quaternion.Y * quaternion.Z + 2.0 * quaternion.W * quaternion.X);

        // Row 2
        matrix.M[0 * 4 + 2] = (float)(2.0 * quaternion.X * quaternion.Z + 2.0 * quaternion.W * quaternion.Y);
        matrix.M[1 * 4 + 2] = (float)(2.0 * quaternion.Y * quaternion.Z - 2.0 * quaternion.W * quaternion.X);
        matrix.M[2 * 4 + 2] = (float)(1.0 - 2.0 * quaternion.X * quaternion.X - 2.0 * quaternion.Y * quaternion.Y);

        result = matrix;
    }

    /// <summary>
    /// Transforms a vector by this matrix (applies rotation and translation).
    /// </summary>
    /// <param name="vector">Input vector</param>
    /// <returns>Transformed vector</returns>
    public Vector3 Transform(Vector3 vector)
    {
        return new Vector3
        {
            X = M[0] * vector.X + M[1] * vector.Y + M[2] * vector.Z + M[3],
            Y = M[4] * vector.X + M[5] * vector.Y + M[6] * vector.Z + M[7],
            Z = M[8] * vector.X + M[9] * vector.Y + M[10] * vector.Z + M[11]
        };
    }

    /// <summary>
    /// Rotates a vector by this matrix (applies rotation only, no translation).
    /// </summary>
    /// <param name="vector">Input vector</param>
    /// <returns>Rotated vector</returns>
    public Vector3 Rotate(Vector3 vector)
    {
        return new Vector3
        {
            X = M[0] * vector.X + M[1] * vector.Y + M[2] * vector.Z,
            Y = M[4] * vector.X + M[5] * vector.Y + M[6] * vector.Z,
            Z = M[8] * vector.X + M[9] * vector.Y + M[10] * vector.Z
        };
    }

    /// <summary>
    /// Creates a matrix from a quaternion and a translation vector.
    /// </summary>
    /// <param name="quaternion">Rotation quaternion</param>
    /// <param name="translation">Translation vector</param>
    /// <returns>Transformation matrix</returns>
    public static Matrix3x4 FromQuaternionAndTranslation(Quaternion quaternion, Vector3 translation)
    {
        QuaternionMatrix(quaternion, out Matrix3x4 result);
        result.Origin = translation;
        return result;
    }

    /// <summary>
    /// Returns a string representation of the matrix.
    /// </summary>
    public override string ToString()
    {
        return $"[{M11:F3}, {M12:F3}, {M13:F3}, {M14:F3}]" + Environment.NewLine +
               $"[{M21:F3}, {M22:F3}, {M23:F3}, {M24:F3}]" + Environment.NewLine +
               $"[{M31:F3}, {M32:F3}, {M33:F3}, {M34:F3}]";
    }
}


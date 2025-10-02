using System.Runtime.CompilerServices;

namespace GoldsrcFramework.Rendering;

/// <summary>
/// Math utilities for Studio model rendering.
/// Provides vector, matrix, and quaternion operations.
/// </summary>
public static unsafe class StudioMath
{
    /// <summary>
    /// Convert degrees to radians
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float DegToRad(float degrees) => degrees * (float)(Math.PI / 180.0);

    /// <summary>
    /// Convert radians to degrees
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float RadToDeg(float radians) => radians * (float)(180.0 / Math.PI);

    /// <summary>
    /// Angle normalize to [-180, 180]
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float AngleNormalize(float angle)
    {
        angle = (float)((angle + 360.0 * (2.0 + 1.0 / 65536.0)) % 360.0);
        if (angle > 180.0f)
            angle -= 360.0f;
        return angle;
    }

    /// <summary>
    /// Vector dot product
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float DotProduct(float* v1, float* v2)
    {
        return v1[0] * v2[0] + v1[1] * v2[1] + v1[2] * v2[2];
    }

    /// <summary>
    /// Vector cross product
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void CrossProduct(float* v1, float* v2, float* cross)
    {
        cross[0] = v1[1] * v2[2] - v1[2] * v2[1];
        cross[1] = v1[2] * v2[0] - v1[0] * v2[2];
        cross[2] = v1[0] * v2[1] - v1[1] * v2[0];
    }

    /// <summary>
    /// Vector length
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float VectorLength(float* v)
    {
        return (float)Math.Sqrt(v[0] * v[0] + v[1] * v[1] + v[2] * v[2]);
    }

    /// <summary>
    /// Normalize vector
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float VectorNormalize(float* v)
    {
        float length = VectorLength(v);
        if (length > 0.0f)
        {
            float ilength = 1.0f / length;
            v[0] *= ilength;
            v[1] *= ilength;
            v[2] *= ilength;
        }
        return length;
    }

    /// <summary>
    /// Copy vector
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void VectorCopy(float* src, float* dst)
    {
        dst[0] = src[0];
        dst[1] = src[1];
        dst[2] = src[2];
    }

    /// <summary>
    /// Subtract two vectors: out = a - b
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void VectorSubtract(float* a, float* b, float* @out)
    {
        @out[0] = a[0] - b[0];
        @out[1] = a[1] - b[1];
        @out[2] = a[2] - b[2];
    }

    /// <summary>
    /// Vector scale
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void VectorScale(float* v, float scale, float* @out)
    {
        @out[0] = v[0] * scale;
        @out[1] = v[1] * scale;
        @out[2] = v[2] * scale;
    }

    /// <summary>
    /// Vector multiply-add
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void VectorMA(float* veca, float scale, float* vecb, float* vecc)
    {
        vecc[0] = veca[0] + scale * vecb[0];
        vecc[1] = veca[1] + scale * vecb[1];
        vecc[2] = veca[2] + scale * vecb[2];
    }

    /// <summary>
    /// Quaternion to matrix conversion
    /// </summary>
    public static void QuaternionMatrix(float* q, float* matrix)
    {
        // matrix is float[3][4]
        matrix[0] = 1.0f - 2.0f * q[1] * q[1] - 2.0f * q[2] * q[2];
        matrix[1] = 2.0f * q[0] * q[1] + 2.0f * q[3] * q[2];
        matrix[2] = 2.0f * q[0] * q[2] - 2.0f * q[3] * q[1];

        matrix[4] = 2.0f * q[0] * q[1] - 2.0f * q[3] * q[2];
        matrix[5] = 1.0f - 2.0f * q[0] * q[0] - 2.0f * q[2] * q[2];
        matrix[6] = 2.0f * q[1] * q[2] + 2.0f * q[3] * q[0];

        matrix[8] = 2.0f * q[0] * q[2] + 2.0f * q[3] * q[1];
        matrix[9] = 2.0f * q[1] * q[2] - 2.0f * q[3] * q[0];
        matrix[10] = 1.0f - 2.0f * q[0] * q[0] - 2.0f * q[1] * q[1];
    }

    /// <summary>
    /// Quaternion spherical linear interpolation (SLERP)
    /// </summary>
    public static void QuaternionSlerp(float* p, float* q, float t, float* qt)
    {
        int i;
        float omega, cosom, sinom, sclp, sclq;

        // decide if one of the quaternions is backwards
        float a = 0;
        float b = 0;
        for (i = 0; i < 4; i++)
        {
            a += (p[i] - q[i]) * (p[i] - q[i]);
            b += (p[i] + q[i]) * (p[i] + q[i]);
        }
        if (a > b)
        {
            for (i = 0; i < 4; i++)
            {
                q[i] = -q[i];
            }
        }

        cosom = p[0] * q[0] + p[1] * q[1] + p[2] * q[2] + p[3] * q[3];

        if ((1.0f + cosom) > 0.00000001f)
        {
            if ((1.0f - cosom) > 0.00000001f)
            {
                omega = (float)Math.Acos(cosom);
                sinom = (float)Math.Sin(omega);
                sclp = (float)Math.Sin((1.0f - t) * omega) / sinom;
                sclq = (float)Math.Sin(t * omega) / sinom;
            }
            else
            {
                sclp = 1.0f - t;
                sclq = t;
            }
            for (i = 0; i < 4; i++)
            {
                qt[i] = sclp * p[i] + sclq * q[i];
            }
        }
        else
        {
            qt[0] = -p[1];
            qt[1] = p[0];
            qt[2] = -p[3];
            qt[3] = p[2];
            sclp = (float)Math.Sin((1.0f - t) * 0.5f * Math.PI);
            sclq = (float)Math.Sin(t * 0.5f * Math.PI);
            for (i = 0; i < 3; i++)
            {
                qt[i] = sclp * p[i] + sclq * qt[i];
            }
        }
    }

    /// <summary>
    /// Angle to quaternion conversion
    /// </summary>
    public static void AngleQuaternion(float* angles, float* quaternion)
    {
        float angle;
        float sr, sp, sy, cr, cp, cy;

        // FIXME: rescale the inputs to 1/2 angle
        angle = angles[2] * 0.5f;
        sy = (float)Math.Sin(angle);
        cy = (float)Math.Cos(angle);
        angle = angles[1] * 0.5f;
        sp = (float)Math.Sin(angle);
        cp = (float)Math.Cos(angle);
        angle = angles[0] * 0.5f;
        sr = (float)Math.Sin(angle);
        cr = (float)Math.Cos(angle);

        quaternion[0] = sr * cp * cy - cr * sp * sy; // X
        quaternion[1] = cr * sp * cy + sr * cp * sy; // Y
        quaternion[2] = cr * cp * sy - sr * sp * cy; // Z
        quaternion[3] = cr * cp * cy + sr * sp * sy; // W
    }

    /// <summary>
    /// Matrix concatenation (R = A * B)
    /// </summary>
    public static void ConcatTransforms(float* in1, float* in2, float* @out)
    {
        // in1, in2, out are float[3][4]
        @out[0] = in1[0] * in2[0] + in1[1] * in2[4] + in1[2] * in2[8];
        @out[1] = in1[0] * in2[1] + in1[1] * in2[5] + in1[2] * in2[9];
        @out[2] = in1[0] * in2[2] + in1[1] * in2[6] + in1[2] * in2[10];
        @out[3] = in1[0] * in2[3] + in1[1] * in2[7] + in1[2] * in2[11] + in1[3];
        @out[4] = in1[4] * in2[0] + in1[5] * in2[4] + in1[6] * in2[8];
        @out[5] = in1[4] * in2[1] + in1[5] * in2[5] + in1[6] * in2[9];
        @out[6] = in1[4] * in2[2] + in1[5] * in2[6] + in1[6] * in2[10];
        @out[7] = in1[4] * in2[3] + in1[5] * in2[7] + in1[6] * in2[11] + in1[7];
        @out[8] = in1[8] * in2[0] + in1[9] * in2[4] + in1[10] * in2[8];
        @out[9] = in1[8] * in2[1] + in1[9] * in2[5] + in1[10] * in2[9];
        @out[10] = in1[8] * in2[2] + in1[9] * in2[6] + in1[10] * in2[10];
        @out[11] = in1[8] * in2[3] + in1[9] * in2[7] + in1[10] * in2[11] + in1[11];
    }

    /// <summary>
    /// Matrix vector transform
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void VectorTransform(float* in1, float* in2, float* @out)
    {
        // in2 is float[3][4]
        @out[0] = DotProduct(in1, in2) + in2[3];
        @out[1] = DotProduct(in1, in2 + 4) + in2[7];
        @out[2] = DotProduct(in1, in2 + 8) + in2[11];
    }

    /// <summary>
    /// Rotate vector by matrix (no translation)
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void VectorRotate(float* in1, float* in2, float* @out)
    {
        // in2 is float[3][4]
        @out[0] = DotProduct(in1, in2);
        @out[1] = DotProduct(in1, in2 + 4);
        @out[2] = DotProduct(in1, in2 + 8);
    }

    /// <summary>
    /// Inverse rotate vector by matrix
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void VectorIRotate(float* in1, float* in2, float* @out)
    {
        // in2 is float[3][4]
        @out[0] = in1[0] * in2[0] + in1[1] * in2[1] + in1[2] * in2[2];
        @out[1] = in1[0] * in2[4] + in1[1] * in2[5] + in1[2] * in2[6];
        @out[2] = in1[0] * in2[8] + in1[1] * in2[9] + in1[2] * in2[10];
    }

    /// <summary>
    /// Convert angles to a rotation matrix.
    /// </summary>
    public static void AngleMatrix(float* angles, float* matrix)
    {
        float angle;
        float sr, sp, sy, cr, cp, cy;

        angle = angles[2] * (float)(Math.PI * 2 / 360);
        sy = (float)Math.Sin(angle);
        cy = (float)Math.Cos(angle);
        angle = angles[1] * (float)(Math.PI * 2 / 360);
        sp = (float)Math.Sin(angle);
        cp = (float)Math.Cos(angle);
        angle = angles[0] * (float)(Math.PI * 2 / 360);
        sr = (float)Math.Sin(angle);
        cr = (float)Math.Cos(angle);

        // matrix = (Z * Y) * X
        matrix[0] = cp * cy;
        matrix[1] = cp * sy;
        matrix[2] = -sp;
        matrix[4] = sr * sp * cy + cr * -sy;
        matrix[5] = sr * sp * sy + cr * cy;
        matrix[6] = sr * cp;
        matrix[8] = (cr * sp * cy + -sr * -sy);
        matrix[9] = (cr * sp * sy + -sr * cy);
        matrix[10] = cr * cp;

        matrix[3] = 0.0f;
        matrix[7] = 0.0f;
        matrix[11] = 0.0f;
    }

    /// <summary>
    /// Copy a 3x4 matrix.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void MatrixCopy(float* @in, float* @out)
    {
        for (int i = 0; i < 3; i++)
        {
            for (int j = 0; j < 4; j++)
            {
                @out[i * 4 + j] = @in[i * 4 + j];
            }
        }
    }

    /// <summary>
    /// Invert a vector (negate all components).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void VectorInverse(float* v)
    {
        v[0] = -v[0];
        v[1] = -v[1];
        v[2] = -v[2];
    }

    /// <summary>
    /// Copy a quaternion
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void QuaternionCopy(float* src, float* dst)
    {
        dst[0] = src[0];
        dst[1] = src[1];
        dst[2] = src[2];
        dst[3] = src[3];
    }

    /// <summary>
    /// Compare two null-terminated strings (C-style strcmp)
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int strcmp(byte* str1, byte* str2)
    {
        while (*str1 != 0 && *str2 != 0)
        {
            if (*str1 != *str2)
                return *str1 - *str2;
            str1++;
            str2++;
        }
        return *str1 - *str2;
    }

    /// <summary>
    /// Compare a null-terminated byte string with a C# string
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool StrEquals(byte* str1, string str2)
    {
        int i = 0;
        while (i < str2.Length && str1[i] != 0)
        {
            if (str1[i] != (byte)str2[i])
                return false;
            i++;
        }
        return i == str2.Length && str1[i] == 0;
    }
}


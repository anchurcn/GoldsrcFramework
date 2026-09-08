using Stride.Core.Mathematics;

namespace GoldsrcFramework.Ecs;

/// <summary>
/// Converts GoldSrc degree-based entity angles to Stride quaternions and back.
/// </summary>
public static class GoldsrcTransformConverter
{
    private const float DegreesToRadians = MathF.PI / 180.0f;
    private const float RadiansToDegrees = 180.0f / MathF.PI;

    public static Quaternion ToStrideRotation(Vector3 goldsrcAngles)
    {
        var pitch = -goldsrcAngles.X * DegreesToRadians;
        var yaw = goldsrcAngles.Y * DegreesToRadians;
        var roll = goldsrcAngles.Z * DegreesToRadians;

        var sp = MathF.Sin(pitch);
        var cp = MathF.Cos(pitch);
        var sy = MathF.Sin(yaw);
        var cy = MathF.Cos(yaw);
        var sr = MathF.Sin(roll);
        var cr = MathF.Cos(roll);

        // Transposed GoldSrc AngleMatrix, matching Stride's row-vector matrices.
        var matrix = new Matrix(
            cp * cy, cp * sy, -sp, 0,
            sr * sp * cy - cr * sy, sr * sp * sy + cr * cy, sr * cp, 0,
            cr * sp * cy + sr * sy, cr * sp * sy - sr * cy, cr * cp, 0,
            0, 0, 0, 1);

        return Quaternion.Normalize(Quaternion.RotationMatrix(matrix));
    }

    public static Vector3 ToGoldsrcAngles(Quaternion rotation, Vector3? previousAngles = null)
    {
        var matrix = Matrix.RotationQuaternion(Quaternion.Normalize(rotation));
        var sinPitch = Math.Clamp(-matrix.M13, -1.0f, 1.0f);
        var pitch = MathF.Asin(sinPitch);
        var cosPitch = MathF.Cos(pitch);

        float yaw;
        float roll;
        if (MathF.Abs(cosPitch) > 0.0001f)
        {
            yaw = MathF.Atan2(matrix.M12, matrix.M11);
            roll = MathF.Atan2(matrix.M23, matrix.M33);
        }
        else
        {
            yaw = MathF.Atan2(-matrix.M21, matrix.M22);
            roll = 0;
        }

        var angles = new Vector3(
            -pitch * RadiansToDegrees,
            yaw * RadiansToDegrees,
            roll * RadiansToDegrees);

        if (previousAngles is { } previous)
        {
            angles.X = NearestEquivalent(angles.X, previous.X);
            angles.Y = NearestEquivalent(angles.Y, previous.Y);
            angles.Z = NearestEquivalent(angles.Z, previous.Z);
        }

        return angles;
    }

    private static float NearestEquivalent(float angle, float reference)
    {
        return angle + 360.0f * MathF.Round((reference - angle) / 360.0f);
    }
}

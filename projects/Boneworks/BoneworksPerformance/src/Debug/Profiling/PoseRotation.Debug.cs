using System;
using System.Runtime.InteropServices;

namespace Sst.BoneworksPerformance;

internal struct PoseMatrix3x4
{
  public float M0, M1, M2, M3;
  public float M4, M5, M6, M7;
  public float M8, M9, M10, M11;
}

// Only diagnostics use rotations. Gameplay history contains position and validity only.
[StructLayout(LayoutKind.Auto)]
internal partial struct TrackedPoseValue
{
  public bool IsConnected;
  public int TrackingResult;
  public float Qx, Qy, Qz, Qw;
}

internal static partial class PoseHistoryMath
{
  public static TrackedPoseValue FromMatrix(
      PoseMatrix3x4 matrix, bool valid, bool connected, int trackingResult
  )
  {
    var result = new TrackedPoseValue
    {
      IsValid = valid,
      IsConnected = connected,
      TrackingResult = trackingResult,
      X = matrix.M3,
      Y = matrix.M7,
      Z = matrix.M11,
    };
    MatrixToQuaternion(
        matrix, out result.Qx, out result.Qy, out result.Qz, out result.Qw
    );
    return result;
  }


  private static void DebugInterpolateRotation(
      TrackedPoseValue first, TrackedPoseValue second, float amount, ref TrackedPoseValue result)
  {
    result.IsConnected = first.IsConnected && second.IsConnected;
    result.TrackingResult = amount < 0.5f ? first.TrackingResult : second.TrackingResult;
    Slerp(first.Qx, first.Qy, first.Qz, first.Qw,
        second.Qx, second.Qy, second.Qz, second.Qw, amount,
        out result.Qx, out result.Qy, out result.Qz, out result.Qw);
  }

  private static void MatrixToQuaternion(
      PoseMatrix3x4 m, out float x, out float y, out float z, out float w
  )
  {
    var trace = m.M0 + m.M5 + m.M10;
    if (trace > 0f)
    {
      var scale = (float)Math.Sqrt(trace + 1f) * 2f;
      w = 0.25f * scale;
      x = (m.M9 - m.M6) / scale;
      y = (m.M2 - m.M8) / scale;
      z = (m.M4 - m.M1) / scale;
    } else if (m.M0 > m.M5 && m.M0 > m.M10)
    {
      var scale = (float)Math.Sqrt(1f + m.M0 - m.M5 - m.M10) * 2f;
      w = (m.M9 - m.M6) / scale;
      x = 0.25f * scale;
      y = (m.M1 + m.M4) / scale;
      z = (m.M2 + m.M8) / scale;
    } else if (m.M5 > m.M10)
    {
      var scale = (float)Math.Sqrt(1f + m.M5 - m.M0 - m.M10) * 2f;
      w = (m.M2 - m.M8) / scale;
      x = (m.M1 + m.M4) / scale;
      y = 0.25f * scale;
      z = (m.M6 + m.M9) / scale;
    } else
    {
      var scale = (float)Math.Sqrt(1f + m.M10 - m.M0 - m.M5) * 2f;
      w = (m.M4 - m.M1) / scale;
      x = (m.M2 + m.M8) / scale;
      y = (m.M6 + m.M9) / scale;
      z = 0.25f * scale;
    }
    Normalize(ref x, ref y, ref z, ref w);
  }

  private static void Slerp(
      float ax, float ay, float az, float aw,
      float bx, float by, float bz, float bw, float amount,
      out float x, out float y, out float z, out float w
  )
  {
    var dot = ax * bx + ay * by + az * bz + aw * bw;
    if (dot < 0f)
    {
      dot = -dot;
      bx = -bx;
      by = -by;
      bz = -bz;
      bw = -bw;
    }
    if (dot > 0.9995f)
    {
      x = Lerp(ax, bx, amount);
      y = Lerp(ay, by, amount);
      z = Lerp(az, bz, amount);
      w = Lerp(aw, bw, amount);
      Normalize(ref x, ref y, ref z, ref w);
      return;
    }
    dot = Math.Max(-1f, Math.Min(1f, dot));
    var theta = Math.Acos(dot);
    var sinTheta = Math.Sin(theta);
    var firstWeight = Math.Sin((1.0 - amount) * theta) / sinTheta;
    var secondWeight = Math.Sin(amount * theta) / sinTheta;
    x = (float)(ax * firstWeight + bx * secondWeight);
    y = (float)(ay * firstWeight + by * secondWeight);
    z = (float)(az * firstWeight + bz * secondWeight);
    w = (float)(aw * firstWeight + bw * secondWeight);
    Normalize(ref x, ref y, ref z, ref w);
  }

  private static void Normalize(
      ref float x, ref float y, ref float z, ref float w
  )
  {
    var length = Math.Sqrt(x * x + y * y + z * z + w * w);
    if (length < 1e-12)
    {
      x = y = z = 0f;
      w = 1f;
      return;
    }
    var inverse = (float)(1.0 / length);
    x *= inverse;
    y *= inverse;
    z *= inverse;
    w *= inverse;
  }

}

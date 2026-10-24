using System;

namespace Sst.BoneworksFrameFix.Debug.Gunfly
{
  public struct ScheduledPose
  {
    public double Height, Angle, Yaw;
  }

  public static class InputSchedule
  {
    public static ScheduledPose Evaluate(string mode, double seconds)
    {
      double first = Ramp(seconds, 18, 20) - Ramp(seconds, 22, 24);
      double second = Ramp(seconds, 24, 26) - Ramp(seconds, 28, 30);
      return new ScheduledPose
      {
        Height = mode == "pitch-height" ? 1.76 - .10 * (first + second) : 1.76,
        Angle = mode == "pitch-height" ? 45 + 15 * first - 15 * second : 45,
        Yaw = mode == "yaw-turn" ? 45 * (first - second) : 0
      };
    }

    static double Ramp(double t, double from, double to) => Math.Max(0, Math.Min(1, (t - from) / (to - from)));
    // Unity yaw * pitch quaternion, converted to OpenVR handedness. Pure math;
    // safe on the pose-sampling thread without accessing Unity objects or APIs.
    public static double[] OpenVrMatrix(ScheduledPose pose)
    {
      double p = (90 - pose.Angle) * Math.PI / 360, yaw = pose.Yaw * Math.PI / 360;
      double x = -Math.Sin(p) * Math.Cos(yaw), y = -Math.Cos(p) * Math.Sin(yaw), z = -Math.Sin(p) * Math.Sin(yaw), w = Math.Cos(p) * Math.Cos(yaw);
      return new[]
      {
        1 - 2 * (y * y + z * z),
        2 * (x * y - z * w),
        2 * (x * z + y * w),
        0,
        2 * (x * y + z * w),
        1 - 2 * (x * x + z * z),
        2 * (y * z - x * w),
        pose.Height,
        2 * (x * z - y * w),
        2 * (y * z + x * w),
        1 - 2 * (x * x + y * y),
        0
      };
    }
  }
}

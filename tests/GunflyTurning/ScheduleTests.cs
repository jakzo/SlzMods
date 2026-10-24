using System;
using Sst.BoneworksFrameFix.Debug.Gunfly;

static class Test
{
  static void Near(double a, double b)
  {
    if (Math.Abs(a - b) > 1e-10)
    {
      throw new Exception($"{a} != {b}");
    }
  }

  static void Main()
  {
    foreach (var t in new[]
    {
      0d,
      17d,
      35d
    }

    )
    {
      var p = InputSchedule.Evaluate("straight", t);
      Near(p.Height, 1.76);
      Near(p.Angle, 45);
      Near(p.Yaw, 0);
    }

    Near(InputSchedule.Evaluate("yaw-turn", 19).Yaw, 22.5);
    Near(InputSchedule.Evaluate("yaw-turn", 21).Yaw, 45);
    Near(InputSchedule.Evaluate("yaw-turn", 25).Yaw, -22.5);
    Near(InputSchedule.Evaluate("yaw-turn", 27).Yaw, -45);
    var h = InputSchedule.Evaluate("pitch-height", 19);
    Near(h.Height, 1.71);
    Near(h.Angle, 52.5);
    foreach (var mode in new[]
    {
      "straight",
      "yaw-turn",
      "pitch-height"
    }

    )
    {
      for (double t = 0; t <= 40; t += .03125)
      {
        var p = InputSchedule.Evaluate(mode, t);
        if (p.Height < 1.66 - 1e-10 || p.Height > 1.76 + 1e-10)
        {
          throw new Exception("Height bound");
        }

        var m = InputSchedule.OpenVrMatrix(p);
        Near(m[7], p.Height);
        for (int r = 0; r < 3; r++)
        {
          for (int s = 0; s < 3; s++)
          {
            double dot = 0;
            for (int c = 0; c < 3; c++)
            {
              dot += m[r * 4 + c] * m[s * 4 + c];
            }

            Near(dot, r == s ? 1 : 0);
          }
        }
      }
    }

    var down = InputSchedule.OpenVrMatrix(new ScheduledPose { Height = 1.76, Angle = 0, Yaw = 0 });
    Near(down[6], 1);
    foreach (var angle in new[]
    {
      0d,
      15,
      30,
      45,
      60,
      75,
      90,
      105,
      120,
      -15
    }

    )
    {
      var m = InputSchedule.OpenVrMatrix(new ScheduledPose { Height = 1.76, Angle = angle, Yaw = 0 });
      // Undo OpenVR handedness for the Unity forward direction, retaining signed angles.
      var recovered = Math.Atan2(m[10], m[6]) * 180 / Math.PI;
      Near(recovered, angle);
    }

    var yaw = InputSchedule.OpenVrMatrix(new ScheduledPose { Height = 1.76, Angle = 90, Yaw = 90 });
    Near(yaw[2], -1);
    Console.WriteLine("Schedule endpoints, ramps, bounds, handedness and 3843 orthonormal pose matrices passed.");
  }
}

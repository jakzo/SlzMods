using System;
using System.Collections.Generic;
using System.Linq;

namespace Sst.BoneworksPerformance.Debug.Gunfly
{
  public struct Observation
  {
    public double Time, Dt;
    public long Tick;
    public double X, Y, Z, Vx, Vy, Vz, Rbx, Rby, Rbz, RawHeight, Angle;
    public double FootVx, FootVy, FootVz;
    public double FootX, FootY, FootZ, CommandHeight, CommandAngle, CommandYaw, RouteError;
    public double ControllerX, ControllerY, ControllerZ, ControllerYaw, FeetOffset, HeadX, HeadY, HeadZ, HeadFx, HeadFy, HeadFz, ChestFx, ChestFy, ChestFz, FixedTime, GameTime;
    public double LeftX, LeftY, RightX, RightY;
    public bool Sprint, Jog, MagazineContact;
    public bool VelocityValid;
  }

  public sealed class Window
  {
    public string Reason;
    public int First, Last, Frames;
    public double Seconds, Fps, PhysicsHz, Horizontal, Vertical, Lateral, Vx, Vz, RbHorizontal, RbVertical, RbX, RbZ;
    public double HorizontalSpread, VerticalSpread, LateralSpread, HorizontalDrift, VerticalDrift;
    public bool Valid => Reason == "valid_stable_flight";
  }

  public static class Validation
  {
    public static double Quantile(IEnumerable<double> values, double q)
    {
      var a = values.OrderBy(x => x).ToArray();
      if (a.Length == 0)
      {
        return double.NaN;
      }

      double p = (a.Length - 1) * q;
      int i = (int)p;
      return a[i] + (a[Math.Min(i + 1, a.Length - 1)] - a[i]) * (p - i);
    }

    public static double Median(IEnumerable<double> values) => Quantile(values, .5);
    static double Horizontal(Observation x) => Math.Sqrt(x.Vx * x.Vx + x.Vz * x.Vz);
    static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    static bool FiniteVelocity(Observation sample) => Finite(sample.Vx) && Finite(sample.Vy) && Finite(sample.Vz) && Finite(sample.Rbx) && Finite(sample.Rby) && Finite(sample.Rbz);
    public static Window Evaluate(IList<Observation> rows, int hz, double angle, double transitionEnd, bool padEnabled, double measurementSeconds = 2)
    {
      var w = new Window
      {
        Reason = "insufficient_samples"
      };
      if (hz <= 0 || !Finite(angle) || !Finite(transitionEnd) || !Finite(measurementSeconds) || measurementSeconds <= 0)
      {
        throw new ArgumentException("Validation requires positive finite rates and windows and finite angle/timing values.");
      }

      if (rows.Count < 3)
      {
        return w;
      }

      if (!Finite(rows[rows.Count - 1].Time) || !Finite(rows[0].Time))
      {
        w.Reason = "invalid_sample";
        return w;
      }

      int end = rows.Count - 1, first = end;
      while (first > 0 && rows[end].Time - rows[first].Time < measurementSeconds)
      {
        first--;
      }

      w.First = first;
      w.Last = end;
      w.Frames = end - first;
      w.Seconds = rows[end].Time - rows[first].Time;
      if (w.Seconds < measurementSeconds * .975)
      {
        return w;
      }

      w.Fps = w.Frames / w.Seconds;
      w.PhysicsHz = (rows[end].Tick - rows[first].Tick) / w.Seconds;
      if (padEnabled)
      {
        w.Reason = "launch_pad_still_enabled";
        return w;
      }

      if (rows[first].Time < transitionEnd + 5)
      {
        w.Reason = "settling_after_angle_change";
        return w;
      }

      for (int i = first + 1; i <= end; i++)
      {
        var s = rows[i];
        if (!Finite(s.Time) || !Finite(s.Dt) || s.Time <= rows[i - 1].Time || s.Tick - rows[i - 1].Tick != 1 || s.Dt < .75 / hz || s.Dt > 1.25 / hz)
        {
          w.Reason = "frame_pacing_not_perfect";
          return w;
        }

        if (!s.VelocityValid || !FiniteVelocity(s))
        {
          w.Reason = "invalid_velocity";
          return w;
        }

        if (!Finite(s.RawHeight) || !Finite(s.Angle) || Math.Abs(s.RawHeight - 1.76) > .00001 || Math.Abs(s.Angle - angle) > .1)
        {
          w.Reason = "pose_mismatch";
          return w;
        }

        if (!Finite(s.LeftX) || !Finite(s.LeftY) || !Finite(s.RightX) || !Finite(s.RightY) || Math.Abs(s.LeftX) > .00001 || Math.Abs(s.LeftY + 1) > .00001 || Math.Abs(s.RightX) > .00001 || Math.Abs(s.RightY + 1) > .00001 || !s.Sprint || !s.Jog)
        {
          w.Reason = "input_mismatch";
          return w;
        }
      }

      if (Math.Abs(w.Fps / hz - 1) > .01 || Math.Abs(w.PhysicsHz / hz - 1) > .01)
      {
        w.Reason = "rate_mismatch";
        return w;
      }

      int stableFirst = first;
      while (stableFirst > 0 && rows[end].Time - rows[stableFirst].Time < 5)
      {
        stableFirst--;
      }

      var stable = rows.Skip(stableFirst).Where(s => s.VelocityValid).ToArray();
      if (stable.Any(s => !FiniteVelocity(s)))
      {
        w.Reason = "invalid_velocity";
        return w;
      }

      if (stable.Length < hz * 4)
      {
        w.Reason = "insufficient_stability_window";
        return w;
      }

      if (stable.Count(s => s.MagazineContact) < 5)
      {
        w.Reason = "no_sustained_magazine_contact";
        return w;
      }

      var h = stable.Select(Horizontal).ToArray();
      var v = stable.Select(s => s.Vy).ToArray();
      var lateral = stable.Select(s => s.Vx).ToArray();
      double hm = Median(h), vm = Median(v);
      w.HorizontalSpread = Quantile(h, .95) - Quantile(h, .05);
      w.VerticalSpread = Quantile(v, .95) - Quantile(v, .05);
      w.LateralSpread = Quantile(lateral, .95) - Quantile(lateral, .05);
      w.HorizontalDrift = Math.Abs(Median(h.Take(h.Length / 2)) - Median(h.Skip(h.Length / 2)));
      w.VerticalDrift = Math.Abs(Median(v.Take(v.Length / 2)) - Median(v.Skip(v.Length / 2)));
      if (w.HorizontalSpread > Math.Max(.35, hm * .025) || w.HorizontalDrift > Math.Max(.12, hm * .005))
      {
        w.Reason = "horizontal_not_settled";
        return w;
      }

      if (w.VerticalSpread > Math.Max(.4, Math.Abs(vm) * .025) || w.VerticalDrift > Math.Max(.15, Math.Abs(vm) * .0075))
      {
        w.Reason = "vertical_not_settled";
        return w;
      }

      if (w.LateralSpread > Math.Max(.35, hm * .02))
      {
        w.Reason = "lateral_not_settled";
        return w;
      }

      // Freefall cannot pass the vertical stability check. Low-horizontal vertical flight is allowed.
      if (hm < .5 && Math.Abs(vm) < .5)
      {
        w.Reason = "stationary_not_flying";
        return w;
      }

      var selected = rows.Skip(first + 1).ToArray();
      w.Horizontal = Median(selected.Select(Horizontal));
      w.Vertical = Median(selected.Select(s => s.Vy));
      w.Lateral = Median(selected.Select(s => s.Vx));
      w.Vx = Median(selected.Select(s => s.Vx));
      w.Vz = Median(selected.Select(s => s.Vz));
      w.RbHorizontal = Median(selected.Select(s => Math.Sqrt(s.Rbx * s.Rbx + s.Rbz * s.Rbz)));
      w.RbVertical = Median(selected.Select(s => s.Rby));
      w.RbX = Median(selected.Select(s => s.Rbx));
      w.RbZ = Median(selected.Select(s => s.Rbz));
      w.Reason = "valid_stable_flight";
      return w;
    }
  }
}

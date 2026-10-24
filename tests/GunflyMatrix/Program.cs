using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Sst.BoneworksFrameFix.Debug.Gunfly;

class Program
{
  static List<Observation> Samples()
  {
    var r = new List<Observation>();
    for (int i = 0; i < 3600; i++)
    {
      r.Add(new Observation { Time = i / 120.0, Dt = 1 / 120.0, Tick = i, Vx = 0, Vy = -7, Vz = -23, Rbx = 0, Rby = -10, Rbz = -22, RawHeight = 1.76, Angle = 45, LeftY = -1, RightY = -1, Sprint = true, Jog = true, VelocityValid = true, MagazineContact = i % 2 == 0 });
    }

    return r;
  }

  static void Check(bool condition, string name)
  {
    if (!condition)
    {
      throw new Exception("FAILED " + name);
    }

    Console.WriteLine("PASS " + name);
  }

  static Window Evaluate(List<Observation> r) => Validation.Evaluate(r, 120, 45, 9, false);
  static void Main()
  {
    var plan = MatrixPlan.Create();
    Check(plan.Length == 320 && plan.Distinct().Count() == 320, "original320-case plan");
    Check(plan[0] == "001\tM16\t120\t45" && plan[319] == "320\trifle_MK18_Ironsights\t180\t-15", "original matrix order and IDs preserved");
    var expanded = MatrixPlan.Create(true);
    Check(expanded.Length == 400 && expanded.Take(320).SequenceEqual(plan) && expanded[320] == "321\tsmg_MP5\t120\t45" && expanded[399] == "400\tsmg_MP5K\t180\t-15", "optional SMGs append without changing rifles");
    Check(!MatrixPlan.SafeId("../escape") && !MatrixPlan.SafeId("..") && MatrixPlan.SafeId("control-1_2"), "request IDs cannot escape output directories");
    var request = Path.GetTempFileName();
    try
    {
      File.WriteAllLines(request, expanded);
      MatrixPlan.Validate(request);
      Check(true, "generated requests accepted");
      File.WriteAllLines(request, new[] { plan[0], plan[0] });
      bool rejected = false;
      try
      {
        MatrixPlan.Validate(request);
      } catch (ArgumentException)
      {
        rejected = true;
      }

      Check(rejected, "duplicate request IDs rejected");
    } finally
    {
      File.Delete(request);
    }

    var rows = Samples();
    var w = Evaluate(rows);
    Check(w.Valid && Math.Abs(w.Horizontal - 23) < 1e-9 && Math.Abs(w.Vertical + 7) < 1e-9, "settled matched-rate flight and exact medians");
    rows = Samples();
    var invalid = rows[rows.Count - 10];
    invalid.Vx = double.NaN;
    rows[rows.Count - 10] = invalid;
    Check(Evaluate(rows).Reason == "invalid_velocity", "NaN horizontal velocity rejected");
    rows = Samples();
    invalid = rows[rows.Count - 10];
    invalid.Rbz = double.PositiveInfinity;
    rows[rows.Count - 10] = invalid;
    Check(Evaluate(rows).Reason == "invalid_velocity", "infinite rigidbody velocity rejected");
    rows = Samples();
    invalid = rows[rows.Count - 10];
    invalid.RawHeight = double.NaN;
    rows[rows.Count - 10] = invalid;
    Check(Evaluate(rows).Reason == "pose_mismatch", "NaN HMD position rejected");
    rows = Samples();
    invalid = rows[rows.Count - 10];
    invalid.LeftY = double.NaN;
    rows[rows.Count - 10] = invalid;
    Check(Evaluate(rows).Reason == "input_mismatch", "NaN stick input rejected");
    rows = Samples();
    invalid = rows[rows.Count - 10];
    invalid.Dt = double.NaN;
    rows[rows.Count - 10] = invalid;
    Check(Evaluate(rows).Reason == "frame_pacing_not_perfect", "NaN frame time rejected");
    rows = Samples();
    invalid = rows[rows.Count - 400];
    invalid.Vz = double.NaN;
    rows[rows.Count - 400] = invalid;
    Check(Evaluate(rows).Reason == "invalid_velocity", "invalid velocity in preceding stability window rejected");
    rows = Samples();
    for (int i = 0; i < rows.Count; i++)
    {
      var x = rows[i];
      x.MagazineContact = false;
      rows[i] = x;
    }

    Check(Evaluate(rows).Reason == "no_sustained_magazine_contact", "terminal fall without magazine contact rejected");
    rows = Samples();
    var last = rows[rows.Count - 10];
    last.Tick -= 1;
    rows[rows.Count - 10] = last;
    Check(Evaluate(rows).Reason == "frame_pacing_not_perfect", "zero-tick and catch-up render frames rejected");
    rows = Samples();
    last = rows[rows.Count - 10];
    last.RawHeight = .8;
    rows[rows.Count - 10] = last;
    Check(Evaluate(rows).Reason == "pose_mismatch", "physical HMD pose leak rejected");
    rows = Samples();
    last = rows[rows.Count - 10];
    last.Jog = false;
    rows[rows.Count - 10] = last;
    Check(Evaluate(rows).Reason == "input_mismatch", "native sprint off rejected");
    rows = Samples();
    for (int i = 0; i < rows.Count; i++)
    {
      last = rows[i];
      last.Vy = -last.Time;
      rows[i] = last;
    }

    Check(Evaluate(rows).Reason == "vertical_not_settled", "accelerating freefall rejected");
    rows = Samples();
    for (int i = 0; i < rows.Count; i++)
    {
      last = rows[i];
      last.Vz = -last.Time;
      rows[i] = last;
    }

    Check(Evaluate(rows).Reason == "horizontal_not_settled", "horizontal acceleration rejected");
    rows = Samples();
    for (int i = 0; i < rows.Count; i++)
    {
      last = rows[i];
      last.Vz = 0;
      last.Vy = 5;
      last.Angle = 0;
      rows[i] = last;
    }

    Check(Validation.Evaluate(rows, 120, 0, 9, false).Valid, "straight-down-look vertical flight allowed");
    rows = Samples();
    Check(!Validation.Evaluate(rows, 90, 45, 9, false).Valid, "wrong requested rate rejected");
    Check(!Validation.Evaluate(rows, 120, 45, 9, true).Valid, "standing on launch pad rejected");
    Check(Validation.Median(new double[] { 1, 7, 3, 5 }) == 4, "even median interpolation");
    Console.WriteLine("All validation tests passed.");
  }
}

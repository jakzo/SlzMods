using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using MelonLoader;

namespace Sst.BoneworksPerformance.Debug.Gunfly
{
  public static class ModControl
  {
    static object mod;
    static string mode;
    static long backgroundPolls;
    static int appliedRate;
    static readonly List<Tuple<object, object>> saved = new List<Tuple<object, object>>();
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    public static void Validate(string value)
    {
      if (value != "absent" && value != "full" && value != "half")
      {
        throw new ArgumentException("Condition must be absent, full or half.");
      }

      bool loaded = MelonHandler.Mods.Any(m => m.Info.Name.IndexOf("Performance", StringComparison.OrdinalIgnoreCase) >= 0);
      if (loaded == (value == "absent"))
      {
        throw new InvalidOperationException("Absent needs the performance mod unloaded; full/half need an actual MelonLoader-loaded mod. Restart the game between conditions.");
      }
    }

    public static void Configure(string value)
    {
      Validate(value);
      mode = value;
      appliedRate = 0;
      backgroundPolls = 0;
      mod = MelonHandler.Mods.FirstOrDefault(m => m.Info.Name.IndexOf("Performance", StringComparison.OrdinalIgnoreCase) >= 0);
      if (value == "absent")
      {
        if (mod != null)
        {
          throw new InvalidOperationException("Absent condition has a performance mod loaded");
        }

        return;
      }

      if (mod == null)
      {
        throw new InvalidOperationException("Mod-on condition requires actual MelonLoader-loaded release mod");
      }

      if (value != "full" && value != "half")
      {
        throw new InvalidOperationException("Unknown mod condition");
      }

      Set("_customPhysicsTickRate", 120);
      Set("_usePhysicsRateMenu", false);
      Set("_showPerformanceHud", false);
      Set("_enableFixedUpdatePoseHistory", true);
      Set("_keepSlottedWeaponsWithPhysics", true);
    }

    static void Set(string field, object value)
    {
      var f = mod.GetType().GetField(field, Flags);
      if (f == null)
      {
        throw new MissingFieldException(field);
      }

      var entry = f.GetValue(mod);
      var property = entry.GetType().GetProperty("Value");
      if (!saved.Any(p => ReferenceEquals(p.Item1, entry)))
      {
        saved.Add(Tuple.Create(entry, property.GetValue(entry, null)));
      }

      property.SetValue(entry, value, null);
    }

    public static void SetRate(int hz)
    {
      if (mod != null && hz != appliedRate)
      {
        Set("_customPhysicsTickRate", hz);
        appliedRate = hz;
      }
    }

    public static void NoteBackgroundPose()
    {
      Interlocked.Increment(ref backgroundPolls);
    }

    public static string Describe()
    {
      var s = new StringBuilder();
      s.AppendLine("condition=" + mode + " background_synthetic_pose_polls=" + Interlocked.Read(ref backgroundPolls));
      s.AppendLine("loaded_mods=" + string.Join(",", MelonHandler.Mods.Select(m => m.Info.Name + "@" + m.Info.Version)));
      if (mod == null)
      {
        return s.ToString();
      }

      foreach (var name in new[]
      {
        "_customPhysicsTickRate",
        "_usePhysicsRateMenu",
        "_showPerformanceHud",
        "_enableFixedUpdatePoseHistory",
        "_keepSlottedWeaponsWithPhysics"
      }

      )
      {
        var entry = mod.GetType().GetField(name, Flags).GetValue(mod);
        s.AppendLine(name + "=" + entry.GetType().GetProperty("Value").GetValue(entry, null));
      }

      var history = mod.GetType().GetField("_fixedUpdatePoseHistory", Flags).GetValue(mod);
      if (history != null)
      {
        foreach (var name in new[]
        {
          "_running",
          "_appliedTicks",
          "_unavailableTicks",
          "_queryErrors",
          "_scheduleOverruns"
        }

        )
        {
          s.AppendLine(name + "=" + history.GetType().GetField(name, Flags).GetValue(history));
        }

        var buffer = history.GetType().GetField("_history", Flags).GetValue(history);
        s.AppendLine("history_samples=" + buffer.GetType().GetProperty("TotalWritten").GetValue(buffer, null));
      }

      return s.ToString();
    }

    public static void Restore()
    {
      var failures = new List<Exception>();
      foreach (var pair in saved)
      {
        try
        {
          pair.Item1.GetType().GetProperty("Value").SetValue(pair.Item1, pair.Item2, null);
        } catch (Exception error)
        {
          failures.Add(error);
        }
      }

      saved.Clear();
      mod = null;
      if (failures.Count > 0)
      {
        throw new AggregateException("Some benchmark preferences could not be restored.", failures);
      }
    }
  }
}

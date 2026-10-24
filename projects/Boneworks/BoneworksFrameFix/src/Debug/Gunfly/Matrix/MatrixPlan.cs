using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace Sst.BoneworksFrameFix.Debug.Gunfly
{
  public static class MatrixPlan
  {
    public static readonly string[] Rifles =
    {
      "M16",
      "rifle_M16_Scoped",
      "rifle_M16_Ironsights",
      "rifle_M16_LaserForegrip",
      "rifle_MK18_Holosight",
      "rifle_MK18_Sabrelake",
      "rifle_MK18_LaserForegrip",
      "rifle_MK18_Ironsights"
    };
    public static readonly int[] Rates =
    {
      120,
      90,
      144,
      180
    };
    public static readonly int[] Angles =
    {
      45,
      0,
      15,
      30,
      60,
      75,
      90,
      105,
      120,
      -15
    };
    public static bool IsSupportedGun(string prefab) => Rifles.Contains(prefab) || prefab == "smg_MP5" || prefab == "smg_MP5K";
    public static string[] Create(bool includeSmgs = false)
    {
      var lines = new List<string>();
      int id = 0;
      foreach (var gun in Rifles.Concat(includeSmgs ? new[] { "smg_MP5", "smg_MP5K" } : new string[0]))
      {
        foreach (var hz in Rates)
        {
          foreach (var angle in Angles)
          {
            lines.Add((++id).ToString("D3") + "\t" + gun + "\t" + hz + "\t" + angle);
          }
        }
      }

      return lines.ToArray();
    }

    public static bool SafeId(string value) => !string.IsNullOrWhiteSpace(value) && value != "." && value != ".." && value.All(c => char.IsLetterOrDigit(c) || c == '_' || c == '-');
    public static void Validate(string path)
    {
      var ids = new HashSet<string>();
      foreach (var line in File.ReadAllLines(path))
      {
        if (string.IsNullOrWhiteSpace(line))
        {
          continue;
        }

        var f = line.Split('\t');
        int hz;
        float angle;
        if (f.Length != 4 || !SafeId(f[0]) || !ids.Add(f[0]) || !IsSupportedGun(f[1]) || !int.TryParse(f[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out hz) || !Rates.Contains(hz) || !float.TryParse(f[3], NumberStyles.Float, CultureInfo.InvariantCulture, out angle) || !Angles.Any(a => a == angle))
        {
          throw new ArgumentException("Expected unique safe id, native gun, Hz, matrix angle separated by tabs.");
        }
      }

      if (ids.Count == 0)
      {
        throw new ArgumentException("Empty matrix request.");
      }
    }
  }
}

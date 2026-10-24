using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;

namespace Sst.BoneworksFrameFix.Debug.Gunfly
{
  public static class SceneFixture
  {
    static readonly List<GameObject> disabled = new List<GameObject>();
    static void Guard()
    {
      HarnessCleanup.RequireIdle();
    }

    public static string Prepare()
    {
      Guard();
      int boxes = 0, dispensers = 0;
      foreach (var go in UnityEngine.Object.FindObjectsOfType<GameObject>())
      {
        if (!go.scene.IsValid() || !go.activeSelf)
        {
          continue;
        }

        bool box = go.transform.parent == null && (go.name == "prop_ammoBox_med(Clone)" || go.name == "prop_ammoBox_light(Clone)");
        bool dispenser = go.transform.parent == null && go.name == "AMMODISPENSER";
        if (!box && !dispenser)
        {
          continue;
        }

        disabled.Add(go);
        go.SetActive(false);
        if (box)
        {
          boxes++;
        } else
        {
          dispensers++;
        }
      }

      return "Temporarily disabled " + boxes + " native scene ammo-box roots and " + dispensers + " ammo dispenser roots. Recoverable through SceneFixture.Restore(). Active rigidbodies=" + UnityEngine.Object.FindObjectsOfType<Rigidbody>().Length;
    }

    public static string Restore()
    {
      Guard();
      int n = 0;
      foreach (var go in disabled)
      {
        if (go)
        {
          go.SetActive(true);
          n++;
        }
      }

      disabled.Clear();
      return "Restored " + n + " scene objects.";
    }

    public static string Status() => "Temporarily disabled scene objects=" + disabled.Count;
  }
}

using System;
using System.Linq;
using HarmonyLib;
using UnityEngine;
using StressLevelZero.Rig;
using UnhollowerRuntimeLib;

namespace Sst.BoneworksFrameFix.Debug.Gunfly
{
  // Explicit entry points only. No automatic patches or MelonMod registration.
  public static class HarnessCleanup
  {
    public static void RequireIdle()
    {
      if (MatrixRunner.IsActive || ComparisonRunner.IsActive)
      {
        throw new InvalidOperationException("An experiment is active. Stop it before changing the fixture or hooks.");
      }

      foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies().Where(a => a.GetName().Name.StartsWith("Gunfly")))
      {
        foreach (var type in assembly.GetTypes().Where(t => t.Name == "Runner5" && t.Namespace != null && t.Namespace.StartsWith("Gunfly")))
        {
          if (((string)type.GetMethod("Status").Invoke(null, null)).StartsWith("active=True"))
          {
            throw new InvalidOperationException("A legacy experiment is still active.");
          }
        }
      }
    }

    internal static void RemoveHooks()
    {
      RequireIdle();
      foreach (var owner in HarmonyLib.Harmony.GetAllPatchedMethods().SelectMany(m => HarmonyLib.Harmony.GetPatchInfo(m).Owners).Distinct().Where(o => o.StartsWith("jakzo.gunfly.")).ToArray())
      {
        var h = new HarmonyLib.Harmony(owner);
        var self = typeof(HarmonyLib.Harmony).GetMethod("UnpatchSelf", Type.EmptyTypes);
        if (self != null)
        {
          self.Invoke(h, null);
        } else
        {
          typeof(HarmonyLib.Harmony).GetMethod("UnpatchAll", new[] { typeof(string) }).Invoke(h, new object[] { owner });
        }
      }

      NativeInputs.ResetInstallation();
    }

    public static string Run()
    {
      RequireIdle();
      NativeInputs.Stop();
      RemoveHooks();
      var rig = UnityEngine.Object.FindObjectOfType<RigManager>();
      if (rig)
      {
        foreach (var c in rig.GetComponentsInChildren<MonoBehaviour>(true))
        {
          var name = c.GetIl2CppType().FullName;
          if (name == typeof(MagazineCollisionObserver).FullName || (name.StartsWith("Gunfly") && name.Contains(".MagazineCollisionProbe")))
          {
            UnityEngine.Object.DestroyImmediate(c);
          }
        }
      }

      foreach (var go in Resources.FindObjectsOfTypeAll<GameObject>())
      {
        if (go.scene.IsValid() && (go.name == "Gunfly matrix launch pad" || go.name == "Native gunfly launch pad"))
        {
          UnityEngine.Object.DestroyImmediate(go);
        }
      }

      return SceneFixture.Restore() + " Benchmark hooks and temporary pads removed. Controller-role compatibility retained. Restart before another independent batch.";
    }
  }

  public sealed class MagazineCollisionObserver : MonoBehaviour
  {
    static bool registered;
    public MagazineCollisionObserver(IntPtr pointer) : base(pointer)
    {
    }

    public void OnCollisionEnter(Collision collision)
    {
      MatrixRunner.Contact(collision);
      ComparisonRunner.Contact(collision);
    }

    public void OnCollisionStay(Collision collision)
    {
      MatrixRunner.Contact(collision);
      ComparisonRunner.Contact(collision);
    }

    internal static void Attach(RigManager rig)
    {
      if (!registered)
      {
        ClassInjector.RegisterTypeInIl2Cpp<MagazineCollisionObserver>();
        registered = true;
      }

      var bodies = rig.physicsRig.m_chest.GetComponents<Collider>().Select(c => c.attachedRigidbody).Where(b => b).Distinct().ToArray();
      if (bodies.Length == 0)
      {
        throw new InvalidOperationException("Chest has no rigidbody for passive contact logging.");
      }

      foreach (var body in bodies)
      {
        if (!body.GetComponent<MagazineCollisionObserver>())
        {
          body.gameObject.AddComponent<MagazineCollisionObserver>();
        }
      }
    }
  }
}

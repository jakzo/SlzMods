using System;
using System.Collections.Generic;
using System.Diagnostics;
using HarmonyLib;
using UnityEngine;
using Valve.VR;
using StressLevelZero.Rig;

namespace Sst.BoneworksFrameFix.Debug.Gunfly
{
  // Injects at the OpenVR action-data boundary, before SteamVR action caching and
  // BONEWORKS Controller.CacheInputs. Never writes controller, rig, or body state.
  public static class NativeInputs
  {
    struct Key : IEquatable<Key>
    {
      public ulong Action, Source;
      public bool Equals(Key other) => Action == other.Action && Source == other.Source;
      public override bool Equals(object other) => other is Key && Equals((Key)other);
      public override int GetHashCode() => Action.GetHashCode() ^ Source.GetHashCode();
    }

    sealed class AnalogState
    {
      public long Generation = -1;
      public float X, Y, DeltaX, DeltaY;
    }

    sealed class DigitalState
    {
      public long Generation = -1;
      public bool Value, Changed;
    }

    static readonly object gate = new object();
    static readonly HashSet<ulong> axes = new HashSet<ulong>(), clicks = new HashSet<ulong>(), touches = new HashSet<ulong>();
    static readonly Dictionary<Key, AnalogState> analog = new Dictionary<Key, AnalogState>();
    static readonly Dictionary<Key, DigitalState> digital = new Dictionary<Key, DigitalState>();
    static ulong left, right;
    static long generation, releaseGeneration, analogLeft, analogRight, digitalLeft, digitalRight;
    public static float CrouchAxis = -1;
    public static void ResetInstallation()
    {
      lock (gate)
      {
        active = false;
        installed = false;
        releaseGeneration = -100;
      }
    }

    static bool installed, active;
    static double duration;
    static readonly Stopwatch timer = new Stopwatch();
    public static string Initialize()
    {
      lock (gate)
      {
        if (installed)
        {
          return Status();
        }

        Check(OpenVR.Input.GetInputSourceHandle("/user/hand/left", ref left));
        Check(OpenVR.Input.GetInputSourceHandle("/user/hand/right", ref right));
        foreach (var set in new[]
        {
          "default",
          "oculustouch"
        }

        )
        {
          Resolve("/actions/" + set + "/in/ThumbStickPosition", axes);
          Resolve("/actions/" + set + "/in/ThumbStickClick", clicks);
          Resolve("/actions/" + set + "/in/ThumbStickTouch", touches);
        }

        var h = new HarmonyLib.Harmony("jakzo.gunfly.benchmarks.inputs");
        h.Patch(AccessTools.Method(typeof(CVRInput), "UpdateActionState"), postfix: new HarmonyMethod(typeof(NativeInputs).GetMethod("AfterActionUpdate")));
        h.Patch(AccessTools.Method(typeof(CVRInput), "GetAnalogActionData"), postfix: new HarmonyMethod(typeof(NativeInputs).GetMethod("AnalogPostfix")));
        h.Patch(AccessTools.Method(typeof(CVRInput), "GetDigitalActionData"), postfix: new HarmonyMethod(typeof(NativeInputs).GetMethod("DigitalPostfix")));
        installed = true;
        return Status();
      }
    }

    static void Check(EVRInputError error)
    {
      if (error != EVRInputError.None)
      {
        throw new InvalidOperationException("OpenVR input error: " + error);
      }
    }

    static void Resolve(string path, HashSet<ulong> set)
    {
      ulong handle = 0;
      Check(OpenVR.Input.GetActionHandle(path, ref handle));
      if (handle == 0)
      {
        throw new InvalidOperationException("Zero action handle: " + path);
      }

      set.Add(handle);
    }

    public static string StartProbe(float seconds)
    {
      try
      {
        Initialize();
        lock (gate)
        {
          duration = seconds;
          generation = 0;
          releaseGeneration = -100;
          analog.Clear();
          digital.Clear();
          analogLeft = analogRight = digitalLeft = digitalRight = 0;
          timer.Restart();
          active = true;
          return Status();
        }
      } catch
      {
        Stop();
        throw;
      }
    }

    public static string Stop()
    {
      lock (gate)
      {
        if (active)
        {
          active = false;
          releaseGeneration = generation;
        }

        return Status();
      }
    }

    public static void AfterActionUpdate()
    {
      lock (gate)
      {
        generation++;
        if (active && timer.Elapsed.TotalSeconds >= duration)
        {
          active = false;
          releaseGeneration = generation;
        }
      }
    }

    public static void AnalogPostfix(ulong __0, ref InputAnalogActionData_t __1, ulong __3, ref EVRInputError __result)
    {
      lock (gate)
      {
        if (!axes.Contains(__0) || (!active && generation - releaseGeneration > 2))
        {
          return;
        }

        var key = new Key
        {
          Action = __0,
          Source = __3
        };
        AnalogState state;
        if (!analog.TryGetValue(key, out state))
        {
          state = new AnalogState
          {
            X = __1.x,
            Y = __1.y
          };
          analog.Add(key, state);
        }

        if (state.Generation != generation)
        {
          float x = active ? 0f : __1.x;
          float y = active ? (__3 == right ? (timer.Elapsed.TotalSeconds < 2 ? 1f : CrouchAxis) : (timer.Elapsed.TotalSeconds < 2 ? 0f : -1f)) : __1.y;
          state.DeltaX = x - state.X;
          state.DeltaY = y - state.Y;
          state.X = x;
          state.Y = y;
          state.Generation = generation;
        }

        __1.x = state.X;
        __1.y = state.Y;
        __1.z = 0;
        __1.deltaX = state.DeltaX;
        __1.deltaY = state.DeltaY;
        __1.deltaZ = 0;
        if (active)
        {
          __1.bActive = true;
          __1.fUpdateTime = 0;
          __result = EVRInputError.None;
          if (__3 == left)
          {
            analogLeft++;
          }

          if (__3 == right)
          {
            analogRight++;
          }
        }
      }
    }

    public static void DigitalPostfix(ulong __0, ref InputDigitalActionData_t __1, ulong __3, ref EVRInputError __result)
    {
      lock (gate)
      {
        bool click = clicks.Contains(__0), touch = touches.Contains(__0);
        if ((!click && !touch) || (!active && generation - releaseGeneration > 2))
        {
          return;
        }

        var key = new Key
        {
          Action = __0,
          Source = __3
        };
        DigitalState state;
        if (!digital.TryGetValue(key, out state))
        {
          state = new DigitalState
          {
            Value = __1.bState
          };
          digital.Add(key, state);
        }

        if (state.Generation != generation)
        {
          bool value = active ? (touch || (__3 != right && timer.Elapsed.TotalSeconds >= 2)) : __1.bState;
          state.Changed = value != state.Value;
          state.Value = value;
          state.Generation = generation;
        }

        __1.bState = state.Value;
        __1.bChanged = state.Changed;
        if (active)
        {
          __1.bActive = true;
          __1.fUpdateTime = 0;
          __result = EVRInputError.None;
          if (click && __3 == left)
          {
            digitalLeft++;
          }

          if (click && __3 == right)
          {
            digitalRight++;
          }
        }
      }
    }

    public static string Status() => "rawOpenVrOverrides=" + active + " elapsed=" + timer.Elapsed.TotalSeconds + " actionUpdates=" + generation + " analogPollsLeft=" + analogLeft + " right=" + analogRight + " clickPollsLeft=" + digitalLeft + " right=" + digitalRight;
    public static string Observe()
    {
      try
      {
        var c = UnityEngine.Object.FindObjectOfType<RigManager>().ControllerRig;
        var l = c.leftController.TryCast<StressLevelZero.Rig.Controller>();
        var r = c.rightController.TryCast<StressLevelZero.Rig.Controller>();
        if (!l || !r)
        {
          return Status() + "\nNative controller types=" + c.leftController.GetIl2CppType().FullName + " / " + c.rightController.GetIl2CppType().FullName;
        }

        return Status() + "\nNative cached leftAxis=" + l._thumbstickAxis.x + "," + l._thumbstickAxis.y + " click=" + l._thumbstick + " down=" + l._thumbstickDown + " rightAxis=" + r._thumbstickAxis.x + "," + r._thumbstickAxis.y + " rightClick=" + r._thumbstick + " jog=" + c._jogToggle;
      } catch (Exception e)
      {
        return Status() + "\n" + e;
      }
    }
  }
}

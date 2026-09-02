#if DEBUG
using System;
using System.Collections.Generic;
using System.Diagnostics;
using MelonLoader;
using UnhollowerBaseLib;
using UnityEngine.Experimental.LowLevel;

namespace Sst.BoneworksPerformance;

internal enum PlayerLoopPhase {
  None,
  Initialization,
  EarlyUpdate,
  FixedUpdate,
  FixedUpdateClearLines,
  DirectorFixedUpdate,
  ScriptRunDelayedFixedFrameRate,
  NewInputFixedUpdate,
  PhysicsFixedUpdate,
  Physics2DFixedUpdate,
  DirectorFixedUpdatePostPhysics,
  ScriptFixedUpdate,
  LegacyFixedAnimationUpdate,
  XRFixedUpdate,
  PreUpdate,
  Update,
  PreLateUpdate,
  PostLateUpdate,
  Count,
}

internal static class PlayerLoopPhaseProfiler {
  private const string FixedUpdateType =
      "UnityEngine.Experimental.PlayerLoop.FixedUpdate";
  private const string InitializationType =
      "UnityEngine.Experimental.PlayerLoop.Initialization";
  private const string EarlyUpdateType =
      "UnityEngine.Experimental.PlayerLoop.EarlyUpdate";
  private const string PhysicsType =
      "UnityEngine.Experimental.PlayerLoop.FixedUpdate+PhysicsFixedUpdate";
  private const string ScriptType =
      "UnityEngine.Experimental.PlayerLoop.FixedUpdate+" +
      "ScriptRunBehaviourFixedUpdate";
  private const string FixedClearLinesType =
      "UnityEngine.Experimental.PlayerLoop.FixedUpdate+ClearLines";
  private const string DirectorFixedType =
      "UnityEngine.Experimental.PlayerLoop.FixedUpdate+DirectorFixedUpdate";
  private const string DelayedFixedType =
      "UnityEngine.Experimental.PlayerLoop.FixedUpdate+" +
      "ScriptRunDelayedFixedFrameRate";
  private const string NewInputFixedType =
      "UnityEngine.Experimental.PlayerLoop.FixedUpdate+NewInputFixedUpdate";
  private const string Physics2DType =
      "UnityEngine.Experimental.PlayerLoop.FixedUpdate+Physics2DFixedUpdate";
  private const string DirectorPostPhysicsType =
      "UnityEngine.Experimental.PlayerLoop.FixedUpdate+" +
      "DirectorFixedUpdatePostPhysics";
  private const string LegacyAnimationType =
      "UnityEngine.Experimental.PlayerLoop.FixedUpdate+" +
      "LegacyFixedAnimationUpdate";
  private const string XrFixedType =
      "UnityEngine.Experimental.PlayerLoop.FixedUpdate+XRFixedUpdate";
  private const string PreUpdateType =
      "UnityEngine.Experimental.PlayerLoop.PreUpdate";
  private const string UpdateType =
      "UnityEngine.Experimental.PlayerLoop.Update";
  private const string PreLateUpdateType =
      "UnityEngine.Experimental.PlayerLoop.PreLateUpdate";
  private const string PostLateUpdateType =
      "UnityEngine.Experimental.PlayerLoop.PostLateUpdate";

  private static PlayerLoopSystem _instrumentedLoop;

  public static void Install() {
    try {
      var loop = GetDefaultLoopWithoutStrippedConverters();
      var inserted = 0;
      Instrument(ref loop, ref inserted);
      var minimumExpected = 11 * 2;
      if (inserted < minimumExpected) {
        MelonLogger.Warning(
            "Expected at least " + minimumExpected +
            " PlayerLoop probes but inserted " +
            inserted + "; phase context is incomplete."
        );
      }
      _instrumentedLoop = loop;
      SetLoopWithoutStrippedConverters(loop);
      MelonLogger.Msg(
          "Installed " + inserted +
          " timestamp probes around top-level and fixed-step phases."
      );
    } catch (Exception exception) {
      MelonLogger.Warning(
          "Could not install PlayerLoop phase probes: " + exception
      );
    }
  }

  private static PlayerLoopSystem GetDefaultLoopWithoutStrippedConverters() {
    var flat = PlayerLoop.GetDefaultPlayerLoopInternal();
    var offset = 0;
    return Expand(flat, ref offset);
  }

  private static PlayerLoopSystem Expand(
      Il2CppReferenceArray<PlayerLoopSystemInternal> flat, ref int offset
  ) {
    var index = offset;
    var source = flat[offset++];
    var result = new PlayerLoopSystem {
      type = source.type,
      updateDelegate = source.updateDelegate,
      updateFunction = source.updateFunction,
      loopConditionFunction = source.loopConditionFunction,
    };
    if (source.numSubSystems <= 0)
      return result;
    // Unity stores the total number of descendants, not the direct child
    // count. Child subtrees occupy the following contiguous array entries.
    var end = index + source.numSubSystems;
    var expanded = new List<PlayerLoopSystem>();
    while (offset <= end)
      expanded.Add(Expand(flat, ref offset));
    var children =
        new Il2CppReferenceArray<PlayerLoopSystem>(expanded.Count);
    for (var i = 0; i < expanded.Count; i++)
      children[i] = expanded[i];
    result.subSystemList = children;
    return result;
  }

  private static void SetLoopWithoutStrippedConverters(
      PlayerLoopSystem loop
  ) {
    var flattened = new List<PlayerLoopSystemInternal>();
    Flatten(loop, flattened);
    var array =
        new Il2CppReferenceArray<PlayerLoopSystemInternal>(flattened.Count);
    for (var i = 0; i < flattened.Count; i++)
      array[i] = flattened[i];
    PlayerLoop.SetPlayerLoopInternal(array);
  }

  private static void Flatten(
      PlayerLoopSystem source, List<PlayerLoopSystemInternal> output
  ) {
    var children = source.subSystemList;
    var index = output.Count;
    output.Add(new PlayerLoopSystemInternal {
      type = source.type,
      updateDelegate = source.updateDelegate,
      updateFunction = source.updateFunction,
      loopConditionFunction = source.loopConditionFunction,
      numSubSystems = 0,
    });
    if (children == null)
      return;
    for (var i = 0; i < children.Length; i++)
      Flatten(children[i], output);
    var flattened = output[index];
    flattened.numSubSystems = output.Count - index - 1;
    output[index] = flattened;
  }

  private static void Instrument(ref PlayerLoopSystem parent, ref int count) {
    var children = parent.subSystemList;
    if (children == null)
      return;
    var output = new List<PlayerLoopSystem>(children.Length + 6);
    for (var i = 0; i < children.Length; i++) {
      var child = children[i];
      Instrument(ref child, ref count);
      var name = child.type == null ? "" : child.type.FullName;
      if (TryGetPhase(name, out var phase)) {
        output.Add(CreateProbe(child, phase, true));
        count++;
      }
      output.Add(child);
      if (TryGetPhase(name, out phase)) {
        output.Add(CreateProbe(child, phase, false));
        count++;
      }
    }
    var replacement =
        new Il2CppReferenceArray<PlayerLoopSystem>(output.Count);
    for (var i = 0; i < output.Count; i++)
      replacement[i] = output[i];
    parent.subSystemList = replacement;
  }

  private static bool TryGetPhase(
      string typeName, out PlayerLoopPhase phase
  ) {
    switch (typeName) {
      case InitializationType:
        phase = PlayerLoopPhase.Initialization;
        return true;
      case EarlyUpdateType:
        phase = PlayerLoopPhase.EarlyUpdate;
        return true;
      case FixedUpdateType:
        phase = PlayerLoopPhase.FixedUpdate;
        return true;
      case PhysicsType:
        phase = PlayerLoopPhase.PhysicsFixedUpdate;
        return true;
      case ScriptType:
        phase = PlayerLoopPhase.ScriptFixedUpdate;
        return true;
      case FixedClearLinesType:
        phase = PlayerLoopPhase.FixedUpdateClearLines;
        return true;
      case DirectorFixedType:
        phase = PlayerLoopPhase.DirectorFixedUpdate;
        return true;
      case DelayedFixedType:
        phase = PlayerLoopPhase.ScriptRunDelayedFixedFrameRate;
        return true;
      case NewInputFixedType:
        phase = PlayerLoopPhase.NewInputFixedUpdate;
        return true;
      case Physics2DType:
        phase = PlayerLoopPhase.Physics2DFixedUpdate;
        return true;
      case DirectorPostPhysicsType:
        phase = PlayerLoopPhase.DirectorFixedUpdatePostPhysics;
        return true;
      case LegacyAnimationType:
        phase = PlayerLoopPhase.LegacyFixedAnimationUpdate;
        return true;
      case XrFixedType:
        phase = PlayerLoopPhase.XRFixedUpdate;
        return true;
      case PreUpdateType:
        phase = PlayerLoopPhase.PreUpdate;
        return true;
      case UpdateType:
        phase = PlayerLoopPhase.Update;
        return true;
      case PreLateUpdateType:
        phase = PlayerLoopPhase.PreLateUpdate;
        return true;
      case PostLateUpdateType:
        phase = PlayerLoopPhase.PostLateUpdate;
        return true;
      default:
        phase = default(PlayerLoopPhase);
        return false;
    }
  }

  private static PlayerLoopSystem CreateProbe(
      PlayerLoopSystem target, PlayerLoopPhase phase, bool begin
  ) {
    Action callback = begin
        ? (Action)(() => PerformanceCapture.BeginPlayerLoopPhase(phase))
        : () => PerformanceCapture.EndPlayerLoopPhase(phase);
    return new PlayerLoopSystem {
      type = target.type,
      updateDelegate = callback,
    };
  }
}
#endif

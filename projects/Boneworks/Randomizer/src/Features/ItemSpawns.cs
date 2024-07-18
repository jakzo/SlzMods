using System;
using MelonLoader;
using HarmonyLib;
using StressLevelZero.Pool;
using System.Collections.Generic;
using StressLevelZero.Data;
using System.Reflection;

namespace Sst.Randomizer.Features;

public class ItemSpawns : Feature {
  private static List<SpawnableObject> _spawnables = new();
  private List<(MethodBase, MethodInfo)> _patches = new();

  public override void OnEnabled() {
    PatchAllOverloads(
        typeof(PoolManager), nameof(PoolManager.Spawn),
        typeof(ItemSpawns).GetMethod(nameof(PoolManagerSpawnPrefix)));
  }

  public override void OnDisabled() {
    foreach (var (method, patch) in _patches) {
      Mod.HarmonyInstance.Unpatch(method, patch);
    }
  }

  private void PatchAllOverloads(Type type, string methodName,
                                 MethodInfo prefix) {
    foreach (var method in type.GetMethods(BindingFlags.Static |
                                           BindingFlags.Public)) {
      if (method.Name == methodName) {
        var patch = Mod.HarmonyInstance.Patch(
            method, prefix: new HarmonyMethod(prefix));
        _patches.Add((method, patch));
      }
    }
  }

  private void PoolManagerSpawnPrefix(ref string name) {
    // TODO: Does _registeredSpawnableObjects always contain everything in
    // the game?
    if (PoolManager._registeredSpawnableObjects.Count != _spawnables.Count) {
      _spawnables = new();
      foreach (var spawnable in PoolManager._registeredSpawnableObjects
                   .Values) {
        _spawnables.Add(spawnable);
      }
    }
    var randomName = _spawnables[new Random().Next(_spawnables.Count)].title;
    Dbg.Log("Randomizing PoolManager.Spawn from", name, "to", randomName);
    name = randomName;
  }
}

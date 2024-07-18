using System;
using System.Reflection;
using HarmonyLib;
using MelonLoader;

namespace Sst.Randomizer;

public class Mod : MelonMod {
  public Feature[] Features = {
    // new Features.ItemSpawns(),
    new Features.LootSpawns(),
  };
  public MelonPreferences_Category PrefCategory;

  public override void OnApplicationStart() {
    Dbg.Init(BuildInfo.NAME);

    PrefCategory = MelonPreferences.CreateCategory(BuildInfo.NAME);
    foreach (var feature in Features) {
      feature.Mod = this;
      feature.Initialize();
    }
  }
}

public abstract class Feature {
  public Mod Mod;

  public abstract void Initialize();
}

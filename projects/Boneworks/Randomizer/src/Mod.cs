using System;
using System.Reflection;
using HarmonyLib;
using MelonLoader;

namespace Sst.Randomizer;

public class Mod : MelonMod {
  public Feature[] Features;

  public override void OnApplicationStart() {
    Dbg.Init(BuildInfo.NAME);

    Features = new[] {
      new Features.ItemSpawns(),
    };

    var category = MelonPreferences.CreateCategory(BuildInfo.NAME);
    foreach (var feature in Features) {
      feature.Mod = this;
      feature.PrefEnabled = category.CreateEntry(feature.GetType().Name, true);
      feature.OnEnabled();
    }
  }
}

public abstract class Feature {
  public Mod Mod;
  public MelonPreferences_Entry<bool> PrefEnabled;

  public abstract void OnEnabled();

  public abstract void OnDisabled();
}

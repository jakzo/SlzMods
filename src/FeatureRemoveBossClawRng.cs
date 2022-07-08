using MelonLoader;
using UnityEngine;

namespace SpeedrunTools
{
  class FeatureRemoveBossClawRng : Feature
  {
    public readonly Pref<bool> PrefEnabled = new Pref<bool>()
    {
      Id = "removeBossClawRng",
      Name = "Make boss claw always patrol to a single point",
      DefaultValue = true
    };

    public readonly Pref<float> PrefX = new Pref<float>()
    {
      Id = "bossClawX",
      Name = "The point the boss claw will always patrol to (should be between -100 and 140, default is 120 near level exit)",
      DefaultValue = 120.0f
    };

    public override void OnSceneWasInitialized(int buildIndex, string sceneName)
    {
      if (sceneName != "scene_streets" || !PrefEnabled.Read()) return;

      Utils.LogDebug("Init boss claw RNG");
      var bca = Object.FindObjectOfType<BossClawAi>();
      if (bca == null)
      {
        MelonLogger.Warning("No boss claw in current scene");
        return;
      }

      // Set home position X to near the level exit instead of the middle
      Utils.LogDebug("Setting BossClawAi._homePosition");
      var homePosition = bca._homePosition;
      bca._homePosition = new Vector3(
        PrefX.Read(),
        homePosition.y,
        homePosition.z
      );
      // Reduce patrol area to a point at the home position
      Utils.LogDebug("Setting BossClawAi.patrolXz");
      bca.patrolXz = new Vector2(0.0f, 0.0f);

      // Color the boss claw so it's obvious that it's been modded
      var cabin = GameObject.Find("/PLACE_STREETS/boss_CLAW/Physics/cabin");
      if (cabin == null)
      {
        MelonLogger.Warning("No boss claw cabin to color in current scene");
        return;
      }
      Utils.LogDebug("Coloring boss claw");
      var newMaterial = new Material(Shader.Find("Valve/vr_standard"))
      {
        color = new Color(0.8f, 0.8f, 0.2f)
      };
      for (int i = 0; i < cabin.transform.childCount; i++)
      {
        var child = cabin.transform.GetChild(i).gameObject;
        if (!child.name.StartsWith("kitbash_plate_heavy_4m4m")) continue;
        Utils.LogDebug($"Coloring object: {child.name}");
        child.GetComponent<MeshRenderer>().SetMaterial(newMaterial);
      }

      MelonLogger.Msg("Boss claw AI updated and colored");
    }
  }
}

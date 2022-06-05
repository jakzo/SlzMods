using MelonLoader;

namespace SpeedrunTools
{
  public static class BuildInfo
  {
    public const string Name = "SpeedrunTools";
    public const string Author = "jakzo";
    public const string Company = null;
    public const string Version = "1.0.0";
    public const string DownloadLink = "https://boneworks.thunderstore.io/package/jakzo/SpeedrunTools/";
  }

  public class SpeedrunTools : MelonMod
  {
    private const string PREF_CATEGORY = "SpeedrunTools";
    private const string PREF_REMOVE_BOSS_CLAW_RNG = "removeBossClawRng";
    private const string PREF_BOSS_CLAW_X = "bossClawX";

    static private T GetPref<T>(string identifier)
    {
      return MelonPreferences.GetCategory(PREF_CATEGORY).GetEntry<T>(identifier).Value;
    }

    public override void OnApplicationStart()
    {
      MelonLogger.Msg("Loading preferences");
      var category = MelonPreferences.CreateCategory(PREF_CATEGORY);
      category.CreateEntry(PREF_REMOVE_BOSS_CLAW_RNG, true, "Make boss claw always patrol to a single point");
      category.CreateEntry(PREF_BOSS_CLAW_X, 120.0f, "The point the boss claw will always patrol to (should be between -100 and 140, default is 120 near level exit)");
      MelonLogger.Msg("Preferences loaded");
    }

    public override void OnSceneWasInitialized(int buildIndex, string sceneName)
    {
      if (GetPref<bool>(PREF_REMOVE_BOSS_CLAW_RNG))
      {
        var bca = UnityEngine.Object.FindObjectOfType<BossClawAi>();
        if (bca == null)
        {
          MelonLogger.Msg("No boss claw in current scene");
          return;
        }

        // Set home position X to near the level exit instead of the middle
        MelonLogger.Msg("Setting BossClawAi._homePosition");
        var homePosition = bca._homePosition;
        bca._homePosition = new UnityEngine.Vector3(
          GetPref<float>(PREF_BOSS_CLAW_X),
          homePosition.y,
          homePosition.z
        );
        // Reduce patrol area to a point at the home position
        MelonLogger.Msg("Setting BossClawAi.patrolXz");
        bca.patrolXz = new UnityEngine.Vector2(0.0f, 0.0f);

        // Color the boss claw so it's obvious that it's been modded
        var cabin = UnityEngine.GameObject.Find("/PLACE_STREETS/boss_CLAW/Physics/cabin");
        if (cabin == null)
        {
          MelonLogger.Msg("No boss claw cabin to color in current scene");
          return;
        }
        MelonLogger.Msg("Coloring boss claw");
        var newMaterial = new UnityEngine.Material(UnityEngine.Shader.Find("Valve/vr_standard"));
        newMaterial.color = new UnityEngine.Color(0.8f, 0.8f, 0.2f);
        for (int i = 0; i < cabin.transform.childCount; i++)
        {
          var child = cabin.transform.GetChild(i).gameObject;
          if (!child.name.StartsWith("kitbash_plate_heavy_4m4m")) continue;
          MelonLogger.Msg($"Coloring object: {child.name}");
          child.GetComponent<UnityEngine.MeshRenderer>().SetMaterial(newMaterial);
        }

        MelonLogger.Msg("Boss claw AI updated and colored");
      }
    }
  }
}

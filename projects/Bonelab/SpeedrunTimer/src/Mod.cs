using MelonLoader;
using Sst.Utilities;

namespace Sst.SpeedrunTimer {
public class Mod : MelonMod {
  public const string PREF_CATEGORY_ID = BuildInfo.Name;

  private static SplitsTimer _timer = new SplitsTimer();

  public MelonPreferences_Category PrefCategory;

  public static Mod Instance;
  public Mod() { Instance = this; }

  public override void OnInitializeMelon() {
    Dbg.Init(PREF_CATEGORY_ID);
    PrefCategory = MelonPreferences.CreateCategory(PREF_CATEGORY_ID);
    SplitsTimer.OnInitialize();
    SaveDeleteImprovements.OnInitialize();

    LevelHooks.OnLoad += _timer.OnLoadingScreen;
    LevelHooks.OnLevelStart += _timer.OnLevelStart;
  }

  public override void OnUpdate() { _timer.OnUpdate(); }
}
}

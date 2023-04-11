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
    _timer.OnInitialize();

    LevelHooks.OnLoad += level => _timer.OnLoadingScreen(level);

    LevelHooks.OnLevelStart += level => _timer.OnLevelStart(level);
  }

  public override void OnUpdate() { _timer.OnUpdate(); }
}
}

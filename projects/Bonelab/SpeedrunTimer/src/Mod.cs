using MelonLoader;
using Sst.Utilities;

namespace Sst.SpeedrunTimer {
public class Mod : MelonMod {
  public const string PREF_CATEGORY_ID = BuildInfo.Name;

  private static SplitsTimer _timer = new SplitsTimer();
  private InputServer _inputServer;

  public MelonPreferences_Category PrefCategory;
  public SplitsServer SplitsServer;

  public static Mod Instance;
  public Mod() { Instance = this; }

  public override void OnInitializeMelon() {
    Dbg.Init(PREF_CATEGORY_ID);
    PrefCategory = MelonPreferences.CreateCategory(PREF_CATEGORY_ID);
    SplitsTimer.OnInitialize();
    SaveDeleteImprovements.OnInitialize();

    LevelHooks.OnLoad += _timer.OnLoadingScreen;
    LevelHooks.OnLevelStart += _timer.OnLevelStart;

    SplitsServer = new SplitsServer();
    _inputServer = new InputServer();
  }

  public override void OnUpdate() {
    _timer.OnUpdate();
    _inputServer?.SendInputState();
  }
}
}

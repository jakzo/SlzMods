using MelonLoader;
using Sst.Utilities;
using SLZ.Rig;

namespace Sst.SpeedrunTimer {
public class Mod : MelonMod {
  public const string PREF_CATEGORY_ID = BuildInfo.Name;

  private static SplitsTimer _timer = new SplitsTimer();
  private Server _server;

  public MelonPreferences_Category PrefCategory;
  public RigManager RigManager;

  public static Mod Instance;
  public Mod() { Instance = this; }

  public override void OnInitializeMelon() {
    Dbg.Init(PREF_CATEGORY_ID);
    PrefCategory = MelonPreferences.CreateCategory(PREF_CATEGORY_ID);
    SplitsTimer.OnInitialize();
    SaveDeleteImprovements.OnInitialize();

    LevelHooks.OnLoad += _timer.OnLoadingScreen;
    LevelHooks.OnLevelStart += _timer.OnLevelStart;

    LevelHooks.OnLoad += level => RigManager = null;
    LevelHooks.OnLevelStart += level => RigManager =
        Utilities.Bonelab.GetRigManager();

    _server = new Server();
  }

  public override void OnUpdate() {
    _timer.OnUpdate();
    _server?.SendInputState();
  }
}
}

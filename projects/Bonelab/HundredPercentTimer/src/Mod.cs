using MelonLoader;
using Sst.Common.Bonelab.HundredPercent;

namespace Sst.HundredPercentTimer {
public class Mod : MelonMod {
  private const float UPDATE_FREQUENCY = 1f;

  private Server _server;

  public override void OnInitializeMelon() {
    Dbg.Init(BuildInfo.NAME);

    AchievementTracker.Initialize();
    CapsuleTracker.Initialize();

    _server = new Server();
  }

  public override void OnDeinitializeMelon() {
    CapsuleTracker.Deinitialize();
    _server.Dispose();
  }
}
}

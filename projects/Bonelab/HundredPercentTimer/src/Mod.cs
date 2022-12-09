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

    Utilities.LevelHooks.OnLoad += level => ServerSendIfNecessary();
    Utilities.LevelHooks.OnLevelStart += level => ServerSendIfNecessary();
  }

  private void ServerSendIfNecessary() {
    if (Utilities.AntiCheat.CheckRunLegitimacy<Mod>()) {
      if (_server == null)
        _server = new Server();
      else
        _server.SendStateIfChanged();
    } else if (_server != null) {
      _server.Dispose();
      _server = null;
    }
  }

  public override void OnDeinitializeMelon() {
    CapsuleTracker.Deinitialize();
    _server?.Dispose();
    _server = null;
  }
}
}

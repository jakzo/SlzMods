using System;
using System.Linq;
using MelonLoader;

namespace Sst.SpeedrunTimer {
public class SplitsServer {
  private WebsocketServer _ws;

  public SplitsServer(int port = 6162, string ip = null) {
    _ws = new WebsocketServer() {
      OnConnect = client => InitGameTime(),
    };
    _ws.Start(port, ip);
    var addresses = (ip != null ? new[] { ip } : Network.GetAllAddresses())
                        .Select(address => $"ws://{address}:{port}");
    var addressText = string.Join("\n", addresses);
    MelonLogger.Msg($"Splits server started at:\n{addressText}");
  }

  public void Start() { _ws.Send("start"); }
  public void Split() { _ws.Send("split"); }
  public void SplitOrStart() { _ws.Send("splitorstart"); }
  public void Reset() { _ws.Send("reset"); }
  public void TogglePause() { _ws.Send("togglepause"); }
  public void Undo() { _ws.Send("undo"); }
  public void Skip() { _ws.Send("skip"); }
  public void InitGameTime() { _ws.Send("initgametime"); }
  public void SetGameTime(TimeSpan time) {
    _ws.Send($"setgametime {time.TotalSeconds}");
  }
  public void SetLoadingTimes(TimeSpan times) {
    _ws.Send($"setloadingtimes {times.TotalSeconds}");
  }
  public void PauseGameTime() { _ws.Send("pausegametime"); }
  public void ResumeGameTime() { _ws.Send("resumegametime"); }
}
}

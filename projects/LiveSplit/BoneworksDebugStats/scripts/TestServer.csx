#r "../../../../packages/Newtonsoft.Json.13.0.1/lib/net45/Newtonsoft.Json.dll"
#r "../../../../projects/Boneworks/SpeedrunTools/bin/Debug/SpeedrunTools.dll"

using System;
using System.Threading;
using Newtonsoft.Json;
using Sst.Common.Boneworks;

class ConsoleLogger : Sst.Common.Ipc.Logger {
  public override void Debug(string message) => Console.WriteLine(message);
  public override void
  Error(string message) => Console.Error.WriteLine(message);
}

try {
  var server =
      new Sst.Common.Ipc.Server(DebugStats.NAMED_PIPE, new ConsoleLogger());

  var state = new DebugStats() {
    fps = 0f,
    droppedFrames = 0,
    numFramesMagNotTouching = 0,
  };
  var timer = new System.Timers.Timer();
  timer.Interval = 1200;
  timer.Enabled = true;

  timer.Elapsed += (source, e) => {
    state.numFramesMagNotTouching++;
    state.fps += 1f / 3f;
    // if (i % 3 == 0)
    //   state.isLoading = !state.isLoading;
    server.Send(JsonConvert.SerializeObject(state));
    Console.Write(".");
  };

  server.OnClientConnected +=
      stream => { Console.Write("\nClient connected"); };
  server.OnClientDisconnected += stream =>
      Console.Write("\nClient disconnected");

  while (true)
    Thread.Sleep(800);
} catch (Exception ex) {
  Console.Error.WriteLine(ex);
}

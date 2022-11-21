#r "../../../../packages/Newtonsoft.Json.13.0.1/lib/net45/Newtonsoft.Json.dll"
#r "../../../../projects/Bonelab/CompletionistHelper/bin/Debug/CompletionistHelper.dll"

using System;
using System.Threading;
using Newtonsoft.Json;
using Sst.Common.Bonelab;

try {
  var server = new Sst.Common.Ipc.Server(HundredPercent.NAMED_PIPE);
  var state = new HundredPercent.GameState() {
    isLoading = false,           levelBarcode = null,
    capsulesUnlocked = 0,        capsulesTotal = 174,
    capsulesJustUnlocked = null, achievementsUnlocked = 0,
    achievementsTotal = 55,      achievementsJustUnlocked = null,
    percentageComplete = 0,      percentageTotal = 0.95f,
  };
  var timer = new System.Timers.Timer();
  timer.Interval = 1200;
  timer.Enabled = true;

  var i = 0;
  timer.Elapsed += (source, e) => {
    state.capsulesUnlocked++;
    state.achievementsUnlocked++;
    state.achievementsJustUnlocked = new string[] { $"ach{i++}" };
    state.percentageComplete += 0.001f;
    if (i == 5)
      state.levelBarcode = Sst.Utilities.Levels.Barcodes.DESCENT;
    else if (i % 5 == 0)
      state.levelBarcode = $"a{i}";
    if (i % 3 == 0)
      state.isLoading = !state.isLoading;
    server.Send(JsonConvert.SerializeObject(state));
    Console.Write(".");
  };

  server.OnClientConnected += () => Console.Write("\nClient connected");
  server.OnClientDisconnected += () => Console.Write("\nClient disconnected");

  while (true)
    Thread.Sleep(800);
} catch (Exception ex) {
  Console.Error.WriteLine(ex);
}

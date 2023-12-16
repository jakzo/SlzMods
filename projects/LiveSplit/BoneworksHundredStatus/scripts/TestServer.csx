#r "../../../../packages/Newtonsoft.Json.13.0.1/lib/net45/Newtonsoft.Json.dll"
#r "../../../../projects/Boneworks/CompletionistHelper/bin/Debug/CompletionistHelper.dll"

using System;
using System.Threading;
using Newtonsoft.Json;
using Sst.Common.Boneworks.HundredPercent;

try {
  var server = new Sst.Common.Ipc.Server(GameState.NAMED_PIPE);

  var state = new GameState() {
    isLoading = false,          levelBarcode = null,
    capsulesUnlocked = 0,       capsulesTotal = 174,
    capsuleJustUnlocked = null, achievementsUnlocked = 0,
    achievementsTotal = 55,     achievementJustUnlocked = null,
    percentageComplete = 0,     percentageTotal = 0.95f,
  };
  var timer = new System.Timers.Timer();
  timer.Interval = 1200;
  timer.Enabled = true;

  var i = 0;
  timer.Elapsed += (source, e) => {
    state.capsulesUnlocked++;
    state.achievementsUnlocked++;
    state.achievementJustUnlocked = $"ach{i++}";
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

  server.OnClientConnected += stream => {
    Console.Write("\nClient connected");
    // server.Send(
    //     "{\"isComplete\":false,\"isLoading\":false,\"levelBarcode\":\"c2534c5a-de61-4df9-8f6c-416954726547\",\"capsulesUnlocked\":111,\"capsulesTotal\":174,\"capsuleJustUnlocked\":\"Gym
    //     D6\",\"achievementsUnlocked\":37,\"achievementsTotal\":57,\"achievementJustUnlocked\":null,\"percentageComplete\":0.760238051,\"percentageTotal\":0.95}");
  };
  server.OnClientDisconnected += stream =>
      Console.Write("\nClient disconnected");

  while (true)
    Thread.Sleep(800);
} catch (Exception ex) {
  Console.Error.WriteLine(ex);
}

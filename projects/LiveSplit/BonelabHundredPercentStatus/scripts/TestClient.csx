#r "../../../../packages/Newtonsoft.Json.13.0.1/lib/net45/Newtonsoft.Json.dll"
#r "../bin/Debug/BonelabHundredPercentStatus.dll"

using System;
using System.Threading;
using Newtonsoft.Json;
using Sst.Common.Bonelab;

try {
  var client = new Sst.Common.Ipc.Client(HundredPercent.NAMED_PIPE);

  client.OnConnected += () => Console.WriteLine("Server connected");
  client.OnDisconnected += () => Console.WriteLine("Server disconnected");
  client.OnMessageReceived += Console.WriteLine;

  while (true)
    Thread.Sleep(800);
} catch (Exception ex) {
  Console.Error.WriteLine(ex);
}

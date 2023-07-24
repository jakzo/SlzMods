#r "../bin/Debug/SpeedrunTimer.dll"

using System;
using System.Threading;
using System.Threading.Tasks;

try {
  // var server = new Sst.SpeedrunTimer.Server();
  var server = new Sst.SpeedrunTimer.WebsocketServer();
  _ = server.Start();

  while (true) {
    server.Send("Yoyoyo");
    Thread.Sleep(2000);
  }
} catch (Exception ex) {
  Console.Error.WriteLine(ex);
}

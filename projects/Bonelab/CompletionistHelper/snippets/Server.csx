#r "../bin/Debug/CompletionistHelper.dll"

var server = new Sst.Common.Bonelab.HundredPercent.Server(1);
server.SendState(server.BuildGameState());

var stream = new System.IO.Pipes.NamedPipeServerStream(
    "BonelabHundredPercent", System.IO.Pipes.PipeDirection.InOut, 10,
    System.IO.Pipes.PipeTransmissionMode.Message);
stream.WaitForConnection();
// stream.IsConnected;
var bytes = System.Text.Encoding.UTF8.GetBytes("{\"capsulesUnlocked\":123}");
stream.Write(bytes, 0, bytes.Length);

namespace Sst.SpeedrunTimer {
class Livesplit {
  // State that the ASL finds using signature scanning
  private static byte[] State = {
    // 0 = magic string start
    // Signature is set dynamically to avoid finding this hardcoded array
    0x00, // 0xD4
    0xE2,
    0x03,
    0x34,
    0xC2,
    0xDF,
    0x63,
    0x24,
    // 8.0   = isLoading
    // 8.1   = isSittingInTaxi
    // 8.2-7 = unused
    0x00,
    // 9 = levelIdx
    0x00,
    // 10 = unused
    0x00,
    // 11 = unused
    0x00,
  };

  public static void
  SetState(bool isLoading, bool isSittingInTaxi, string levelTitle = "") {
    State[0] = 0xD4;
    State[8] =
        (byte)((isLoading ? 1 : 0) << 0 | (isSittingInTaxi ? 1 : 0) << 1);
    State[9] = Utilities.Levels.GetIndex(levelTitle);
  }
}

//   private class LivesplitPipe : System.IDisposable {
//     private List<(NamedPipeServerStream, StreamWriter)> _pipes =
//         new List<(NamedPipeServerStream, StreamWriter)>();

//     public LivesplitPipe() { CreateNewPipe(); }

//     private void CreateNewPipe() {
//      var thread= new Thread(() => {
//         var pipe =
//             new NamedPipeServerStream("BonelabSpeedrunTimer",
//             PipeDirection.Out);
//      Dbg.Log("Waiting for connection on new pipe");
//      pipe.WaitForConnection();
//      CreateNewPipe();
//      _pipes.Add((pipe, new StreamWriter(pipe)));
//     });
//     thread.IsBackground = true;
//     thread.Start();
//   }

//   public void Dispose() {
//     foreach (var (pipe, writer) in _pipes)
//       writer.Dispose();
//   }

//   private void WritePipe(string message) {
//     Dbg.Log($"WritePipe (pipeCount={_pipes.Count}): {message}");
//     foreach (var (pipe, writer) in _pipes) {
//       if (!pipe.IsConnected)
//         continue;
//       try {
//         writer.WriteLine(message);
//         writer.Flush();
//       } catch (IOException err) {
//         MelonLogger.Error("Pipe error:", err.Message);
//       }
//     }
//   }

//   public void OnLoading(string levelName) { WritePipe($":L:{levelName}"); }
//   public void OnStart(string levelName) { WritePipe($":S:{levelName}"); }
// }
}

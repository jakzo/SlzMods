#if DEBUG
using UnityEngine.Profiling;

namespace Sst.BoneworksPerformance;

internal sealed class UnityMarker {
  public readonly string Name;
  public readonly Recorder Recorder;
  public bool IsAvailable => Recorder != null;

  public UnityMarker(string name) {
    Name = name;
    try {
      Recorder = Recorder.Get(name);
      Recorder.enabled = true;
    } catch {
      Recorder = null;
    }
  }

  public double ElapsedMilliseconds =>
      Recorder == null ? 0.0 : Recorder.elapsedNanoseconds / 1000000.0;
  public int SampleCount => Recorder == null ? 0 : Recorder.sampleBlockCount;

  public void Disable() {
    if (Recorder != null)
      Recorder.enabled = false;
  }
}
#endif

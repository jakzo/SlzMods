#if DEBUG
namespace Sst.BoneworksPerformance;

internal enum CaptureMode { MetricsOnly, Basic, Detailed }

internal sealed class CaptureOptions {
  public string Preset = "Configured";
  public bool EnableCpuSampling = true;
  public bool EnableCompletionWaits = true;
  public bool EnableGraphics = true;
  public bool EnableUnityBinaryProfiler;
  public bool EnableMonoGcEvents = true;
  public bool EnableTargetedDiagnostics = true;
  public bool EnablePoseHistory = true;
  public int PoseHistorySampleRate = 250;
  public int PoseHistoryInterpolationDelayTicks = 1;
  public bool ShowIndicator = true;

  public CaptureOptions Clone() => new CaptureOptions {
    Preset = Preset,
    EnableCpuSampling = EnableCpuSampling,
    EnableCompletionWaits = EnableCompletionWaits,
    EnableGraphics = EnableGraphics,
    EnableUnityBinaryProfiler = EnableUnityBinaryProfiler,
    EnableMonoGcEvents = EnableMonoGcEvents,
    EnableTargetedDiagnostics = EnableTargetedDiagnostics,
    EnablePoseHistory = EnablePoseHistory,
    PoseHistorySampleRate = PoseHistorySampleRate,
    PoseHistoryInterpolationDelayTicks = PoseHistoryInterpolationDelayTicks,
    ShowIndicator = ShowIndicator,
  };
}
#endif

#if DEBUG
namespace Sst.BoneworksPerformance;

internal struct FrameSample {
  public int Frame;
  public double ElapsedMs;
  public double FrameMs;
  public double ProcessCpuMs;
  public int FixedUpdates;
  public double FixedUpdateMs;
  public double PhysicsFixedUpdateMs;
  public double ScriptFixedUpdateMs;
  public double SimulationDebtMs;
  public double FixedDeltaMs;
  public long ManagedHeapCapacityBytes;
  public long ManagedHeapBytes;
  public long TotalAllocatedBytes;
  public int GcGen0Collections;
  public int GcGen1Collections;
  public int GcGen2Collections;
  public int PhysBodyUpdateCollidersCalls;
  public int PhysBodyUnchangedColliderCalls;
  public double PhysBodyUpdateCollidersMs;
  public int ExceptionCount;
  public double ProfilerUpdateMs;
  public double ProfilerFixedUpdateMs;
  public double ProfilerGuiMs;
  public double ProfilerCaptureMs;
  public long ProfilerGuiHeapDeltaBytes;
  public int ProfilerGuiCalls;
  public int ProfilerGuiLayoutCalls;
  public int ProfilerGuiRepaintCalls;
  public int ProfilerGuiOtherCalls;
  public bool HasCompositorTiming;
  public double ClientFrameIntervalMs;
  public double CompositorCpuMs;
  public double CompositorGpuMs;
  public double CompositorIdleCpuMs;
  public double PresentWaitCpuMs;
  public double TotalRenderGpuMs;
  public double PreSubmitGpuMs;
  public double PostSubmitGpuMs;
  public int DroppedFrames;
}
#endif

#if DEBUG
namespace Sst.BoneworksPerformance;

internal struct CpuSample {
  public long Timestamp;
  public int ThreadId;
  public int Tick;
  public long InstructionPointer;
  public int StackOffset;
  public byte StackDepth;
  public double WeightMilliseconds;
  public bool IsMainThread;
  public bool IsWaitSnapshot;
  public int WaitId;
  public ulong CycleDelta;
  public PlayerLoopPhase PlayerLoopPhase;
}
#endif

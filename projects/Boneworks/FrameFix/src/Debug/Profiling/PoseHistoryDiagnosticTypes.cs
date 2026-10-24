using System.Runtime.InteropServices;

namespace Sst.BoneworksFrameFix;

internal struct FixedPoseObservation
{
  public int UnityFrame;
  public float FixedTime;
  public float RealtimeSinceStartup;
  public PoseHistorySample Sample;
}

internal struct ResampledFixedPoseObservation
{
  public int UnityFrame;
  public float FixedTime;
  public long TargetTimestamp;
  public long SimulationTimestamp;
  public long QueryTimestamp;
  public long AnchorTimestamp;
  public float AnchorGameTime;
  public float AnchorTimeScale;
  public long BeforeSequence;
  public long AfterSequence;
  public long BeforeTimestamp;
  public long AfterTimestamp;
  public float InterpolationAmount;
  public PoseBracketStatus Status;
  public TrackedPoseValue Head;
  public TrackedPoseValue Left;
  public TrackedPoseValue Right;
}

[StructLayout(LayoutKind.Auto)]
internal partial struct PoseHistorySample
{
  public long QueryTicks;
  public int HeadDeviceIndex;
  public int LeftDeviceIndex;
  public int RightDeviceIndex;
  public TrackedPoseValue Left;
  public TrackedPoseValue Right;
}

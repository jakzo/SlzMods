namespace Sst.Common.Boneworks {
public class DebugStats {
  public const string NAMED_PIPE = "BoneworksDebugStats";

  public float fps;
  public float physicsRate;
  public int droppedFrames;
  public int extraFrames;
  public int numFixedUpdatesMagNotTouching;
}
}

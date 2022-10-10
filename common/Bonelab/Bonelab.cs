using UnityEngine;
using SLZ.Rig;

namespace Sst.Utilities {
public class Bonelab {
  public static RigManager GetRigManager() =>
      GameObject.Find("[RigManager (Blank)]")?.GetComponent<RigManager>();
}
}

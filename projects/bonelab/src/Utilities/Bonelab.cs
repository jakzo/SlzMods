using MelonLoader;
using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using StressLevelZero.Rig;

namespace SpeedrunTools.Utilities {
public class Bonelab {
  public static RigManager GetRigManager() =>
      GameObject.Find("[RigManager (blank)]").GetComponent<RigManager>();
}
}

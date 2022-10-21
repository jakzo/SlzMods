using MelonLoader;
using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using StressLevelZero.Rig;

namespace Sst.Utilities {
public class Boneworks {
  public static RigManager
  GetRigManager() => GameObject.Find("[RigManager (Default Brett)]")
                         .GetComponent<RigManager>();
}
}

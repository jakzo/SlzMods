using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using MelonLoader;
using UnityEngine;

namespace Sst.Colliders;

public class Mod : MelonMod {
  private LinkedList<(Collider, GameObject)> _visualizations = new();

  public override void OnInitializeMelon() { Dbg.Init(BuildInfo.NAME); }

  public override void OnUpdate() {
    System.AppDomain.CurrentDomain.GetAssemblies() [0].GetName();

    var colliders = GameObject.FindObjectsOfType<Collider>();
    var item = _visualizations.First;
    foreach (var col in colliders) {
      var collider = col.TryCast<Collider>();
    }
  }
}

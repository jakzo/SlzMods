#r "../references/Bonelab/Assembly-CSharp.dll"
#r "../projects/Bonelab/SpeedrunPractice/bin/Debug/SpeedrunPractice.dll"
using System;
using UnityEngine;

var rigManager =
    GameObject.Find("[RigManager (Blank)]").GetComponent<SLZ.Rig.RigManager>();
Sst.Utilities.Collider.VisualizeAllIn(
    rigManager.gameObject, Sst.Utilities.Bonelab.HighlightShader
);

#r "../../references/Bonelab/Assembly-CSharp.dll"
#r "../../projects/Bonelab/SpeedrunPractice/bin/Debug/SpeedrunPractice.dll"
using System;
using UnityEngine;

// Barcode list at:
// ../projects/Bonelab/CompletionistHelper/snippets/ListUnlocks.csx

var avatarCrateBarcode =
    "c3534c5a-2236-4ce5-9385-34a850656173"; // peasant female M
var rigManager =
    GameObject.Find("[RigManager (Blank)]").GetComponent<SLZ.Rig.RigManager>();
rigManager.SwapAvatarCrate(avatarCrateBarcode);

#r "../../references/Bonelab/Assembly-CSharp.dll"
#r "../../projects/Bonelab/SpeedrunPractice/bin/Debug/SpeedrunPractice.dll"
using UnityEngine;

// Barcode list at:
// ../projects/Bonelab/CompletionistHelper/snippets/ListUnlocks.csx

var crateBarcode = "c1534c5a-adaf-4ae7-bd46-f19541766174";
var rigManager =
    GameObject.Find("[RigManager (Blank)]").GetComponent<SLZ.Rig.RigManager>();
SLZ.Marrow.Pool.AssetSpawner.Spawn(
    new SLZ.Marrow.Data.Spawnable() {
      crateRef = new SLZ.Marrow.Warehouse.SpawnableCrateReference(crateBarcode),
      policyData =
          new SLZ.Marrow.Data.SpawnPolicyData() {
            mode = SLZ.Marrow.Data.SpawnPolicyData.PolicyRule.GROW,
            maxSize = 999,
          },
    },
    rigManager.physicsRig.m_head.position +
        rigManager.physicsRig.m_head.rotation * Vector3.forward,
    Quaternion.identity, new Sst.Utilities.Il2CppNullable<Vector3>(null), false,
    new Sst.Utilities.Il2CppNullable<int>(null)
);

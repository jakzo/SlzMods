#r "../../references/Boneworks/Assembly-CSharp.dll"
#r "../../projects/Boneworks/LootDropBugfix/bin/Debug/LootDropBugfix.dll"
using UnityEngine;

var rigManager =
    UnityEngine.GameObject.FindObjectOfType<StressLevelZero.Rig.RigManager>();
rigManager.Teleport(new Vector3(193f, 11f, -104f));
var head = rigManager.physicsRig.m_head;
bool? nullableTrue = true;
var go = StressLevelZero.Pool.PoolManager.Spawn(
    "Baton", head.position + head.rotation * new Vector3(0, 0, 2),
    Quaternion.identity, new Sst.Utilities.Il2CppNullable<bool>(nullableTrue)
);
go;

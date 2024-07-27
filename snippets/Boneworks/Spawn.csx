#r "../../references/Boneworks/Assembly-CSharp.dll"
#r "../../projects/Boneworks/LootDropBugfix/bin/Debug/LootDropBugfix.dll"
using UnityEngine;

var head =
    UnityEngine.GameObject.FindObjectOfType<StressLevelZero.Rig.RigManager>()
        .physicsRig.m_head;
bool? nullableTrue = true;
var go = StressLevelZero.Pool.PoolManager.Spawn(
    "Ammo Box Small 2500", head.position + head.rotation * new Vector3(0, 0, 2),
    Quaternion.identity, new Sst.Utilities.Il2CppNullable<bool>(nullableTrue)
);
go;

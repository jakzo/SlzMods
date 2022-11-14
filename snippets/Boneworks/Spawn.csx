var head =
    UnityEngine.GameObject.FindObjectOfType<StressLevelZero.Rig.RigManager>()
        .physicsRig.m_head;
var go = StressLevelZero.Pool.PoolManager.Spawn(
    "Ammo Box Small 2500", head.position + head.rotation * new Vector3(0, 0, 2),
    Quaternion.identity, Sst.LootDropBugfix.Mod._spawnAutoEnable);
go;

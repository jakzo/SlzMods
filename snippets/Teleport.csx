var rigManager =
    GameObject.Find("[RigManager (Blank)]").GetComponent<SLZ.Rig.RigManager>();
var pos = rigManager.physicsRig._feetRb.transform.position;
rigManager.Teleport(
    new Vector3(pos.x, pos.y - rigManager.physicsRig.footballRadius, pos.z),
    true
);
rigManager.physicsRig._feetRb.velocity = new Vector3(0f, 5f, 0f);

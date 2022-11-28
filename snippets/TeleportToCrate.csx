#r "../references/Bonelab/Assembly-CSharp.dll"
#r "../references/Other/UnityExplorer.dll"

var rigManager = UnityEngine.GameObject.Find("[RigManager (Blank)]")
                     .GetComponent<SLZ.Rig.RigManager>();
var ammoBox = UnityEngine.GameObject.Find("dest_ammoBoxLight Variant [7]");
var pos = ammoBox.transform.position;
UnityExplorer.ExplorerCore.Log($"ammo box pos: {pos.ToString()}");
rigManager.Teleport(new UnityEngine.Vector3(pos.x, pos.y, pos.z - 1f), true);

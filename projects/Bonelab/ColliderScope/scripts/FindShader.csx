#r "../bin/Debug/Patch3_MelonLoader0.5/ColliderScope.P3.ML5.dll"
#r "../../../../references/Other/UnityExplorer.dll"

using UnityEngine;
using UnityExplorer;

var i = 0;
var material = Paste() as Material;
var shaders = Resources.FindObjectsOfTypeAll<Shader>();

material.shader = shaders[i];
Log("shader: " + shaders[i++].name);

material.shader = shaders.First(s => s.name == "Sprites/Default");

// Doesn't do anything?
material.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Front);

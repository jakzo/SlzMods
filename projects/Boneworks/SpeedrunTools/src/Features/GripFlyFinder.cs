using UnityEngine;
using MelonLoader;
using StressLevelZero.Props;
using System;
using System.Linq;
using System.Collections.Generic;
using TMPro;
using StressLevelZero.Data;
using StressLevelZero.Interaction;
using Harmony;

namespace Sst.Features {
class GripFlyFinder : Feature {
  private Camera cam;
  private HashSet<TextMeshPro> tmps = new HashSet<TextMeshPro>();

  // private GameObject wristText;
  // private ObjectDestructable heldDestructable;

  public GripFlyFinder() { IsDev = true; }

  public static Grip[] GetFlyableGrips() =>
      UnityEngine.Object.FindObjectsOfType<Grip>().ToArray();

  // public static void Find() {
  //   var grips = UnityEngine.Object
  //                   .FindObjectsOfType<StressLevelZero.Interaction.Grip>();
  //   foreach (var grip in grips) {
  //     if (grip.bodyDominance > 0.5f)
  //       continue;
  //     Log(grip.name + ": " + grip.bodyDominance);
  //   }
  // }

  public override void OnLevelStart(int sceneIdx) {
    tmps = new HashSet<TextMeshPro>();
    cam = Camera.main;

    var grips = GetFlyableGrips();
    foreach (var grip in grips) {
      var go = new GameObject("GripFlyFinder_text");

      var tmp = go.AddComponent<TextMeshPro>();
      tmp.alignment = TextAlignmentOptions.Center;
      tmp.fontSize = 0.5f;
      tmp.color = Color.red;
      tmp.rectTransform.sizeDelta = new Vector2(10f, 10f);
      tmp.text = $"{grip.name} (bd = {grip.bodyDominance})";
      go.transform.localPosition = Vector3.zero;
      go.transform.SetParent(grip.transform, false);
      go.transform.localScale =
          new Vector3(go.transform.localScale.x / go.transform.lossyScale.x,
                      go.transform.localScale.y / go.transform.lossyScale.y,
                      go.transform.localScale.z / go.transform.lossyScale.z);
      tmps.Add(tmp);

      var pos = grip.transform.position;
      MelonLogger.Msg(
          $"Flyable grip {FullName(grip)} with body dominance {grip.bodyDominance} at: {pos.x} {pos.y} {pos.z}");
    }
  }

  public override void OnLateUpdate() {
    foreach (var tmp in tmps) {
      if (tmp == null)
        continue;

      var toCamera = tmp.transform.position - cam.transform.position;
      toCamera.y = 0;
      tmp.transform.rotation = Quaternion.LookRotation(toCamera, Vector3.up);
      tmp.transform.localPosition = tmp.transform.rotation * Vector3.up;
    }
  }

  public static string FullName(Component obj) {
    var parts = new Stack<string>();
    var tr = obj.transform;
    while (tr != null) {
      parts.Push(tr.name);
      tr = tr.parent;
    }
    return string.Join(".", parts);
  }
}
}

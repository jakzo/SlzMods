using MelonLoader;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using SLZ.Rig;
using SLZ.Marrow.Warehouse;

namespace Sst.SpeedrunPractice {
public struct PlayerState {
  public struct SerializedRigidbody {
    public string name;
    public Vector3 position;
    public Quaternion rotation;
    public Vector3 velocity;
    public Vector3 angularVelocity;
  }

  public Vector3 pos;
  public float rot;
  public Vector3 footPos;
  public Vector3 headPos;
  public Quaternion headRot;
  public Dictionary<string, SerializedRigidbody> rigidbodies;

  public static PlayerState Read() {
    var rigManager = Utils.State.rigManager;
    return new PlayerState() {
      pos = rigManager.realHeptaRig.transform.position,
      rot = rigManager.realHeptaRig.transform.rotation.eulerAngles.y,
      footPos = rigManager.physicsRig._feetRb.transform.position,
      headPos = rigManager.physicsRig.m_head.position,
      headRot = rigManager.physicsRig.m_head.rotation,
      rigidbodies = rigManager.GetComponentsInChildren<Rigidbody>()
                        .Select(
                            rb => new SerializedRigidbody() {
                              name = rb.name,
                              position = rb.transform.position,
                              rotation = rb.transform.rotation,
                              velocity = rb.velocity,
                              angularVelocity = rb.angularVelocity,
                            }
                        )
                        .ToDictionary(item => item.name),
    };
  }

  public static void Apply(PlayerState state) {
    var rigManager = Utils.State.rigManager;
    var pos = rigManager.physicsRig._feetRb.transform.position;
    rigManager.Teleport(
        new Vector3(pos.x, pos.y - rigManager.physicsRig.footballRadius, pos.z),
        true
    );
    foreach (var rb in rigManager.GetComponentsInChildren<Rigidbody>()) {
      SerializedRigidbody data;
      if (!state.rigidbodies.TryGetValue(rb.name, out data))
        continue;
      rb.transform.position = data.position;
      rb.transform.rotation = data.rotation;
      rb.velocity = data.velocity;
      rb.angularVelocity = data.angularVelocity;
    }
  }
}
}

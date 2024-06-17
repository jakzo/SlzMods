using MelonLoader;
using MelonLoader.TinyJSON;
using UnityEngine;
using System;
using System.Linq;
using StressLevelZero.Props.Weapons;
using System.Collections.Generic;

namespace Sst.Features {
class DebugStats : Feature {
  public Common.Boneworks.DebugStats Stats = new Common.Boneworks.DebugStats();

  private int _numUpdates = 0;
  private int _numFixedUpdates = 0;
  private int _numFixedUpdatesSinceLastUpdate = 0;
  private Common.Ipc.Server _ipcServer;

  private HandWeaponSlotReciever[] _weaponReceivers = {};

  public DebugStats() { IsDev = IsEnabledByDefault = true; }

  public override void OnApplicationStart() {
    _ipcServer = new Common.Ipc.Server(Common.Boneworks.DebugStats.NAMED_PIPE,
                                       new Logger());
    _ipcServer.OnClientConnected += stream => { Dbg.Log("OnClientConnected"); };
    _ipcServer.OnClientDisconnected +=
        stream => { Dbg.Log("OnClientDisconnected"); };
  }

  public override void OnSceneWasInitialized(int buildIndex, string sceneName) {
    Stats = new Common.Boneworks.DebugStats();
    _numUpdates = _numFixedUpdates = _numFixedUpdatesSinceLastUpdate = 0;

    _weaponReceivers =
        Utilities.Unity
            .FindAllInDescendants(
                Mod.GameState.rigManager.gameWorldSkeletonRig.gameObject,
                "WeaponReciever")
            .Select(wr => wr.GetComponent<HandWeaponSlotReciever>())
            .ToArray();
  }

  public override void OnUpdate() {
    _numUpdates++;

    Stats.fps = 1f / Time.deltaTime;
    Stats.physicsRate = 1f / Time.fixedDeltaTime;
    Stats.droppedFrames = _numFixedUpdates - _numUpdates;
    if (_numFixedUpdatesSinceLastUpdate == 0)
      Stats.extraFrames++;

    _numFixedUpdatesSinceLastUpdate = 0;

    _ipcServer.Send(Encoder.Encode(Stats));
  }

  public override void OnFixedUpdate() {
    _numFixedUpdates++;
    _numFixedUpdatesSinceLastUpdate++;

    if (IsMagNotTouching())
      Stats.numFixedUpdatesMagNotTouching++;
  }

  public bool IsMagNotTouching() {
    var physicsRig = Mod.GameState.rigManager?.physicsRig;
    if (!physicsRig)
      return false;
    var magSocket =
        _weaponReceivers?.FirstOrDefault(wr => wr.m_SlottedWeapon != null)
            ?.m_WeaponHost?.GetComponent<Gun>()
            ?.magazineSocket;
    if (!magSocket)
      return false;
    var magazines = new List<Magazine>();
    Utilities.Unity.FindDescendantComponentsOfType<Magazine>(
        ref magazines, magSocket.transform, true);
    var magazine = magazines.FirstOrDefault(
        mag => mag != magSocket._magazinePlug?.magazine);
    if (!magazine)
      return false;
    var colliders = new List<Collider>();
    Utilities.Unity.FindDescendantComponentsOfType<Collider>(
        ref colliders, magazine.transform, true);
    var playerColliders = physicsRig.m_chest.GetComponents<Collider>();
    return !playerColliders.Any(
        playerCol => colliders.Any(magCol => Physics.ComputePenetration(
                                       magCol, magCol.transform.position,
                                       magCol.transform.rotation, playerCol,
                                       playerCol.transform.position,
                                       playerCol.transform.rotation,
                                       out var direction, out var distance)));
  }

  class Logger : Common.Ipc.Logger {
    public override void Debug(string message) => Dbg.Log(message);
    public override void Error(string message) => MelonLogger.Error(message);
  }
}
}

using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using StressLevelZero.Rig;
using StressLevelZero.Pool;
using StressLevelZero.Props.Weapons;
using StressLevelZero.Interaction;
using StressLevelZero.Data;

namespace Sst.BoneworksPerformance.Debug.Gunfly
{
  public static class NativeWeapons
  {
    public static readonly Dictionary<string, Gun> Guns = new Dictionary<string, Gun>();
    public static Gun Current;
    public static string Prefab, Phase = "idle";
    public static Magazine Magazine;
    static HandWeaponSlotReciever slot;
    static InteractableHost original;
    static MagazinePlug pendingPlug;
    static bool originalCaptured;
    static GameObject pendingObject;
    static int phaseFrame;
    static void PhaseTo(string value)
    {
      Phase = value;
      phaseFrame = Time.frameCount;
    }

    public static bool Ready => Phase == "ready";

    public static void Prepare(string prefab)
    {
      slot = UnityEngine.Object.FindObjectsOfType<HandWeaponSlotReciever>().First(x => x.saveBodySlotType.ToString() == "RSHOULDER");
      if (!originalCaptured)
      {
        original = slot.m_WeaponHost;
        originalCaptured = true;
      }

      if (slot.m_WeaponHost)
      {
        var previous = slot.m_WeaponHost;
        slot.DropWeapon();
        previous.gameObject.SetActive(false);
      }

      Prefab = prefab;
      Magazine = null;
      // Native unequipping can detach the glitched magazine. Never reuse a cached gun after switching away.
      Current = null;
      SpawnableObject item = null;
      foreach (var c in PoolManager._registeredSpawnableObjects.Values)
      {
        if (c.prefab && c.prefab.name == prefab)
        {
          item = c;
          break;
        }
      }

      if (!item)
      {
        throw new InvalidOperationException("Unregistered rifle " + prefab);
      }

      var rig = UnityEngine.Object.FindObjectOfType<RigManager>();
      var spawned = PoolManager.Spawn(item.title, rig.physicsRig.m_head.position + new Vector3(3, 2, 0), Quaternion.identity, new Sst.Utilities.Il2CppNullable<bool>(null));
      if (!spawned)
      {
        throw new InvalidOperationException("Gun spawn failed");
      }

      Current = spawned.GetComponentInChildren<Gun>(true);
      if (!Current)
      {
        throw new InvalidOperationException("Spawned prefab has no Gun");
      }

      Guns[prefab] = Current;
      PhaseTo("gun_initializing");
    }

    public static void Tick()
    {
      if (Ready)
      {
        return;
      }

      if (Time.frameCount - phaseFrame > 900)
      {
        throw new InvalidOperationException("Weapon setup timed out in " + Phase);
      }

      if (Phase == "gun_initializing" && Time.frameCount - phaseFrame >= 3)
      {
        if (Current.magazineSocket._magazinePlug)
        {
          PhaseTo("seating");
          return;
        }

        var item = Current.spawnableMagazine;
        if (!item)
        {
          throw new InvalidOperationException("Missing matching magazine prefab");
        }

        pendingObject = PoolManager.Spawn(item.title, Current.transform.position, Current.transform.rotation, new Sst.Utilities.Il2CppNullable<bool>(null));
        if (!pendingObject)
        {
          throw new InvalidOperationException("Magazine spawn failed");
        }

        pendingPlug = pendingObject.GetComponentInChildren<MagazinePlug>(true);
        if (!pendingPlug)
        {
          throw new InvalidOperationException("Magazine has no plug");
        }

        PhaseTo("magazine_initializing");
        return;
      }

      if (Phase == "magazine_initializing" && Time.frameCount - phaseFrame >= 3)
      {
        var socket = Current.magazineSocket;
        var delta = socket.transform.rotation * Quaternion.Inverse(pendingPlug.transform.rotation);
        pendingObject.transform.position = socket.transform.position + delta * (pendingObject.transform.position - pendingPlug.transform.position);
        pendingObject.transform.rotation = delta * pendingObject.transform.rotation;
        pendingPlug.InsertPlug(socket);
        PhaseTo("seating");
        return;
      }

      if (Phase == "seating")
      {
        pendingPlug = Current.magazineSocket._magazinePlug;
        if (!pendingPlug)
        {
          return;
        }

        if (pendingPlug._isEnterTransition)
        {
          return;
        }

        Current.magazineSocket.MagazineRelease();
        PhaseTo("eject_stage_one");
        return;
      }

      if (Phase == "eject_stage_one")
      {
        if (!pendingPlug._isStageTwoAnimation)
        {
          return;
        }

        pendingPlug._isExitTransition = false;
        pendingPlug._isStageTwoAnimation = false;
        pendingPlug._lastSocket = null;
        if (Current.magazineSocket.proxyGrip)
        {
          Current.magazineSocket.proxyGrip.ForceDetach();
        }

        Current.magazineSocket.Unlock();
        slot.OnInteractableHostDrop(Current.host);
        PhaseTo("holstering");
        return;
      }

      if (Phase == "holstering" && Time.frameCount - phaseFrame > 6)
      {
        slot.ReturnFromUI();
        Verify();
        PhaseTo("ready");
      }
    }

    static void Verify()
    {
      if (!slot.m_WeaponHost || slot.m_WeaponHost.Pointer != Current.host.Pointer)
      {
        throw new InvalidOperationException("Shoulder rejected weapon");
      }

      Magazine = Current.GetComponentsInChildren<Magazine>(true).FirstOrDefault(x => x.gameObject.activeInHierarchy);
      if (!Magazine || !Magazine.GetComponentsInChildren<Collider>(true).Any(c => c.enabled && !c.isTrigger && c.gameObject.layer == 10))
      {
        throw new InvalidOperationException("Missing enabled glitched magazine collider");
      }

      var plug = Magazine.GetComponentInChildren<MagazinePlug>(true);
      if (plug && (plug._isEnterTransition || plug._isExitTransition))
      {
        throw new InvalidOperationException("Magazine animation still active");
      }
    }

    public static string Geometry()
    {
      var s = new System.Text.StringBuilder();
      s.AppendLine("prefab=" + Prefab + " magazine=" + Magazine.name + " local=" + FormatVector(Magazine.transform.localPosition));
      s.AppendLine("host_local=" + FormatVector(Current.host.transform.localPosition) + " host_rotation=" + FormatQuaternion(Current.host.transform.localRotation));
      foreach (var c in Magazine.GetComponentsInChildren<Collider>(true))
      {
        if (c.enabled && !c.isTrigger)
        {
          s.AppendLine("collider=" + c.name + " layer=" + c.gameObject.layer + " local=" + FormatVector(c.transform.localPosition) + " rotation=" + FormatQuaternion(c.transform.localRotation) + " scale=" + FormatVector(c.transform.localScale));
          var b = c.TryCast<BoxCollider>();
          if (b)
          {
            s.AppendLine("box_center=" + FormatVector(b.center) + " size=" + FormatVector(b.size));
          }
        }
      }

      return s.ToString();
    }

    static string FormatVector(Vector3 v) => v.x.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + "," + v.y.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + "," + v.z.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
    static string FormatQuaternion(Quaternion q) => FormatVector(new Vector3(q.x, q.y, q.z)) + "," + q.w.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
    public static void RestoreOriginal()
    {
      if (slot && slot.m_WeaponHost)
      {
        var previous = slot.m_WeaponHost;
        slot.DropWeapon();
        previous.gameObject.SetActive(false);
      }

      foreach (var gun in Guns.Values)
      {
        if (gun)
        {
          gun.gameObject.SetActive(false);
        }
      }

      if (original && slot)
      {
        original.gameObject.SetActive(true);
        slot.OnInteractableHostDrop(original);
      }
    }
  }
}

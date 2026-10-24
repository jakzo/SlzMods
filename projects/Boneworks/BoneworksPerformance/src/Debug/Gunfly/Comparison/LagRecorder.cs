using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using HarmonyLib;
using StressLevelZero.Rig;
using StressLevelZero.Props.Weapons;
using UnityEngine;

namespace Sst.BoneworksPerformance.Debug.Gunfly
{
  // Passive transform recorder. No transform, physics, input, or mod-state writes.
  public static class LagRecorder
  {
    struct Sample
    {
      public double Time;
      public int Frame, TickInFrame, Phase;
      public long Tick;
      public Vector3 Chest, Parent, Slot, Host, Mag, MagInHost, HostInChest, HostLocal;
      public Quaternion ChestQ, ParentQ, HostQ, MagQ, MagInHostQ, HostLocalQ;
      public Vector3 Foot, TrackedLocal, PhysicsHead;
      public Quaternion TrackedLocalQ;
      public float FixedTime, GameTime;
      public double CommandHeight, CommandAngle, CommandYaw;
    }

    static readonly List<Sample> rows = new List<Sample>(25000);
    static RigManager rig;
    static HandWeaponSlotReciever receiver;
    static int lastFrame = -1, tickInFrame;
    static long tick;
    static readonly string[] phases =
    {
      "fixed_enter_before_mod",
      "fixed_before_native_after_mod",
      "fixed_exit",
      "late_enter_before_restore",
      "late_before_native_after_restore",
      "late_exit"
    };
    public static void Install(RigManager value, HarmonyLib.Harmony harmony)
    {
      rig = value;
      Patch(harmony, "FixedUpdate", "FixedEnter", false, 800);
      Patch(harmony, "FixedUpdate", "FixedReady", false, 0);
      Patch(harmony, "FixedUpdate", "FixedExit", true, 0);
      Patch(harmony, "LateUpdate", "LateEnter", false, 800);
      Patch(harmony, "LateUpdate", "LateReady", false, 0);
      Patch(harmony, "LateUpdate", "LateExit", true, 0);
    }

    static void Patch(HarmonyLib.Harmony h, string target, string patch, bool post, int priority)
    {
      var m = new HarmonyMethod(typeof(LagRecorder).GetMethod(patch));
      m.priority = priority;
      if (post)
      {
        h.Patch(AccessTools.Method(typeof(RigManager), target), postfix: m);
      } else
      {
        h.Patch(AccessTools.Method(typeof(RigManager), target), prefix: m);
      }
    }

    public static void Begin()
    {
      rows.Clear();
      tick = 0;
      tickInFrame = 0;
      lastFrame = -1;
      var gun = NativeWeapons.Current;
      receiver = UnityEngine.Object.FindObjectsOfType<HandWeaponSlotReciever>().FirstOrDefault(x => x.m_WeaponHost && x.m_WeaponHost == gun.host);
    }

    public static void FixedEnter()
    {
      if (!ComparisonRunner.Recording)
      {
        return;
      }

      int frame = Time.frameCount;
      if (lastFrame != frame)
      {
        lastFrame = frame;
        tickInFrame = 0;
      }

      tickInFrame++;
      tick++;
      Record(0);
    }

    public static void FixedReady()
    {
      Record(1);
    }

    public static void FixedExit()
    {
      Record(2);
    }

    public static void LateEnter()
    {
      Record(3);
    }

    public static void LateReady()
    {
      Record(4);
    }

    public static void LateExit()
    {
      Record(5);
    }

    static void Record(int phase)
    {
      if (!ComparisonRunner.Recording)
      {
        return;
      }

      double t = ComparisonRunner.Elapsed;
      if (t < 10 || t > 40)
      {
        return;
      }

      var gun = NativeWeapons.Current;
      var mag = NativeWeapons.Magazine;
      if (!gun || !mag || !gun.host)
      {
        return;
      }

      var host = gun.host.transform;
      if (!receiver)
      {
        return;
      }

      var chest = rig.physicsRig.m_chest;
      var parent = receiver.transform.parent;
      var tracked = rig.ControllerRig.m_head.parent;
      var command = ComparisonRunner.Command;
      rows.Add(new Sample { Time = t, Frame = Time.frameCount, Tick = tick, TickInFrame = tickInFrame, Phase = phase, Chest = chest.position, ChestQ = chest.rotation, Parent = parent.position, ParentQ = parent.rotation, Slot = receiver.transform.position, Host = host.position, HostQ = host.rotation, Mag = mag.transform.position, MagQ = mag.transform.rotation, MagInHost = host.InverseTransformPoint(mag.transform.position), MagInHostQ = Quaternion.Inverse(host.rotation) * mag.transform.rotation, HostInChest = chest.InverseTransformPoint(host.position), HostLocal = host.localPosition, HostLocalQ = host.localRotation, Foot = rig.physicsRig.physBody.feet.transform.position - Vector3.up * rig.physicsRig.physBody.footballRadius, TrackedLocal = tracked.localPosition, TrackedLocalQ = tracked.localRotation, PhysicsHead = rig.physicsRig.m_head.position, FixedTime = Time.fixedTime, GameTime = Time.time, CommandHeight = command.Height, CommandAngle = command.Angle, CommandYaw = command.Yaw });
    }

    static string FormatNumber(double v) => v.ToString("R", CultureInfo.InvariantCulture);
    static string FormatVector(Vector3 p) => FormatNumber(p.x) + "," + FormatNumber(p.y) + "," + FormatNumber(p.z);
    static string FormatQuaternion(Quaternion q) => FormatNumber(q.x) + "," + FormatNumber(q.y) + "," + FormatNumber(q.z) + "," + FormatNumber(q.w);
    public static string Geometry()
    {
      var g = NativeWeapons.Current;
      var m = NativeWeapons.Magazine;
      if (!g || !m)
      {
        return "no gun";
      }

      var h = g.host.transform;
      return "mag_in_host=" + FormatVector(h.InverseTransformPoint(m.transform.position)) + "\nmag_rotation_in_host=" + FormatQuaternion(Quaternion.Inverse(h.rotation) * m.transform.rotation) + "\nhost_parent=" + (h.parent ? h.parent.name : "none") + "\nmag_parent=" + (m.transform.parent ? m.transform.parent.name : "none");
    }

    public static void Save()
    {
      using (var writer = new StreamWriter(Path.Combine(ComparisonRunner.CaptureDirectory, "slot-phases.csv")))
      {
        writer.WriteLine("elapsed_s,unity_frame,physics_tick,tick_in_frame,phase,chest_x,chest_y,chest_z,chest_qx,chest_qy,chest_qz,chest_qw,parent_x,parent_y,parent_z,parent_qx,parent_qy,parent_qz,parent_qw,slot_x,slot_y,slot_z,host_x,host_y,host_z,host_qx,host_qy,host_qz,host_qw,mag_x,mag_y,mag_z,mag_qx,mag_qy,mag_qz,mag_qw,mag_host_x,mag_host_y,mag_host_z,mag_host_qx,mag_host_qy,mag_host_qz,mag_host_qw,host_chest_x,host_chest_y,host_chest_z,host_local_x,host_local_y,host_local_z,host_local_qx,host_local_qy,host_local_qz,host_local_qw,foot_x,foot_y,foot_z,tracked_x,tracked_y,tracked_z,tracked_qx,tracked_qy,tracked_qz,tracked_qw,physics_head_x,physics_head_y,physics_head_z,fixed_time,game_time,command_height,command_angle,command_yaw");
        foreach (var r in rows)
        {
          writer.WriteLine(FormatNumber(r.Time) + "," + r.Frame + "," + r.Tick + "," + r.TickInFrame + "," + phases[r.Phase] + "," + FormatVector(r.Chest) + "," + FormatQuaternion(r.ChestQ) + "," + FormatVector(r.Parent) + "," + FormatQuaternion(r.ParentQ) + "," + FormatVector(r.Slot) + "," + FormatVector(r.Host) + "," + FormatQuaternion(r.HostQ) + "," + FormatVector(r.Mag) + "," + FormatQuaternion(r.MagQ) + "," + FormatVector(r.MagInHost) + "," + FormatQuaternion(r.MagInHostQ) + "," + FormatVector(r.HostInChest) + "," + FormatVector(r.HostLocal) + "," + FormatQuaternion(r.HostLocalQ) + "," + FormatVector(r.Foot) + "," + FormatVector(r.TrackedLocal) + "," + FormatQuaternion(r.TrackedLocalQ) + "," + FormatVector(r.PhysicsHead) + "," + FormatNumber(r.FixedTime) + "," + FormatNumber(r.GameTime) + "," + FormatNumber(r.CommandHeight) + "," + FormatNumber(r.CommandAngle) + "," + FormatNumber(r.CommandYaw));
        }
      }
    }
  }
}

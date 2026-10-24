using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using HarmonyLib;
using MelonLoader;
using StressLevelZero;
using StressLevelZero.Rig;
using UnityEngine;
using Valve.VR;
using UnhollowerBaseLib;
using UnhollowerRuntimeLib;

namespace Sst.BoneworksPerformance.Debug.Gunfly
{
  public static class ComparisonRunner
  {
    sealed class Configuration
    {
      public string Gun;
      public int Hz;
      public float Angle, Height, Stick;
      public string Id;
      public string Mode;
    }

    public static bool Recording => active && attemptActive;
    public static double Elapsed => clock.Elapsed.TotalSeconds - attemptBegan;
    public static int PhysicsHz => currentHz;
    public static string CaptureDirectory => caseDirectory;

    static string condition = "absent";
    static int mainThread;
    static int RenderDivisor => condition == "half" ? 2 : 1;

    sealed class InputRun
    {
      public string Mode;
      public long Started;
    }

    static volatile InputRun inputRun;
    public static ScheduledPose Command
    {
      get
      {
        var run = inputRun;
        return InputSchedule.Evaluate(run == null ? "straight" : run.Mode, run == null ? 0 : (Stopwatch.GetTimestamp() - run.Started) / (double)Stopwatch.Frequency);
      }
    }

    public static string StartComparison(string path, string value)
    {
      HarnessCleanup.RequireIdle();
      ModControl.Validate(value);
      ValidateRequests(path);
      condition = value;
      retryPath = path;
      try
      {
        return StartInternal();
      } catch
      {
        active = false;
        attemptActive = false;
        inputRun = null;
        try
        {
          if (directory != null)
          {
            Restore();
          } else
          {
            ModControl.Restore();
          }
        } finally
        {
          HarnessCleanup.RemoveHooks();
        }

        throw;
      }
    }

    static void ValidateRequests(string path)
    {
      var ids = new HashSet<string>();
      foreach (var line in File.ReadAllLines(path))
      {
        if (string.IsNullOrWhiteSpace(line))
        {
          continue;
        }

        var f = line.Split('\t');
        int hz;
        float angle, height, stick;
        if (f.Length != 7 || !ids.Add(f[0]) || !MatrixPlan.SafeId(f[0]) || !MatrixPlan.IsSupportedGun(f[1]) || !int.TryParse(f[2], out hz) || !MatrixPlan.Rates.Contains(hz) || !float.TryParse(f[3], NumberStyles.Float, CultureInfo.InvariantCulture, out angle) || float.IsNaN(angle) || angle != 45 || !float.TryParse(f[4], NumberStyles.Float, CultureInfo.InvariantCulture, out height) || height != 1.76f || !float.TryParse(f[5], NumberStyles.Float, CultureInfo.InvariantCulture, out stick) || stick != -1 || !new[]
        {
          "straight",
          "yaw-turn",
          "pitch-height"
        }.Contains(f[6]))
        {
          throw new ArgumentException("Expected id, native gun, Hz, 45, 1.76, -1, straight/yaw-turn/pitch-height separated by tabs.");
        }
      }

      if (ids.Count == 0)
      {
        throw new ArgumentException("Empty comparison request.");
      }
    }

    static readonly List<Configuration> cases = new List<Configuration>();
    static readonly List<Observation> samples = new List<Observation>(16000);
    static readonly List<string> results = new List<string>();
    static readonly List<Collider> disabledColliders = new List<Collider>();
    static readonly List<Renderer> disabledRenderers = new List<Renderer>();
    static readonly HashSet<int> magazineColliders = new HashSet<int>();
    static readonly Stopwatch clock = Stopwatch.StartNew();
    static RigManager rig;
    static Rigidbody pelvis;
    static StressLevelZero.Rig.Controller left, right;
    static GameObject pad;
    static HarmonyLib.Harmony harmony;
    static bool active, attemptActive, initialized, restored;
    public static bool IsActive => active;

    static string phase = "idle", directory, caseDirectory, reason = "not_started", currentGun;
    static int index, attempt, validCount, failedCount, setupFailedCount, currentHz = 120;
    static long ticks, lastMagTick = -1, posePolls, totalContacts;
    static uint leftIndex, rightIndex;
    static double attemptBegan, lastFrame, nextFrame, phaseBegan;
    static Vector3 impulse;
    static float savedHeight, savedFloor, savedStep, savedTimeScale;
    static bool savedStanding, savedXr, savedLock;
    static int savedFps, savedVsync, savedCaptureRate;
    static Vector3 savedFeet;
    static Window lastWindow;
    static string FormatNumber(double x) => double.IsNaN(x) || double.IsInfinity(x) ? "" : x.ToString("R", CultureInfo.InvariantCulture);
    static string QuoteCsv(string s) => "\"" + (s ?? "").Replace("\"", "\"\"") + "\"";
    static string QuoteJson(string s) => "\"" + (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n") + "\"";
    const string Header = "case_id,condition,rifle_prefab,requested_hz,look_angle_from_down_deg,hmd_height_m,player_height_m,status,attempts,reason,window_seconds,render_fps,physics_hz,world_horizontal_median_m_s,world_vertical_median_m_s,world_vx_median_m_s,world_vz_median_m_s,rigidbody_horizontal_median_m_s,rigidbody_vertical_median_m_s,rigidbody_vx_median_m_s,rigidbody_vz_median_m_s,world_horizontal_p95_minus_p05_m_s,world_vertical_p95_minus_p05_m_s,world_lateral_p95_minus_p05_m_s,world_horizontal_half_window_drift_m_s,world_vertical_half_window_drift_m_s,lateral_drift_flag,source_attempt,frame_log";
    static string retryPath;
    static string StartInternal()
    {
      HarnessCleanup.RequireIdle();
      if (active)
      {
        return Status();
      }

      if (initialized)
      {
        throw new InvalidOperationException("Restart the game before starting another independent batch.");
      }

      initialized = true;
      rig = UnityEngine.Object.FindObjectOfType<RigManager>();
      if (!rig)
      {
        throw new InvalidOperationException("No native RigManager");
      }

      mainThread = System.Threading.Thread.CurrentThread.ManagedThreadId;
      HarnessCleanup.RemoveHooks();
      left = rig.steamControllerRig.leftController.TryCast<StressLevelZero.Rig.Controller>();
      right = rig.steamControllerRig.rightController.TryCast<StressLevelZero.Rig.Controller>();
      leftIndex = left.deviceIndex;
      rightIndex = right.deviceIndex;
      if (leftIndex >= 10 || rightIndex >= 10 || leftIndex == rightIndex)
      {
        throw new InvalidOperationException("Controller role compatibility is not initialized correctly");
      }

      pelvis = rig.physicsRig.m_pelvis.GetComponent<Rigidbody>();
      var data = Data_Manager.Instance.data_player;
      savedHeight = data.player_Height;
      savedFloor = data.offset_Floor;
      savedStanding = data.standing;
      savedStep = Time.fixedDeltaTime;
      savedTimeScale = Time.timeScale;
      savedFps = Application.targetFrameRate;
      savedVsync = QualitySettings.vSyncCount;
      savedCaptureRate = Time.captureFramerate;
      savedXr = UnityEngine.XR.XRSettings.enabled;
      savedLock = Resources.Load<SteamVR_Settings>("SteamVR_Settings").lockPhysicsUpdateRateToRenderFrequency;
      savedFeet = rig.physicsRig.physBody.feet.transform.position - Vector3.up * rig.physicsRig.physBody.footballRadius;
      directory = Path.Combine(MelonUtils.UserDataDirectory, "GunflyMeasurements", "turning-ab-" + condition + "-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff"));
      Directory.CreateDirectory(directory);
      foreach (var line in File.ReadAllLines(retryPath))
      {
        if (string.IsNullOrWhiteSpace(line))
        {
          continue;
        }

        var fields = line.Split('\t');
        if (fields.Length != 7)
        {
          throw new InvalidOperationException("Invalid retry request");
        }

        int hz = int.Parse(fields[2], CultureInfo.InvariantCulture);
        float a = float.Parse(fields[3], CultureInfo.InvariantCulture);
        if (!MatrixPlan.IsSupportedGun(fields[1]) || !MatrixPlan.Rates.Contains(hz) || (a < -15 || a > 120) || cases.Any(c => c.Id == fields[0]))
        {
          throw new InvalidOperationException("Unknown or duplicate configuration");
        }

        cases.Add(new Configuration { Gun = fields[1], Hz = hz, Angle = a, Height = float.Parse(fields[4], CultureInfo.InvariantCulture), Stick = float.Parse(fields[5], CultureInfo.InvariantCulture), Id = fields[0], Mode = fields[6] });
      }

      if (cases.Count == 0)
      {
        throw new InvalidOperationException("Empty retry subset");
      }

      if (cases.Any(c => c.Height < .8f || c.Height > 1.76f || c.Stick < -1 || c.Stick > 0))
      {
        throw new InvalidOperationException("Diagnostic pose out of declared bounds");
      }

      results.Add(Header);
      File.Copy(retryPath, Path.Combine(directory, "retry-requests.tsv"));
      File.WriteAllText(Path.Combine(directory, "retry-source.txt"), retryPath);
      ModControl.Configure(condition);
      NativeInputs.CrouchAxis = -1;
      File.WriteAllText(Path.Combine(directory, "ab-protocol.txt"), "Open-loop comparison, 40 seconds per case; angle45, HMD1.76m. Cases and condition recorded verbatim. Absent/full render at requested physics Hz; half renders at half physics Hz throughout. Rightstick up2s then down; left backward sprint after2s. Pad removed10s. Synthetic poses injected at OpenVR API on both main and background threads using the same stopwatch schedule. Straight/yaw-turn/pitch-height schedules in InputSchedule.cs. Passive fixed/late slot and magazine recording10..40s; fixed_exit is after RigManager, BEFORE physics solver. Native gun/magazine geometry unchanged; scene environment disabled with native weapon colliders preserved. Diagnostic only, no scored stable-flight acceptance.\n" + ModControl.Describe());
      pad = GameObject.Find("Gunfly matrix launch pad");
      if (!pad)
      {
        pad = GameObject.CreatePrimitive(PrimitiveType.Cube);
        pad.name = "Gunfly matrix launch pad";
        pad.layer = LayerMask.NameToLayer("Static");
        pad.transform.position = new Vector3(0, -.1f, 0);
      }

      pad.transform.localScale = new Vector3(6, .2f, 6);
      foreach (var c in UnityEngine.Object.FindObjectsOfType<Collider>())
      {
        if (c.enabled && c.gameObject.scene.IsValid() && !c.transform.IsChildOf(rig.transform) && c.gameObject != pad && !c.GetComponentInParent<StressLevelZero.Props.Weapons.Gun>() && !c.GetComponentInParent<StressLevelZero.Props.Weapons.Magazine>())
        {
          disabledColliders.Add(c);
          c.enabled = false;
        }
      }

      foreach (var r in UnityEngine.Object.FindObjectsOfType<Renderer>())
      {
        if (r.enabled && !r.transform.IsChildOf(rig.transform) && r.gameObject != pad)
        {
          disabledRenderers.Add(r);
          r.enabled = false;
        }
      }

      data.player_Height = 176;
      data.offset_Floor = 0;
      data.standing = true;
      Data_Manager.Instance.control_player.SendHeight();
      Data_Manager.Instance.control_player.STANDING();
      MagazineCollisionObserver.Attach(rig);
      harmony = new HarmonyLib.Harmony("jakzo.gunfly.benchmarks.comparison");
      Patch(typeof(CVRSystem), "GetDeviceToAbsoluteTrackingPose", "Pose", true);
      Patch(typeof(CVRSystem), "IsTrackedDeviceConnected", "Connected", false);
      Patch(typeof(RigManager), "Update", "BeforeUpdate", false);
      Patch(typeof(RigManager), "FixedUpdate", "BeforeFixed", false);
      Patch(typeof(RigManager), "LateUpdate", "AfterFrame", true);
      LagRecorder.Install(rig, harmony);
      NativeInputs.Initialize();
      UnityEngine.XR.XRSettings.enabled = false;
      active = true;
      nextFrame = phaseBegan = clock.Elapsed.TotalSeconds;
      phase = "next_case";
      Checkpoint();
      return Status();
    }

    static void Patch(Type t, string method, string patch, bool post)
    {
      var p = new HarmonyMethod(typeof(ComparisonRunner).GetMethod(patch));
      if (post)
      {
        harmony.Patch(AccessTools.Method(t, method), postfix: p);
      } else
      {
        harmony.Patch(AccessTools.Method(t, method), prefix: p);
      }
    }

    public static bool Connected(uint __0, ref bool __result)
    {
      if (!active || (__0 != 0 && __0 != leftIndex && __0 != rightIndex))
      {
        return true;
      }

      __result = true;
      return false;
    }

    public static void BeforeUpdate()
    {
      if (!active)
      {
        return;
      }

      try
      {
        ApplyRate();
        double now = clock.Elapsed.TotalSeconds;
        if (now - nextFrame > RenderDivisor / (double)currentHz)
        {
          nextFrame = now;
        }

        while (clock.Elapsed.TotalSeconds < nextFrame)
        {
          if (nextFrame - clock.Elapsed.TotalSeconds > .002)
          {
            System.Threading.Thread.Sleep(1);
          } else
          {
            System.Threading.Thread.SpinWait(32);
          }
        }

        nextFrame += RenderDivisor / (double)currentHz;
      } catch (Exception e)
      {
        Abort("pacing_exception_" + e);
      }
    }

    static void ApplyRate()
    {
      ModControl.SetRate(currentHz);
      Resources.Load<SteamVR_Settings>("SteamVR_Settings").lockPhysicsUpdateRateToRenderFrequency = false;
      Time.timeScale = 1;
      Time.captureFramerate = currentHz / RenderDivisor;
      Time.fixedDeltaTime = 1f / currentHz;
      Application.targetFrameRate = -1;
      QualitySettings.vSyncCount = 0;
    }

    public static void BeforeFixed()
    {
      if (!active)
      {
        return;
      }

      ticks++;
      ApplyRate();
    }

    public static void Pose(Il2CppStructArray<TrackedDevicePose_t> __2)
    {
      if (!active || __2 == null || __2.Length == 0)
      {
        return;
      }

      if (System.Threading.Thread.CurrentThread.ManagedThreadId != mainThread)
      {
        ModControl.NoteBackgroundPose();
      } else
      {
        posePolls++;
      }

      var matrix = InputSchedule.OpenVrMatrix(Command);
      var head = __2[0];
      head.bDeviceIsConnected = true;
      head.bPoseIsValid = true;
      head.eTrackingResult = ETrackingResult.Running_OK;
      head.vVelocity = new HmdVector3_t();
      head.vAngularVelocity = new HmdVector3_t();
      head.mDeviceToAbsoluteTracking = new HmdMatrix34_t
      {
        m0 = (float)matrix[0],
        m1 = (float)matrix[1],
        m2 = (float)matrix[2],
        m3 = (float)matrix[3],
        m4 = (float)matrix[4],
        m5 = (float)matrix[5],
        m6 = (float)matrix[6],
        m7 = (float)matrix[7],
        m8 = (float)matrix[8],
        m9 = (float)matrix[9],
        m10 = (float)matrix[10],
        m11 = (float)matrix[11]
      };
      __2[0] = head;
      SetHand(__2, leftIndex, -.4f);
      SetHand(__2, rightIndex, .4f);
    }

    static void SetHand(Il2CppStructArray<TrackedDevicePose_t> poses, uint index, float x)
    {
      if (index >= poses.Length)
      {
        return;
      }

      var p = poses[(int)index];
      p.bDeviceIsConnected = true;
      p.bPoseIsValid = true;
      p.eTrackingResult = ETrackingResult.Running_OK;
      p.vVelocity = new HmdVector3_t();
      p.vAngularVelocity = new HmdVector3_t();
      p.mDeviceToAbsoluteTracking = new HmdMatrix34_t
      {
        m0 = 1,
        m3 = x,
        m5 = 1,
        m7 = 1.2f,
        m10 = 1,
        m11 = -.3f
      };
      poses[(int)index] = p;
    }

    public static void Contact(Collision c)
    {
      if (!active || !attemptActive || c == null)
      {
        return;
      }

      var collider = c.collider;
      if (collider && magazineColliders.Contains(collider.GetInstanceID()))
      {
        lastMagTick = ticks;
        totalContacts++;
        impulse += c.impulse;
      }
    }

    public static void AfterFrame()
    {
      if (!active)
      {
        return;
      }

      try
      {
        double now = clock.Elapsed.TotalSeconds;
        if (phase == "next_case")
        {
          if (now - phaseBegan < 1)
          {
            return;
          }

          if (index >= cases.Count)
          {
            Complete();
            return;
          }

          currentHz = cases[index].Hz;
          attempt = 0;
          caseDirectory = Path.Combine(directory, cases[index].Id + "-" + cases[index].Gun + "-" + currentHz + "Hz-" + cases[index].Angle + "deg");
          Directory.CreateDirectory(caseDirectory);
          pad.SetActive(true);
          rig.Teleport(Vector3.zero, true);
          ZeroBodies();
          if (currentGun != cases[index].Gun)
          {
            currentGun = cases[index].Gun;
            NativeWeapons.Prepare(currentGun);
            phase = "weapon_setup";
            phaseBegan = now;
          } else
          {
            BeginAttempt();
          }

          Checkpoint();
          return;
        }

        if (phase == "weapon_setup")
        {
          try
          {
            NativeWeapons.Tick();
            if (NativeWeapons.Ready)
            {
              File.WriteAllText(Path.Combine(caseDirectory, "geometry.txt"), NativeWeapons.Geometry());
              BeginAttempt();
            }
          } catch (Exception e)
          {
            SetupFailure(e.ToString());
          }

          return;
        }

        if (phase == "retry_wait")
        {
          if (now - phaseBegan >= 1)
          {
            BeginAttempt();
          }

          return;
        }

        if (!attemptActive)
        {
          return;
        }

        double elapsed = now - attemptBegan;
        if (elapsed >= 10 && pad.activeSelf)
        {
          pad.SetActive(false);
        }

        var p = pelvis.position;
        var v = pelvis.velocity;
        var raw = rig.ControllerRig.m_head.parent;
        Vector3 forward = raw.localRotation * Vector3.forward;
        double actualAngle = Math.Atan2(Math.Sqrt(forward.x * forward.x + forward.z * forward.z), -forward.y) * 180 / Math.PI;
        var row = new Observation
        {
          Time = elapsed,
          Dt = now - lastFrame,
          Tick = ticks,
          X = p.x,
          Y = p.y,
          Z = p.z,
          Rbx = v.x,
          Rby = v.y,
          Rbz = v.z,
          RawHeight = raw.localPosition.y,
          Angle = actualAngle,
          LeftX = left._thumbstickAxis.x,
          LeftY = left._thumbstickAxis.y,
          RightX = right._thumbstickAxis.x,
          RightY = right._thumbstickAxis.y,
          Sprint = left._thumbstick,
          Jog = rig.ControllerRig._jogToggle,
          MagazineContact = lastMagTick == ticks
        };
        int velocityStart = samples.Count - 1;
        while (velocityStart > 0 && ticks - samples[velocityStart].Tick < Math.Max(5, currentHz / 10))
        {
          velocityStart--;
        }

        if (velocityStart >= 0)
        {
          var previous = samples[velocityStart];
          double dt = (ticks - previous.Tick) / (double)currentHz;
          if (dt > 0 && elapsed - previous.Time < .5)
          {
            row.Vx = (p.x - previous.X) / dt;
            row.Vy = (p.y - previous.Y) / dt;
            row.Vz = (p.z - previous.Z) / dt;
            row.VelocityValid = true;
          }
        }

        row.ControllerX = rig.ControllerRig.transform.position.x;
        row.ControllerY = rig.ControllerRig.transform.position.y;
        row.ControllerZ = rig.ControllerRig.transform.position.z;
        row.ControllerYaw = rig.ControllerRig.transform.eulerAngles.y;
        row.FeetOffset = rig.ControllerRig.feetOffset;
        row.HeadX = rig.ControllerRig.m_head.position.x;
        row.HeadY = rig.ControllerRig.m_head.position.y;
        row.HeadZ = rig.ControllerRig.m_head.position.z;
        row.HeadFx = rig.ControllerRig.m_head.forward.x;
        row.HeadFy = rig.ControllerRig.m_head.forward.y;
        row.HeadFz = rig.ControllerRig.m_head.forward.z;
        row.ChestFx = rig.physicsRig.m_chest.forward.x;
        row.ChestFy = rig.physicsRig.m_chest.forward.y;
        row.ChestFz = rig.physicsRig.m_chest.forward.z;
        row.FixedTime = Time.fixedTime;
        row.GameTime = Time.time;
        var foot = rig.physicsRig.physBody.feet.transform.position;
        row.FootX = foot.x;
        row.FootY = foot.y - rig.physicsRig.physBody.footballRadius;
        row.FootZ = foot.z;
        if (row.VelocityValid && velocityStart >= 0)
        {
          var previousFoot = samples[velocityStart];
          double footDt = (ticks - previousFoot.Tick) / (double)currentHz;
          row.FootVx = (row.FootX - previousFoot.FootX) / footDt;
          row.FootVy = (row.FootY - previousFoot.FootY) / footDt;
          row.FootVz = (row.FootZ - previousFoot.FootZ) / footDt;
        }

        samples.Add(row);
        var scheduled = Command;
        row.CommandHeight = scheduled.Height;
        row.CommandAngle = scheduled.Angle;
        row.CommandYaw = scheduled.Yaw;
        samples[samples.Count - 1] = row;
        lastFrame = now;
        if (elapsed >= 40 || samples.Count >= 22000)
        {
          FinishAttempt("diagnostic_recorded");
          return;
        }
      } catch (Exception e)
      {
        Abort("runner_exception_" + e);
      }
    }

    static void BeginAttempt()
    {
      attempt++;
      samples.Clear();
      lastWindow = null;
      lastMagTick = -1;
      totalContacts = 0;
      impulse = Vector3.zero;
      magazineColliders.Clear();
      foreach (var c in NativeWeapons.Magazine.GetComponentsInChildren<Collider>(true))
      {
        if (c.enabled && !c.isTrigger && c.gameObject.layer == 10)
        {
          magazineColliders.Add(c.GetInstanceID());
        }
      }

      pad.SetActive(true);
      rig.Teleport(Vector3.zero, true);
      ZeroBodies();
      ticks = 0;
      posePolls = 0;
      LagRecorder.Begin();
      File.WriteAllText(Path.Combine(caseDirectory, "geometry-start.txt"), NativeWeapons.Geometry() + "\n" + LagRecorder.Geometry());
      attemptBegan = lastFrame = clock.Elapsed.TotalSeconds;
      inputRun = new InputRun
      {
        Mode = cases[index].Mode,
        Started = Stopwatch.GetTimestamp()
      };
      phase = "attempt";
      attemptActive = true;
      reason = "warming_up";
      NativeInputs.StartProbe(45);
      Checkpoint();
    }

    static void ZeroBodies()
    {
      foreach (var b in rig.GetComponentsInChildren<Rigidbody>(true))
      {
        if (!b.isKinematic)
        {
          b.velocity = Vector3.zero;
          b.angularVelocity = Vector3.zero;
        }
      }
    }

    static string FramePath() => Path.Combine(caseDirectory, "attempt-" + attempt.ToString("D2") + "-frames.csv");
    static void SaveFrames()
    {
      using (var output = new StreamWriter(FramePath()))
      {
        output.WriteLine("elapsed_s,render_dt_s,fixed_tick,world_x_m,world_y_m,world_z_m,world_vx_m_s,world_vy_m_s,world_vz_m_s,rigidbody_vx_m_s,rigidbody_vy_m_s,rigidbody_vz_m_s,raw_hmd_y_m,look_angle_from_down_deg,left_x,left_y,right_x,right_y,sprint_held,native_jog,magazine_contact,velocity_valid,controller_x,controller_y,controller_z,controller_yaw,feet_offset,head_x,head_y,head_z,head_fx,head_fy,head_fz,chest_fx,chest_fy,chest_fz,fixed_time,game_time,foot_x,foot_y,foot_z,command_height,command_angle,command_yaw,route_error,foot_vx_game_m_s,foot_vy_game_m_s,foot_vz_game_m_s");
        foreach (var s in samples)
        {
          output.WriteLine(string.Join(",", new[] { FormatNumber(s.Time), FormatNumber(s.Dt), s.Tick.ToString(), FormatNumber(s.X), FormatNumber(s.Y), FormatNumber(s.Z), s.VelocityValid ? FormatNumber(s.Vx) : "", s.VelocityValid ? FormatNumber(s.Vy) : "", s.VelocityValid ? FormatNumber(s.Vz) : "", FormatNumber(s.Rbx), FormatNumber(s.Rby), FormatNumber(s.Rbz), FormatNumber(s.RawHeight), FormatNumber(s.Angle), FormatNumber(s.LeftX), FormatNumber(s.LeftY), FormatNumber(s.RightX), FormatNumber(s.RightY), s.Sprint ? "1" : "0", s.Jog ? "1" : "0", s.MagazineContact ? "1" : "0", s.VelocityValid ? "1" : "0", FormatNumber(s.ControllerX), FormatNumber(s.ControllerY), FormatNumber(s.ControllerZ), FormatNumber(s.ControllerYaw), FormatNumber(s.FeetOffset), FormatNumber(s.HeadX), FormatNumber(s.HeadY), FormatNumber(s.HeadZ), FormatNumber(s.HeadFx), FormatNumber(s.HeadFy), FormatNumber(s.HeadFz), FormatNumber(s.ChestFx), FormatNumber(s.ChestFy), FormatNumber(s.ChestFz), FormatNumber(s.FixedTime), FormatNumber(s.GameTime), FormatNumber(s.FootX), FormatNumber(s.FootY), FormatNumber(s.FootZ), FormatNumber(s.CommandHeight), FormatNumber(s.CommandAngle), FormatNumber(s.CommandYaw), FormatNumber(s.RouteError), s.VelocityValid ? FormatNumber(s.FootVx) : "", s.VelocityValid ? FormatNumber(s.FootVy) : "", s.VelocityValid ? FormatNumber(s.FootVz) : "" }));
        }
      }
    }

    static void FinishAttempt(string outcome)
    {
      attemptActive = false;
      inputRun = null;
      NativeInputs.Stop();
      SaveFrames();
      LagRecorder.Save();
      File.WriteAllText(Path.Combine(caseDirectory, "geometry-end.txt"), NativeWeapons.Geometry() + "\n" + LagRecorder.Geometry());
      File.WriteAllText(Path.Combine(caseDirectory, "mod-state-end.txt"), ModControl.Describe());
      File.WriteAllText(Path.Combine(caseDirectory, "attempt-" + attempt.ToString("D2") + "-status.txt"), "outcome=" + outcome + "\ncontact_callbacks=" + totalContacts + "\npose_polls=" + posePolls + "\n" + Status());
      if (outcome == "diagnostic_recorded")
      {
        AddResult("diagnostic_only", outcome, null);
        Next();
      } else if (outcome == "valid_stable_flight")
      {
        validCount++;
        AddResult("valid", outcome, lastWindow);
        Next();
      } else if (attempt < 2)
      {
        phase = "retry_wait";
        phaseBegan = clock.Elapsed.TotalSeconds;
        reason = outcome;
        Checkpoint();
      } else
      {
        failedCount++;
        AddResult("no_valid_measurement", outcome, null);
        Next();
      }
    }

    static void AddResult(string status, string why, Window w)
    {
      var c = cases[index];
      var fields = new List<string>
      {
        c.Id,
        condition,
        c.Gun,
        c.Hz.ToString(),
        FormatNumber(c.Angle),
        FormatNumber(c.Height),
        "1.76",
        status,
        attempt.ToString(),
        why
      };
      if (w != null)
      {
        fields.AddRange(new[] { FormatNumber(w.Seconds), FormatNumber(w.Fps), FormatNumber(w.PhysicsHz), FormatNumber(w.Horizontal), FormatNumber(w.Vertical), FormatNumber(w.Vx), FormatNumber(w.Vz), FormatNumber(w.RbHorizontal), FormatNumber(w.RbVertical), FormatNumber(w.RbX), FormatNumber(w.RbZ), FormatNumber(w.HorizontalSpread), FormatNumber(w.VerticalSpread), FormatNumber(w.LateralSpread), FormatNumber(w.HorizontalDrift), FormatNumber(w.VerticalDrift), Math.Abs(w.Lateral) > .15 ? "1" : "0" });
      } else
      {
        fields.AddRange(Enumerable.Repeat("", 17));
      }

      fields.Add(attempt > 0 ? attempt.ToString() : "");
      fields.Add(attempt > 0 ? Path.GetFileName(caseDirectory) + "/" + Path.GetFileName(FramePath()) : "");
      results.Add(string.Join(",", fields.Select(QuoteCsv)));
      Checkpoint();
    }

    static void SetupFailure(string error)
    {
      setupFailedCount++;
      File.WriteAllText(Path.Combine(caseDirectory, "setup-error.txt"), error);
      AddResult("setup_failed", error.Replace('\r', ' ').Replace('\n', ' '), null);
      currentGun = null;
      if (setupFailedCount >= 3)
      {
        Abort("repeated_native_weapon_setup_failure");
        return;
      }

      Next();
    }

    static void Next()
    {
      pad.SetActive(false);
      index++;
      phase = "next_case";
      phaseBegan = clock.Elapsed.TotalSeconds;
      Checkpoint();
    }

    static void Checkpoint()
    {
      if (directory == null)
      {
        return;
      }

      File.WriteAllLines(Path.Combine(directory, "results-raw.csv"), results);
      File.WriteAllText(Path.Combine(directory, "progress.json"), "{\"active\":" + (active ? "true" : "false") + ",\"phase\":" + QuoteJson(phase) + ",\"completed\":" + index + ",\"total\":" + cases.Count + ",\"valid\":" + validCount + ",\"failed\":" + failedCount + ",\"setup_failed\":" + setupFailedCount + ",\"case\":" + QuoteJson(index < cases.Count ? cases[index].Id : "") + ",\"gun\":" + QuoteJson(index < cases.Count ? cases[index].Gun : "") + ",\"hz\":" + currentHz + ",\"reason\":" + QuoteJson(reason) + ",\"directory\":" + QuoteJson(directory) + "}");
    }

    static void Restore()
    {
      if (restored)
      {
        return;
      }

      try
      {
        ModControl.Restore();
      } catch (Exception error)
      {
        MelonLogger.Error("Failed to restore benchmark preferences: " + error);
      }

      restored = true;
      attemptActive = false;
      NativeInputs.Stop();
      try
      {
        NativeWeapons.RestoreOriginal();
      } catch (Exception e)
      {
        File.WriteAllText(Path.Combine(directory, "restore-weapon-error.txt"), e.ToString());
      }

      var d = Data_Manager.Instance.data_player;
      d.player_Height = savedHeight;
      d.offset_Floor = savedFloor;
      d.standing = savedStanding;
      Data_Manager.Instance.control_player.SendHeight();
      Data_Manager.Instance.control_player.STANDING();
      foreach (var c in disabledColliders)
      {
        if (c)
        {
          c.enabled = true;
        }
      }

      foreach (var r in disabledRenderers)
      {
        if (r)
        {
          r.enabled = true;
        }
      }

      if (pad)
      {
        pad.SetActive(false);
      }

      Resources.Load<SteamVR_Settings>("SteamVR_Settings").lockPhysicsUpdateRateToRenderFrequency = savedLock;
      Time.timeScale = savedTimeScale;
      Time.captureFramerate = savedCaptureRate;
      Time.fixedDeltaTime = savedStep;
      Application.targetFrameRate = savedFps;
      QualitySettings.vSyncCount = savedVsync;
      UnityEngine.XR.XRSettings.enabled = savedXr;
      rig.Teleport(savedFeet, true);
    }

    static void Complete()
    {
      active = false;
      phase = "complete";
      reason = "all_requested_cases_attempted";
      Restore();
      Checkpoint();
    }

    static void Abort(string error)
    {
      active = false;
      phase = "aborted";
      reason = error;
      try
      {
        if (attemptActive)
        {
          SaveFrames();
        }
      } catch
      {
      }

      try
      {
        Restore();
      } finally
      {
        Checkpoint();
      }
    }

    public static string Stop()
    {
      if (active)
      {
        Abort("stopped_by_operator");
      }

      return Status();
    }

    public static string Status()
    {
      var c = index < cases.Count ? cases[index] : null;
      return "active=" + active + " phase=" + phase + " completed=" + index + "/" + cases.Count + " valid=" + validCount + " failed=" + failedCount + " setup_failed=" + setupFailedCount + " case=" + (c == null ? "" : c.Id + " " + c.Gun + " " + c.Hz + "Hz " + c.Angle + "deg") + " attempt=" + attempt + " attempt_elapsed=" + FormatNumber(clock.Elapsed.TotalSeconds - attemptBegan) + " reason=" + reason + " magazine_contacts=" + totalContacts + " headForward=" + (rig ? rig.ControllerRig.m_head.forward.x + "," + rig.ControllerRig.m_head.forward.y + "," + rig.ControllerRig.m_head.forward.z : "") + " impulse=" + impulse.x + "," + impulse.y + "," + impulse.z + " output=" + directory;
    }
  }
}

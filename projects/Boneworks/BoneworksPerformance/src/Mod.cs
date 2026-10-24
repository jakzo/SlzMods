using MelonLoader;

namespace Sst.BoneworksPerformance;

public partial class Mod : MelonMod {
  public static Mod Instance { get; private set; }
  private MelonPreferences_Entry<bool> _enableFixedUpdatePoseHistory;
  private MelonPreferences_Entry<int> _poseHistorySampleRate;
  private MelonPreferences_Entry<int> _poseHistoryInterpolationDelayTicks;
  private MelonPreferences_Entry<float> _poseHistoryCatchUpSpeed;
  private MelonPreferences_Entry<float> _poseHistoryCatchUpHistorySeconds;
  private MelonPreferences_Entry<bool> _protectRisingHeadFromPoseCatchUp;
  private MelonPreferences_Entry<float> _jumpRiseSpeedThreshold;
  private MelonPreferences_Entry<float> _jumpRiseDetectionWindowSeconds;
  private MelonPreferences_Entry<bool> _showPerformanceHud;
  private MelonPreferences_Entry<bool> _usePhysicsRateMenu;
  private MelonPreferences_Entry<int> _customPhysicsTickRate;
  private MelonPreferences_Entry<bool> _keepSlottedWeaponsWithPhysics;
  private FixedUpdatePoseHistory _fixedUpdatePoseHistory;
  private PerformanceHud _performanceHud;
  private PhysicsRateMenu _physicsRateMenu;
  private readonly PendingPreferenceChanges _pendingSettings =
      new PendingPreferenceChanges();
  internal SlottedWeaponFixedCatchUp SlottedWeaponCatchUp { get; private set; }

  public override void OnApplicationStart() {
    Instance = this;
#if DEBUG
    DebugBeforeInitialize();
#endif
    var preferences = MelonPreferences.CreateCategory(BuildInfo.NAME);
    _enableFixedUpdatePoseHistory = preferences.CreateEntry(
        "SmoothTrackingDuringFrameDrops", true,
        "Smooth tracking when frames drop",
        "Continues tracking HMD position and jump/slomo button inputs at a high frequency during lag and applies the inputs to the multiple intermediate physics frames that occur before a rendered frame."
    );
    _protectRisingHeadFromPoseCatchUp = preferences.CreateEntry(
        "ProtectSuperJumpsDuringFrameDrops", true,
        "Protect super jumps during frame drops",
        "Keeps upward head movement at its original speed and IRL movement during a possible jump, even if the inputs need to catch up after frame drops. Catch up is paused while the headset is rising and resumes after it stops."
    );
    _keepSlottedWeaponsWithPhysics = preferences.CreateEntry(
        "ProtectGunFlyDuringFrameDrops", true,
        "Updates inventory weapons at physics rate instead of visual render rate",
        "The vanilla behavior of the game updates the position of glitched guns in inventory on visual render so during frame drops the physics update more frequently than the glitched gun position that is pushing the player during gun fly. This setting updates the inventory between physics ticks like what would happen at full framerate."
    );
    _usePhysicsRateMenu = preferences.CreateEntry(
        "UsePhysicsRateMenu", true,
        "Use the physics rate selected in the Boneworks menu",
        "Stops SteamVR from replacing the in-game physics-rate selection with the headset display frequency. CustomPhysicsTickRate must be set to 0 to use this option. Disable this to restore original Boneworks behavior."
    );
    _customPhysicsTickRate = preferences.CreateEntry(
        "CustomPhysicsTickRate", 0,
        "Custom physics tick rate (0 = obey UsePhysicsRateMenu setting)",
        "A positive number forces that physics tick rate. 0 disables the custom override, so UsePhysicsRateMenu decides whether to use the rate selected in the Boneworks menu or normal headset-rate behavior."
    );

    var advanced = MelonPreferences.CreateCategory(
        BuildInfo.NAME + "Advanced", BuildInfo.NAME + " - Advanced"
    );
    _poseHistorySampleRate = advanced.CreateEntry(
        "TrackingSamplesPerSecond", 250, "Tracking samples per second",
        "How often the SmoothTrackingDuringFrameDrops setting checks inputs in the background. Higher values use slightly more CPU. The allowed range is 1 to 1000 Hz. You should ensure this is higher than your physics and refresh rates."
    );
    _poseHistoryInterpolationDelayTicks = advanced.CreateEntry(
        "TrackingSmoothingDelay", 1, "Tracking smoothing delay",
        "Gives the mod enough tracking history to keep movement smooth during uneven frames. Each step adds one physics tick of delay to inputs but if the tracking sample rate is lower than the physics rate margin is needed to get new inputs. Only change this if this is a problem (really low end PCs)."
    );
    _poseHistoryCatchUpSpeed = advanced.CreateEntry(
        "TrackingCatchUpSpeed", 2f, "Tracking catch-up speed",
        "While lagging the game can compute physics frames unevenly which makes a smooth IRL movement appear uneven to the game and sometimes runs slower than realtime. The mod compensates for this but occasionally needs to catch up. A value of 2 advances the tracking clock at twice normal speed until it reaches the current timeline. Allowed range is 1 to 8."
    );
    _poseHistoryCatchUpHistorySeconds = advanced.CreateEntry(
        "TrackingCatchUpHistorySeconds", 1f,
        "Input history available for catch-up",
        "Maximum age in seconds of timestamped input that can be replayed after lag spikes."
    );
    _jumpRiseSpeedThreshold = advanced.CreateEntry(
        "JumpDetectionSpeed", 0.5f, "Jump detection speed",
        "Controls how fast your headset must move upward before the mod protects the jump. Measured in metres per second over JumpDetectionTime."
    );
    _jumpRiseDetectionWindowSeconds = advanced.CreateEntry(
        "JumpDetectionTime", 0.1f, "Jump detection time",
        "Controls how quickly jump protection responds to upward movement. The mod averages the headset's vertical speed over this many seconds of OpenVR pose history."
    );
    _showPerformanceHud = advanced.CreateEntry(
        "ShowPerformanceStats", false, "Show performance stats",
        "Shows useful game and headset rates above your left hand. The display includes rendered FPS, physics updates per second, the OpenVR client pose-request rate and the headset display frequency."
    );
    _performanceHud = new PerformanceHud(_showPerformanceHud.Value);
    _physicsRateMenu = new PhysicsRateMenu(
        _usePhysicsRateMenu.Value, _customPhysicsTickRate.Value
    );
    _physicsRateMenu.Initialize();
    _fixedUpdatePoseHistory = CreatePoseHistory();
#if DEBUG
    DebugInitialize(preferences);
#endif
    SlottedWeaponCatchUp = CreateSlotCatchUp();
    Watch(SettingsChange.Physics, _usePhysicsRateMenu, _customPhysicsTickRate);
    Watch(
        SettingsChange.Tracking, _enableFixedUpdatePoseHistory,
        _poseHistorySampleRate, _poseHistoryInterpolationDelayTicks,
        _poseHistoryCatchUpSpeed, _poseHistoryCatchUpHistorySeconds,
        _protectRisingHeadFromPoseCatchUp, _jumpRiseSpeedThreshold,
        _jumpRiseDetectionWindowSeconds
    );
    Watch(SettingsChange.Hud, _showPerformanceHud);
    Watch(SettingsChange.Slots, _keepSlottedWeaponsWithPhysics);
  }

  private FixedUpdatePoseHistory
  CreatePoseHistory() => new FixedUpdatePoseHistory(
      _enableFixedUpdatePoseHistory.Value, _poseHistorySampleRate.Value,
      _poseHistoryInterpolationDelayTicks.Value, _poseHistoryCatchUpSpeed.Value,
      _poseHistoryCatchUpHistorySeconds.Value,
      _protectRisingHeadFromPoseCatchUp.Value, _jumpRiseSpeedThreshold.Value,
      _jumpRiseDetectionWindowSeconds.Value
  );

  private void
  Watch(SettingsChange change, params MelonPreferences_Entry[] entries) {
    foreach (var entry in entries) {
      entry.OnValueChangedUntyped += () => _pendingSettings.Mark(change);
    }
  }

  private void ApplyRuntimePreferenceChanges() {
    var changes = _pendingSettings.Consume();
    if ((changes & SettingsChange.Physics) != 0) {
      _physicsRateMenu.Shutdown();
      _physicsRateMenu = new PhysicsRateMenu(
          _usePhysicsRateMenu.Value, _customPhysicsTickRate.Value
      );
      _physicsRateMenu.Initialize();
    }
    if ((changes & SettingsChange.Tracking) != 0) {
      _fixedUpdatePoseHistory.Shutdown();
      _fixedUpdatePoseHistory = CreatePoseHistory();
#if DEBUG
      DebugConfigurePoseHistory();
#endif
    }
    if ((changes & SettingsChange.Hud) != 0) {
      _performanceHud.Shutdown();
      _performanceHud = new PerformanceHud(_showPerformanceHud.Value);
    }
    if ((changes & SettingsChange.Slots) != 0) {
      SlottedWeaponCatchUp.Shutdown();
      SlottedWeaponCatchUp = CreateSlotCatchUp();
      SlottedWeaponCatchUp.ResetScene();
    }
  }

  private SlottedWeaponFixedCatchUp CreateSlotCatchUp() {
    var enabled = _keepSlottedWeaponsWithPhysics.Value;
#if DEBUG
    DebugConfigureSlotCatchUp(ref enabled);
#endif
    return new SlottedWeaponFixedCatchUp(enabled);
  }

  public override void OnSceneWasInitialized(int buildIndex, string sceneName) {
    ApplyRuntimePreferenceChanges();
    _fixedUpdatePoseHistory?.ResetScene();
    _performanceHud?.ResetScene();
    _physicsRateMenu?.ResetScene();
    SlottedWeaponCatchUp?.ResetScene();
#if DEBUG
    DebugSceneInitialized(buildIndex, sceneName);
#endif
  }

  public override void OnFixedUpdate() {
    _performanceHud?.OnFixedUpdate();
#if DEBUG
    DebugFixedUpdate();
#endif
  }

  public override void OnUpdate() {
    ApplyRuntimePreferenceChanges();
    _physicsRateMenu?.OnLateUpdate();
#if DEBUG
    DebugUpdate();
#else
    UpdateRuntime();
#endif
  }

  private void UpdateRuntime() {
    _fixedUpdatePoseHistory?.ObserveRenderedFrame();
#if DEBUG
    _superJumpDiagnostics?.ObserveRenderedFrame();
#endif
    _performanceHud?.OnUpdate(
        _fixedUpdatePoseHistory?.IsJumpDetected ?? false,
        _fixedUpdatePoseHistory?.InputTicksBehindCurrent ?? -1f
    );
    SlottedWeaponCatchUp?.OnUpdate();
  }

  public override void OnLateUpdate() {
    ApplyRuntimePreferenceChanges();
    _physicsRateMenu?.OnLateUpdate();
  }

  public override void OnApplicationQuit() {
#if DEBUG
    DebugBeforeQuit();
#endif
    _fixedUpdatePoseHistory?.Shutdown();
    _performanceHud?.Shutdown();
    _physicsRateMenu?.Shutdown();
    SlottedWeaponCatchUp?.Shutdown();
#if DEBUG
    DebugAfterQuit();
#endif
  }
}

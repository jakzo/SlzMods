using MelonLoader;

#if DEBUG
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using StressLevelZero.Rig;
using UnityEngine;
#endif

namespace Sst.BoneworksPerformance;

public class Mod : MelonMod {
  public static Mod Instance { get; private set; }
  private MelonPreferences_Entry<bool> _enableFixedUpdatePoseHistory;
  private MelonPreferences_Entry<int> _poseHistorySampleRate;
  private MelonPreferences_Entry<int> _poseHistoryInterpolationDelayTicks;
  private MelonPreferences_Entry<bool> _protectRisingHeadFromPoseCatchUp;
  private MelonPreferences_Entry<float> _jumpRiseSpeedThreshold;
  private MelonPreferences_Entry<float> _jumpRiseDetectionWindowSeconds;
  private MelonPreferences_Entry<bool> _showPerformanceHud;
  private MelonPreferences_Entry<bool> _usePhysicsRateMenu;
  private MelonPreferences_Entry<bool> _keepSlottedWeaponsWithPhysics;
  private FixedUpdatePoseHistory _fixedUpdatePoseHistory;
  private PerformanceHud _performanceHud;
  private PhysicsRateMenu _physicsRateMenu;
  internal SlottedWeaponFixedCatchUp SlottedWeaponCatchUp { get; private set; }

#if DEBUG
  private PerformanceCapture _capture;
  private MelonPreferences_Entry<string> _profilingMode;
  private MelonPreferences_Entry<float> _maximumCaptureSeconds;
  private MelonPreferences_Entry<bool> _enableCpuSampling;
  private MelonPreferences_Entry<bool> _enableCompletionWaits;
  private MelonPreferences_Entry<bool> _enableGraphics;
  private MelonPreferences_Entry<bool> _enableUnityBinaryProfiler;
  private MelonPreferences_Entry<bool> _enableMonoGcEvents;
  private MelonPreferences_Entry<bool> _enableTargetedDiagnostics;
  private MelonPreferences_Entry<bool> _enablePoseHistory;
  private MelonPreferences_Entry<bool> _showIndicator;
  private MelonPreferences_Entry<bool> _autoProfileMainMenu;
  private MelonPreferences_Entry<float> _autoProfileMainMenuSeconds;
  private MelonPreferences_Entry<string> _il2CppDumperScriptPath;
  private MelonPreferences_Entry<bool> _automatedRunoffBenchmark;
  private MelonPreferences_Entry<bool> _automatedRunoffLoadScene;
  private MelonPreferences_Entry<bool> _automatedDiagnosticSequence;
  private MelonPreferences_Entry<float> _diagnosticCaptureSeconds;
  private MelonPreferences_Entry<bool> _autoProfileAutomatedBenchmark;
  private MelonPreferences_Entry<bool> _performanceCoresOnly;
  private MelonPreferences_Entry<bool> _highProcessPriority;
  private MelonPreferences_Entry<bool> _reduceLodBias;
  private MelonPreferences_Entry<float> _optimizationCaptureSeconds;
  private MelonPreferences_Entry<float> _frameQueueCaptureSeconds;
  private MelonPreferences_Entry<bool> _enableSuperJumpDiagnostics;
  private SuperJumpDiagnostics _superJumpDiagnostics;
  private BaseController _rightController;
  private bool _wasThumbstickDown;
  private float _nextControllerSearchAt;
  private GameObject _vrIndicator;
  private Material _vrIndicatorMaterial;
  private float _autoStartAt = -1f;
  private float _autoStopAt = -1f;
  private GUIStyle _indicatorStyle;
  private GUIContent _indicatorContent;
  private readonly Rect _indicatorRect = new Rect(20f, 20f, 420f, 44f);
  private int _diagnosticIndex;
  private float _diagnosticNextAt = -1f;
  private float _diagnosticStopAt = -1f;
  private bool _diagnosticFinished;
  private bool _benchmarkAutoProfileStarted;
  private bool _forceAutomatedBenchmark;
  private bool _forceDiagnosticSequence;
  private bool _forceOptimizationSequence;
  private bool _forceFrameQueueSequence;
  private bool _forcePoseHistorySmokeTest;
  private bool _forcePoseSmoothnessTest;
  private bool _forceGunflyCatchUpSequence;
  private float _poseSmoothnessStartAt = -1f;
  private FixedPoseSmoothnessTest _poseSmoothnessTest;
  private int _optimizationIndex;
  private float _optimizationNextAt = -1f;
  private float _optimizationStopAt = -1f;
  private bool _optimizationFinished;
  private OptimizationExperiments _optimizations;

  internal GunflyCatchUpExperiment GunflyCatchUp { get; private set; }

  private const int OriginalFrameQueueDepth = -1;
  private static readonly int[] FrameQueueSequence = {
    OriginalFrameQueueDepth, 1, 2, 3, 3, 2, 1, OriginalFrameQueueDepth,
  };

  private static readonly OptimizationPreset[] OptimizationSequence = {
    OptimizationPreset.Baseline,
    OptimizationPreset.PerformanceCores,
    OptimizationPreset.PerformanceCoresHighPriority,
    OptimizationPreset.PerformanceCoresHighPriorityReducedLodBias,
    OptimizationPreset.PerformanceCoresHighPriorityReducedLodBias,
    OptimizationPreset.PerformanceCoresHighPriority,
    OptimizationPreset.PerformanceCores,
    OptimizationPreset.Baseline,
  };

  internal AutomatedRunoffBenchmark Benchmark { get; private set; }
#endif

  public override void OnApplicationStart() {
    Instance = this;
#if DEBUG
    var arguments = Environment.GetCommandLineArgs();
    _forcePoseSmoothnessTest = arguments.Any(argument => string.Equals(
        argument, "--bw-fixed-pose-smoothness-test",
        StringComparison.OrdinalIgnoreCase
    )) || Directory.Exists(Path.Combine(
        MelonUtils.UserDataDirectory, BuildInfo.NAME,
        "RunFixedPoseSmoothnessTest"
    ));
    if (_forcePoseSmoothnessTest)
      _poseSmoothnessTest = new FixedPoseSmoothnessTest();
#endif
    var preferences = MelonPreferences.CreateCategory(BuildInfo.NAME);
    _enableFixedUpdatePoseHistory = preferences.CreateEntry(
        "SmoothTrackingDuringFrameDrops", true,
        "Smooth tracking when frames drop",
        "Keeps head and hand movement smooth when Boneworks stutters, which " +
        "helps fast physical movement behave consistently. The mod samples " +
        "OpenVR poses on a background thread and interpolates a timestamped " +
        "pose for each physics tick. Buttons and analog inputs are unchanged."
    );
    _poseHistorySampleRate = preferences.CreateEntry(
        "TrackingSamplesPerSecond", 250,
        "Tracking samples per second",
        "Captures quick head and hand movements more accurately. This is how " +
        "often the background thread asks OpenVR for tracking poses. Higher " +
        "values use slightly more CPU. The allowed range is 90 to 1000 Hz."
    );
    _poseHistoryInterpolationDelayTicks = preferences.CreateEntry(
        "TrackingSmoothingDelay", 1,
        "Tracking smoothing delay",
        "Gives the mod enough tracking history to keep movement smooth during " +
        "uneven frames. Each step adds one physics tick of delay to head and " +
        "hand tracking. One tick normally gives interpolation samples on both " +
        "sides of the requested pose time."
    );
    _protectRisingHeadFromPoseCatchUp = preferences.CreateEntry(
        "ProtectSuperJumpsDuringFrameDrops", true,
        "Protect super jumps during frame drops",
        "Keeps upward head movement at its original speed during a possible " +
        "jump, even when Boneworks is catching up after a stutter. Positive " +
        "pose-clock correction pauses while the headset's average upward " +
        "speed exceeds the jump threshold, then resumes after the rise ends."
    );
    _jumpRiseSpeedThreshold = preferences.CreateEntry(
        "JumpDetectionSpeed", 0.5f,
        "Jump detection speed",
        "Controls how fast your headset must move upward before the mod " +
        "protects the jump. The value is measured in metres per second over " +
        "the configured detection time."
    );
    _jumpRiseDetectionWindowSeconds = preferences.CreateEntry(
        "JumpDetectionTime", 0.1f,
        "Jump detection time",
        "Controls how quickly jump protection responds to upward movement. " +
        "The mod averages the headset's vertical speed over this many seconds " +
        "of OpenVR pose history."
    );
    _showPerformanceHud = preferences.CreateEntry(
        "ShowPerformanceStats", false,
        "Show performance stats",
        "Shows useful game and headset rates above your left hand. The display " +
        "includes rendered FPS, physics updates per second, the OpenVR client " +
        "pose-request rate, and the headset display frequency."
    );
    _performanceHud = new PerformanceHud(_showPerformanceHud.Value);
    _usePhysicsRateMenu = preferences.CreateEntry(
        "UsePhysicsRateMenu", true,
        "Use the physics rate selected in the Boneworks menu",
        "Stops SteamVR from replacing the in-game physics-rate selection " +
        "with the headset display frequency. The selected menu rate controls " +
        "Time.fixedDeltaTime. Disable this to restore original Boneworks " +
        "behavior."
    );
    _physicsRateMenu = new PhysicsRateMenu(_usePhysicsRateMenu.Value);
    _physicsRateMenu.Initialize();
    _keepSlottedWeaponsWithPhysics = preferences.CreateEntry(
        "KeepSlottedWeaponsWithPhysics", true,
        "Keep slotted weapons with physics during frame drops",
        "Updates body-slot weapon positions between consecutive physics " +
        "ticks in one rendered frame. This prevents a gun on the back from " +
        "using a stale chest position during gun flying. It does nothing " +
        "when each rendered frame contains one physics tick."
    );
    _fixedUpdatePoseHistory = new FixedUpdatePoseHistory(
        _enableFixedUpdatePoseHistory.Value,
        _poseHistorySampleRate.Value,
        _poseHistoryInterpolationDelayTicks.Value,
        _protectRisingHeadFromPoseCatchUp.Value,
        _jumpRiseSpeedThreshold.Value,
        _jumpRiseDetectionWindowSeconds.Value
    );
#if DEBUG
    if (_poseSmoothnessTest != null)
      _fixedUpdatePoseHistory.SetSmoothnessTest(_poseSmoothnessTest);
    _profilingMode = preferences.CreateEntry(
        "ProfilingMode", "Basic", "Profiling mode",
        "MetricsOnly records frame and PlayerLoop data without CPU sampling. " +
        "Basic samples the main thread. Detailed also unwinds stacks, samples " +
        "workers, and can enable the native wait and GPU instruments."
    );
    _maximumCaptureSeconds = preferences.CreateEntry(
        "MaximumCaptureSeconds", 180f, "Maximum capture duration",
        "A capture stops when this duration or its fixed-size buffer is reached."
    );
    _enableCpuSampling = preferences.CreateEntry(
        "EnableCpuSampling", true, "Enable statistical CPU sampling"
    );
    _enableCompletionWaits = preferences.CreateEntry(
        "EnableCompletionWaitInstrumentation", true,
        "Instrument Unity completion waits"
    );
    _enableGraphics = preferences.CreateEntry(
        "EnableGraphicsInstrumentation", true,
        "Instrument graphics pending work and GPU engines"
    );
    _enableUnityBinaryProfiler = preferences.CreateEntry(
        "EnableUnityBinaryProfiler", false,
        "Enable Unity's binary profiler",
        "Off by default because this BONEWORKS player has not produced the " +
        "requested binary log and may still pay profiler runtime costs."
    );
    _enableMonoGcEvents = preferences.CreateEntry(
        "EnableMonoGcEvents", true, "Record Mono GC lifecycle events"
    );
    _enableTargetedDiagnostics = preferences.CreateEntry(
        "EnableTargetedDiagnostics", true,
        "Measure repeated collider outputs and recurring exceptions",
        "Detailed mode only. Times PhysBody.UpdateColliders, compares its " +
        "resulting collider geometry with the previous call, and groups " +
        "exceptions by a stable stack-trace identity."
    );
    _enablePoseHistory = preferences.CreateEntry(
        "EnablePoseHistoryRecorder", true,
        "Record OpenVR head and hand pose history",
        "Basic and Detailed captures poll OpenVR on a dedicated thread and " +
        "also record the poses observed on each Unity fixed tick."
    );
    _showIndicator = preferences.CreateEntry(
        "ShowProfilingIndicator", true, "Show the profiling indicator"
    );
    _autoProfileMainMenu = preferences.CreateEntry(
        "AutoProfileMainMenu", false, "Automatically profile the main menu",
        "Debug-build smoke test. Starts two seconds after the main menu initializes."
    );
    _autoProfileMainMenuSeconds = preferences.CreateEntry(
        "AutoProfileMainMenuSeconds", 5f, "Main-menu capture duration"
    );
    _il2CppDumperScriptPath = preferences.CreateEntry(
        "Il2CppDumperScriptPath", DefaultIl2CppDumperScriptPath(),
        "IL2CppDumper script.json path",
        "Optional fallback symbols for generated and shared GameAssembly " +
        "methods missing from the runtime metadata wrappers."
    );
    _automatedRunoffBenchmark = preferences.CreateEntry(
        "AutomatedRunoffBenchmark", false,
        "Run the captured Runoff gun-fly benchmark"
    );
    _automatedRunoffLoadScene = preferences.CreateEntry(
        "AutomatedRunoffLoadScene", true,
        "Load Runoff automatically for the benchmark"
    );
    _automatedDiagnosticSequence = preferences.CreateEntry(
        "AutomatedDiagnosticSequence", false,
        "Run the four profiler overhead A/B captures"
    );
    _diagnosticCaptureSeconds = preferences.CreateEntry(
        "DiagnosticCaptureSeconds", 15f,
        "Duration of each A/B diagnostic capture"
    );
    _autoProfileAutomatedBenchmark = preferences.CreateEntry(
        "AutoProfileAutomatedBenchmark", true,
        "Start one configured profile when the benchmark is ready"
    );
    _performanceCoresOnly = preferences.CreateEntry(
        "PerformanceCoresOnly", false,
        "Run BONEWORKS on performance-core CPU sets",
        "Experimental. Restricts unassigned game and Unity worker threads to " +
        "the fastest Windows CPU efficiency class."
    );
    _highProcessPriority = preferences.CreateEntry(
        "HighProcessPriority", false,
        "Run BONEWORKS at high process priority"
    );
    _reduceLodBias = preferences.CreateEntry(
        "ReduceLodBias", false,
        "Reduce the LOD bias by 25 percent",
        "Experimental visual tradeoff. Detailed meshes switch to lower-detail " +
        "versions at shorter distances."
    );
    _optimizationCaptureSeconds = preferences.CreateEntry(
        "OptimizationCaptureSeconds", 8f,
        "Duration of each automated optimization A/B capture"
    );
    _frameQueueCaptureSeconds = preferences.CreateEntry(
        "FrameQueueCaptureSeconds", 8f,
        "Duration of each automated frame-queue A/B capture"
    );
    _enableSuperJumpDiagnostics = preferences.CreateEntry(
        "EnableSuperJumpDiagnostics", true,
        "Record super-jump input timing",
        "Debug builds only. Records the hardware-timestamped jump-button " +
        "edge, the rendered pose, the pose used by each physics tick, and " +
        "the resulting player height. Each completed attempt gets an HTML " +
        "timeline and CSV data under UserData/BoneworksPerformance."
    );
    _superJumpDiagnostics = new SuperJumpDiagnostics(
        _enableSuperJumpDiagnostics.Value
    );
    _fixedUpdatePoseHistory.SetSuperJumpDiagnostics(_superJumpDiagnostics);

    _indicatorStyle = new GUIStyle {
      alignment = TextAnchor.MiddleCenter,
      fontSize = 18,
      fontStyle = FontStyle.Bold,
    };
    _indicatorStyle.normal.textColor = Color.white;
    _indicatorStyle.normal.background = Texture2D.whiteTexture;
    _indicatorContent = new GUIContent("PROFILING ACTIVE");

    _forceOptimizationSequence = arguments.Any(argument => string.Equals(
        argument, "--bw-performance-optimization-ab",
        StringComparison.OrdinalIgnoreCase
    )) || Directory.Exists(Path.Combine(
        MelonUtils.UserDataDirectory, BuildInfo.NAME,
        "RunOptimizationAB"
    ));
    _forceFrameQueueSequence = arguments.Any(argument => string.Equals(
        argument, "--bw-performance-frame-queue-ab",
        StringComparison.OrdinalIgnoreCase
    )) || Directory.Exists(Path.Combine(
        MelonUtils.UserDataDirectory, BuildInfo.NAME,
        "RunFrameQueueAB"
    ));
    _forceGunflyCatchUpSequence = arguments.Any(argument => string.Equals(
        argument, "--bw-gunfly-catchup-ab",
        StringComparison.OrdinalIgnoreCase
    )) || Directory.Exists(Path.Combine(
        MelonUtils.UserDataDirectory, BuildInfo.NAME,
        "RunGunflyCatchUpAB"
    ));
    _forcePoseHistorySmokeTest = arguments.Any(argument => string.Equals(
        argument, "--bw-pose-history-smoke-test",
        StringComparison.OrdinalIgnoreCase
    )) || Directory.Exists(Path.Combine(
        MelonUtils.UserDataDirectory, BuildInfo.NAME,
        "RunPoseHistorySmokeTest"
    ));
    _forceAutomatedBenchmark = _forceOptimizationSequence ||
        _forceFrameQueueSequence ||
        _forceGunflyCatchUpSequence ||
        arguments.Any(argument => string.Equals(
        argument, "--bw-performance-benchmark",
        StringComparison.OrdinalIgnoreCase
    )) || Directory.Exists(Path.Combine(
        MelonUtils.UserDataDirectory, BuildInfo.NAME,
        "RunAutomatedBenchmark"
    ));
    _forceDiagnosticSequence = arguments.Any(argument => string.Equals(
        argument, "--bw-performance-ab", StringComparison.OrdinalIgnoreCase
    )) || Directory.Exists(Path.Combine(
        MelonUtils.UserDataDirectory, BuildInfo.NAME,
        "RunAutomatedAB"
    ));

    NativeMethodCatalog.BeginBuild(_il2CppDumperScriptPath.Value);
    PlayerLoopPhaseProfiler.Install();
    GraphicsProfiler.Initialize();
    _capture = new PerformanceCapture(CpuSampler.GetCurrentNativeThreadId());
    Benchmark = new AutomatedRunoffBenchmark(
        _automatedRunoffBenchmark, _automatedRunoffLoadScene,
        _forceAutomatedBenchmark
    );
    GunflyCatchUp = new GunflyCatchUpExperiment(
        Benchmark, _forceGunflyCatchUpSequence
    );
    _optimizations = new OptimizationExperiments(
        _performanceCoresOnly, _highProcessPriority,
        _reduceLodBias
    );
    _optimizations.ApplyConfigured();
    MelonLogger.Msg(
        "Debug tools ready. Press the right thumbstick to start or stop " +
        "super-jump recording."
    );
#endif
    var suppressSlotCatchUp = false;
#if DEBUG
    suppressSlotCatchUp = _forceGunflyCatchUpSequence;
#endif
    SlottedWeaponCatchUp = new SlottedWeaponFixedCatchUp(
        _keepSlottedWeaponsWithPhysics.Value, suppressSlotCatchUp
    );
  }

#if DEBUG
  public override void OnSceneWasInitialized(int buildIndex, string sceneName) {
    _performanceHud?.ResetScene();
    _physicsRateMenu?.ResetScene();
    _rightController = null;
    _nextControllerSearchAt = 0f;
    TryAcquireRightController();
    _wasThumbstickDown = false;
    _fixedUpdatePoseHistory?.ResetScene();
    _superJumpDiagnostics?.ResetScene(sceneName);
    SlottedWeaponCatchUp?.ResetScene();
    Benchmark.OnSceneWasInitialized(buildIndex);
    GunflyCatchUp?.OnSceneWasInitialized(buildIndex);
    _optimizations.ApplySceneSettings();
    if (buildIndex == 1)
      ResetDiagnosticSequence();

    if (buildIndex == 1 &&
        (_autoProfileMainMenu.Value || _forcePoseHistorySmokeTest)) {
      _autoStartAt = Time.realtimeSinceStartup + 2f;
      _autoStopAt = -1f;
      MelonLogger.Msg("Main-menu profiler smoke test scheduled in 2 seconds.");
    }
    if (buildIndex == 1 && _forcePoseSmoothnessTest) {
      _poseSmoothnessStartAt = Time.realtimeSinceStartup + 2f;
      MelonLogger.Msg(
          "Fixed-pose smoothness test scheduled in the main menu in 2 seconds."
      );
    }
    if (_capture.IsActive || _superJumpDiagnostics.IsRecording)
      ShowVrIndicator();
  }

  public override void OnFixedUpdate() {
    _performanceHud?.OnFixedUpdate();
    var started = Stopwatch.GetTimestamp();
    var wasCapturing = _capture.IsActive;
    try {
      if (wasCapturing)
        _capture.NoteFixedUpdate();
    } finally {
      if (wasCapturing)
        ProfilerSelfMetrics.AddFixedUpdate(started);
    }
  }

  public override void OnUpdate() {
    var started = Stopwatch.GetTimestamp();
    var wasCapturing = _capture.IsActive;
    try {
      _fixedUpdatePoseHistory?.ObserveRenderedFrame();
      _superJumpDiagnostics?.ObserveRenderedFrame();
      _performanceHud?.OnUpdate();
      SlottedWeaponCatchUp?.OnUpdate();
      HandlePoseSmoothnessTest();
      _optimizations.MaintainFrameQueueDepth();
      HandleAutoCapture();
      HandleThumbstick();
      Benchmark.OnUpdate();
      GunflyCatchUp?.OnUpdate();
      HandleAutomatedBenchmarkProfiling();
    } finally {
      if (wasCapturing || _capture.IsActive)
        ProfilerSelfMetrics.AddUpdate(started);
    }
  }

  public override void OnGUI() {
    var profiling = _capture.IsActive;
    var jumpRecording = _superJumpDiagnostics != null &&
                        _superJumpDiagnostics.IsRecording;
    if (!profiling && !jumpRecording)
      return;
    var started = Stopwatch.GetTimestamp();
    var heapBefore = profiling ? MonoGcProfiler.UsedSize : 0;
    var eventType = Event.current == null
        ? EventType.Ignore
        : Event.current.type;
    try {
      if (profiling && !_capture.Options.ShowIndicator ||
          eventType != EventType.Repaint)
        return;
      var previousColor = GUI.color;
      GUI.color = new Color(0.75f, 0.05f, 0.05f, 0.92f);
      GUI.Box(_indicatorRect, _indicatorContent, _indicatorStyle);
      GUI.color = previousColor;
    } finally {
      if (profiling)
        ProfilerSelfMetrics.AddGui(
            started, heapBefore, MonoGcProfiler.UsedSize, eventType
        );
    }
  }

  public override void OnApplicationQuit() {
    if (_capture != null && _capture.IsActive)
      StopProfiling();
    _fixedUpdatePoseHistory?.Shutdown();
    _superJumpDiagnostics?.Shutdown();
    _performanceHud?.Shutdown();
    _physicsRateMenu?.Shutdown();
    SlottedWeaponCatchUp?.Shutdown();
    HideVrIndicator(true);
    Benchmark?.Shutdown();
    _optimizations?.Restore();
    GraphicsProfiler.Shutdown();
  }

  internal static void OnProfilerFrameBoundary() {
    var instance = Instance;
    if (instance == null || instance._capture == null ||
        !instance._capture.IsActive)
      return;
    if (!instance._capture.CaptureFrame()) {
      MelonLogger.Warning("Profiler buffer or duration limit reached.");
      StopProfiling();
    }
  }

  private void HandleThumbstick() {
    if (!_rightController && !TryAcquireRightController())
      return;
    var isDown = _rightController.GetThumbStick();
    if (isDown && !_wasThumbstickDown) {
      if (_superJumpDiagnostics != null &&
          _superJumpDiagnostics.IsEnabled) {
        if (_superJumpDiagnostics.IsRecording) {
          _superJumpDiagnostics.EndRecording();
          if (_capture.IsActive)
            _indicatorContent.text = "PROFILING ACTIVE";
          HideVrIndicator();
        } else {
          _superJumpDiagnostics.BeginRecording();
          _indicatorContent.text = "SUPER-JUMP RECORDING ACTIVE";
          ShowVrIndicator();
        }
      } else {
        ToggleProfiling();
      }
    }
    _wasThumbstickDown = isDown;
  }

  private void HandlePoseSmoothnessTest() {
    if (_poseSmoothnessTest == null)
      return;
    if (_poseSmoothnessStartAt >= 0f &&
        Time.realtimeSinceStartup >= _poseSmoothnessStartAt) {
      _poseSmoothnessStartAt = -1f;
      _fixedUpdatePoseHistory.ResetScene();
      _poseSmoothnessTest.Start();
    }
    if (!_poseSmoothnessTest.IsActive)
      return;
    _poseSmoothnessTest.Update();
    if (!_poseSmoothnessTest.IsComplete)
      return;
    _poseSmoothnessTest.CompleteAndWrite();
    Application.Quit();
  }

  private bool TryAcquireRightController() {
    var now = Time.realtimeSinceStartup;
    if (now < _nextControllerSearchAt)
      return false;
    _nextControllerSearchAt = now + 0.5f;

    var rig = UnityEngine.Object.FindObjectOfType<RigManager>();
    if (rig && rig.ControllerRig)
      _rightController = rig.ControllerRig.rightController;
    if (!_rightController) {
      _rightController = UnityEngine.Object.FindObjectsOfType<BaseController>()
                             .FirstOrDefault(
                                 controller =>
                                     controller.name == "Controller (right)"
                             );
    }
    if (!_rightController)
      return false;

    _wasThumbstickDown = _rightController.GetThumbStick();
    MelonLogger.Msg(
        "Right-stick capture toggle bound to " + _rightController.name + "."
    );
    return true;
  }

  private void ShowVrIndicator() {
    var profiling = _capture != null && _capture.IsActive;
    var jumpRecording = _superJumpDiagnostics != null &&
                        _superJumpDiagnostics.IsRecording;
    if ((!profiling && !jumpRecording) ||
        (profiling && !_capture.Options.ShowIndicator && !jumpRecording) ||
        _vrIndicator)
      return;
    var rig = UnityEngine.Object.FindObjectOfType<RigManager>();
    var hmd = rig && rig.ControllerRig ? rig.ControllerRig.hmdTransform : null;
    if (!hmd)
      return;

    _vrIndicator = GameObject.CreatePrimitive(PrimitiveType.Cube);
    _vrIndicator.name = "BoneworksPerformance capture indicator";
    var collider = _vrIndicator.GetComponent<Collider>();
    if (collider)
      UnityEngine.Object.Destroy(collider);
    _vrIndicator.transform.SetParent(hmd, false);
    _vrIndicator.transform.localPosition = new Vector3(0.12f, 0.08f, 0.35f);
    _vrIndicator.transform.localRotation = Quaternion.identity;
    _vrIndicator.transform.localScale = new Vector3(0.025f, 0.025f, 0.006f);
    var renderer = _vrIndicator.GetComponent<Renderer>();
    if (renderer) {
      var shader = Shader.Find("Unlit/Color");
      if (shader) {
        _vrIndicatorMaterial = new Material(shader);
        _vrIndicatorMaterial.color = Color.red;
        renderer.material = _vrIndicatorMaterial;
      } else {
        renderer.material.color = Color.red;
      }
      renderer.shadowCastingMode =
          UnityEngine.Rendering.ShadowCastingMode.Off;
      renderer.receiveShadows = false;
    }
  }

  private void HideVrIndicator(bool force = false) {
    if (!force && ((_capture != null && _capture.IsActive) ||
                   (_superJumpDiagnostics != null &&
                    _superJumpDiagnostics.IsRecording)))
      return;
    if (_vrIndicator)
      UnityEngine.Object.Destroy(_vrIndicator);
    if (_vrIndicatorMaterial)
      UnityEngine.Object.Destroy(_vrIndicatorMaterial);
    _vrIndicator = null;
    _vrIndicatorMaterial = null;
  }

  private void HandleAutoCapture() {
    var now = Time.realtimeSinceStartup;
    if (_autoStartAt >= 0f && now >= _autoStartAt) {
      _autoStartAt = -1f;
      if (_forcePoseHistorySmokeTest) {
        var options = CreateConfiguredOptions();
        options.Preset = "PoseHistorySmokeTest";
        options.EnableCpuSampling = false;
        options.EnableCompletionWaits = false;
        options.EnableGraphics = false;
        options.EnableUnityBinaryProfiler = false;
        options.EnableMonoGcEvents = false;
        options.EnableTargetedDiagnostics = false;
        options.EnablePoseHistory = true;
        options.PoseHistorySampleRate = 250;
        options.PoseHistoryInterpolationDelayTicks = 1;
        options.ShowIndicator = false;
        StartProfiling(CaptureMode.Basic, options, 7f);
        _autoStopAt = now + 5f;
      } else {
        StartProfiling();
        _autoStopAt = now + Math.Max(1f, _autoProfileMainMenuSeconds.Value);
      }
    }
    if (_autoStopAt >= 0f && now >= _autoStopAt) {
      _autoStopAt = -1f;
      StopProfiling();
    }
  }

  private void HandleAutomatedBenchmarkProfiling() {
    if (!Benchmark.IsReady)
      return;
    if (_forceGunflyCatchUpSequence)
      return;
    var now = Time.realtimeSinceStartup;
    if (_forceOptimizationSequence) {
      HandleOptimizationSequence(now);
      return;
    }
    if (_forceFrameQueueSequence) {
      HandleFrameQueueSequence(now);
      return;
    }
    if (_automatedDiagnosticSequence.Value || _forceDiagnosticSequence) {
      if (_diagnosticFinished)
        return;
      if (_diagnosticNextAt < 0f)
        _diagnosticNextAt = now + 2f;
      if (_capture.IsActive && now >= _diagnosticStopAt) {
        StopProfiling();
        _diagnosticNextAt = now + 2f;
        return;
      }
      if (!_capture.IsActive && now >= _diagnosticNextAt) {
        if (_diagnosticIndex >= 4) {
          _diagnosticFinished = true;
          MelonLogger.Msg("Automated profiler A/B sequence complete.");
          return;
        }
        StartDiagnosticCapture(_diagnosticIndex++);
        _diagnosticStopAt = now + Math.Max(2f, _diagnosticCaptureSeconds.Value);
        _diagnosticNextAt = -1f;
      }
      return;
    }

    if (_autoProfileAutomatedBenchmark.Value &&
        !_benchmarkAutoProfileStarted) {
      _benchmarkAutoProfileStarted = true;
      StartProfiling();
      _autoStopAt = now + Math.Max(2f, _maximumCaptureSeconds.Value);
    }
  }

  private void HandleOptimizationSequence(float now) {
    if (_optimizationFinished)
      return;
    if (_capture.IsActive) {
      if (now >= _optimizationStopAt) {
        StopProfiling();
        _optimizationNextAt = -1f;
      }
      return;
    }
    if (_optimizationNextAt < 0f) {
      if (_optimizationIndex >= OptimizationSequence.Length) {
        _optimizationFinished = true;
        _optimizations.ApplyConfigured();
        MelonLogger.Msg("Automated optimization A/B sequence complete.");
        return;
      }
      var preset = OptimizationSequence[_optimizationIndex];
      _optimizations.ApplyPreset(preset);
      _optimizationNextAt = now + 3f;
      MelonLogger.Msg(
          "Warming optimization preset " + preset + " for 3 seconds."
      );
      return;
    }
    if (now < _optimizationNextAt)
      return;
    var activePreset = OptimizationSequence[_optimizationIndex];
    var options = CreateConfiguredOptions();
    options.Preset = "Optimization-" + (_optimizationIndex + 1) + "-" +
                     activePreset;
    options.EnableCpuSampling = false;
    options.EnableCompletionWaits = false;
    options.EnableGraphics = false;
    options.EnableUnityBinaryProfiler = false;
    options.EnableMonoGcEvents = false;
    options.EnableTargetedDiagnostics = false;
    options.ShowIndicator = false;
    var seconds = Math.Max(3f, _optimizationCaptureSeconds.Value);
    StartProfiling(CaptureMode.MetricsOnly, options, seconds + 2f);
    _optimizationStopAt = now + seconds;
    _optimizationNextAt = -1f;
    _optimizationIndex++;
  }

  private void HandleFrameQueueSequence(float now) {
    if (_optimizationFinished)
      return;
    if (_capture.IsActive) {
      if (now >= _optimizationStopAt) {
        StopProfiling();
        _optimizationNextAt = -1f;
      }
      return;
    }
    if (_optimizationNextAt < 0f) {
      if (_optimizationIndex >= FrameQueueSequence.Length) {
        _optimizationFinished = true;
        _optimizations.ApplyConfigured();
        MelonLogger.Msg("Automated frame-queue A/B sequence complete.");
        return;
      }
      var requestedDepth = FrameQueueSequence[_optimizationIndex];
      var actualDepth = _optimizations.ApplyFrameQueueDepth(requestedDepth);
      _optimizationNextAt = now + 3f;
      MelonLogger.Msg(
          "Warming frame queue " + actualDepth + " for 3 seconds."
      );
      return;
    }
    if (now < _optimizationNextAt)
      return;
    var queueDepth = FrameQueueSequence[_optimizationIndex];
    if (queueDepth < 0)
      queueDepth = _optimizations.OriginalMaxQueuedFrames;
    var options = CreateConfiguredOptions();
    options.Preset = "FrameQueue-" + (_optimizationIndex + 1) + "-Depth-" +
                     queueDepth;
    options.EnableCpuSampling = false;
    options.EnableCompletionWaits = false;
    options.EnableGraphics = false;
    options.EnableUnityBinaryProfiler = false;
    options.EnableMonoGcEvents = false;
    options.EnableTargetedDiagnostics = false;
    options.ShowIndicator = false;
    var seconds = Math.Max(3f, _frameQueueCaptureSeconds.Value);
    StartProfiling(CaptureMode.MetricsOnly, options, seconds + 2f);
    _optimizationStopAt = now + seconds;
    _optimizationNextAt = -1f;
    _optimizationIndex++;
  }

  private void StartDiagnosticCapture(int index) {
    var options = CreateConfiguredOptions();
    CaptureMode mode;
    switch (index) {
      case 0:
        mode = CaptureMode.MetricsOnly;
        options.Preset = "AB1-MetricsOnly-NoIndicator";
        options.EnableCpuSampling = false;
        options.EnableCompletionWaits = false;
        options.EnableGraphics = false;
        options.EnableUnityBinaryProfiler = false;
        options.ShowIndicator = false;
        break;
      case 1:
        mode = CaptureMode.MetricsOnly;
        options.Preset = "AB2-MetricsOnly-Indicator";
        options.EnableCpuSampling = false;
        options.EnableCompletionWaits = false;
        options.EnableGraphics = false;
        options.EnableUnityBinaryProfiler = false;
        options.ShowIndicator = true;
        break;
      case 2:
        mode = CaptureMode.Detailed;
        options.Preset = "AB3-Detailed-NoUnityProfiler";
        options.EnableCpuSampling = true;
        options.EnableUnityBinaryProfiler = false;
        options.ShowIndicator = false;
        break;
      default:
        mode = CaptureMode.Detailed;
        options.Preset = "AB4-Detailed-WithUnityProfiler";
        options.EnableCpuSampling = true;
        options.EnableUnityBinaryProfiler = true;
        options.ShowIndicator = false;
        break;
    }
    StartProfiling(mode, options, _diagnosticCaptureSeconds.Value + 2f);
  }

  private void ResetDiagnosticSequence() {
    _diagnosticIndex = 0;
    _diagnosticNextAt = -1f;
    _diagnosticStopAt = -1f;
    _diagnosticFinished = false;
    _benchmarkAutoProfileStarted = false;
    _optimizationIndex = 0;
    _optimizationNextAt = -1f;
    _optimizationStopAt = -1f;
    _optimizationFinished = false;
  }

  private CaptureMode ReadConfiguredMode() {
    if (string.Equals(
            _profilingMode.Value, "Detailed",
            StringComparison.OrdinalIgnoreCase
        ))
      return CaptureMode.Detailed;
    if (string.Equals(
            _profilingMode.Value, "MetricsOnly",
            StringComparison.OrdinalIgnoreCase
        ))
      return CaptureMode.MetricsOnly;
    return CaptureMode.Basic;
  }

  private CaptureOptions CreateConfiguredOptions() => new CaptureOptions {
    Preset = "Configured",
    EnableCpuSampling = _enableCpuSampling.Value,
    EnableCompletionWaits = _enableCompletionWaits.Value,
    EnableGraphics = _enableGraphics.Value,
    EnableUnityBinaryProfiler = _enableUnityBinaryProfiler.Value,
    EnableMonoGcEvents = _enableMonoGcEvents.Value,
    EnableTargetedDiagnostics = _enableTargetedDiagnostics.Value,
    EnablePoseHistory = _enablePoseHistory.Value &&
                        !_enableFixedUpdatePoseHistory.Value,
    PoseHistorySampleRate = _poseHistorySampleRate.Value,
    PoseHistoryInterpolationDelayTicks =
        _poseHistoryInterpolationDelayTicks.Value,
    ShowIndicator = _showIndicator.Value,
  };

  private void StartProfiling(
      CaptureMode mode, CaptureOptions options, float duration
  ) {
    if (_capture == null || _capture.IsActive)
      return;
    if (mode == CaptureMode.MetricsOnly)
      options.EnableCpuSampling = false;
    _indicatorContent.text = "PROFILING ACTIVE  •  " +
                             options.Preset + "  •  " + mode;
    _capture.Start(mode, duration, options);
    _fixedUpdatePoseHistory?.BeginDiagnosticRun();
    ShowVrIndicator();
  }

  private static string DefaultIl2CppDumperScriptPath() {
    var profile = Environment.GetFolderPath(
        Environment.SpecialFolder.UserProfile
    );
    return Path.Combine(
        profile, "Downloads", "boneworks_il2cppdumper", "script.json"
    );
  }
#endif

#if !DEBUG
  public override void OnSceneWasInitialized(int buildIndex, string sceneName) {
    _fixedUpdatePoseHistory?.ResetScene();
    _performanceHud?.ResetScene();
    _physicsRateMenu?.ResetScene();
    SlottedWeaponCatchUp?.ResetScene();
  }

  public override void OnFixedUpdate() {
    _performanceHud?.OnFixedUpdate();
  }

  public override void OnUpdate() {
    _fixedUpdatePoseHistory?.ObserveRenderedFrame();
    _performanceHud?.OnUpdate();
    SlottedWeaponCatchUp?.OnUpdate();
  }

  public override void OnApplicationQuit() {
    _fixedUpdatePoseHistory?.Shutdown();
    _performanceHud?.Shutdown();
    _physicsRateMenu?.Shutdown();
    SlottedWeaponCatchUp?.Shutdown();
  }
#endif

  public override void OnLateUpdate() {
    _physicsRateMenu?.OnLateUpdate();
  }

  public static void StartProfiling() {
#if DEBUG
    if (Instance == null || Instance._capture == null ||
        Instance._capture.IsActive)
      return;
    Instance.StartProfiling(
        Instance.ReadConfiguredMode(), Instance.CreateConfiguredOptions(),
        Instance._maximumCaptureSeconds.Value
    );
#endif
  }

  public static void StopProfiling() {
#if DEBUG
    if (Instance == null || Instance._capture == null ||
        !Instance._capture.IsActive)
      return;
    Instance._capture.Stop();
    Instance._fixedUpdatePoseHistory?.EndDiagnosticRun();
    if (Instance._superJumpDiagnostics != null &&
        Instance._superJumpDiagnostics.IsRecording)
      Instance._indicatorContent.text = "SUPER-JUMP RECORDING ACTIVE";
    Instance.HideVrIndicator();
#endif
  }

  public static void ToggleProfiling() {
#if DEBUG
    if (Instance != null && Instance._capture != null &&
        Instance._capture.IsActive)
      StopProfiling();
    else
      StartProfiling();
#endif
  }
}

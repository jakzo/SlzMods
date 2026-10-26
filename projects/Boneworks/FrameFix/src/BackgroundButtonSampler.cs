using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading;
using MelonLoader;
using StressLevelZero.Player;
using StressLevelZero.Rig;
using Valve.VR;

namespace Sst.BoneworksFrameFix;

// Reads physical buttons from an isolated legacy OpenVR client. The game's own
// GetControllerState is frozen when SteamVR actions are active.
// Masks match the game's shipped bindings. Remapped actions still need validation.
internal sealed class BackgroundButtonSampler : IDisposable
{
  private sealed class Bindings
  {
    public readonly ulong JumpMask, SlowMotionMask;
    public readonly bool JumpRight;
    public readonly int Generation;

    public Bindings(ulong jump, ulong slowMotion, bool jumpRight, int generation)
    {
      JumpMask = jump;
      SlowMotionMask = slowMotion;
      JumpRight = jumpRight;
      Generation = generation;
    }
  }

  private Bindings _bindings;
  private int _generation;
  private NativeButtonBuffer _buffer;
  private EventWaitHandle _stop;
  private Process _process;
  private bool _reportedExit;
  private NativeInputClock _clock;

  public int Generation => _generation;

  public void Start(int sampleRate)
  {
    try
    {
      _clock = new NativeInputClock();
      var name = "Local\\FrameFix.Input." + Guid.NewGuid().ToString("N");
      _buffer = new NativeButtonBuffer(name, true);
      _stop = new EventWaitHandle(false, EventResetMode.ManualReset, name + ".stop");
      var assembly = Assembly.GetExecutingAssembly();
      using var resource = assembly.GetManifestResourceStream("FrameFix.InputCapture.exe");
      using var bytes = new MemoryStream();
      resource.CopyTo(bytes);
      var executable = bytes.ToArray();
      using var hash = SHA256.Create();
      var version = BitConverter.ToString(hash.ComputeHash(executable)).Replace("-", "");
      var directory = Path.Combine(Directory.GetCurrentDirectory(), "UserData", "BoneworksFrameFix", "native", version);
      Directory.CreateDirectory(directory);
      var path = Path.Combine(directory, "FrameFix.InputCapture.exe");
      if (!File.Exists(path) || !System.Linq.Enumerable.SequenceEqual(hash.ComputeHash(File.ReadAllBytes(path)), hash.ComputeHash(executable)))
        File.WriteAllBytes(path, executable);
      using var parent = Process.GetCurrentProcess();
      var plugins = Path.Combine(Directory.GetCurrentDirectory(), "BONEWORKS_Data", "Plugins");
      _process = Process.Start(new ProcessStartInfo(path,
          parent.Id + " \"" + name + "\" \"" + plugins + "\" " + sampleRate)
      {
        UseShellExecute = false,
        CreateNoWindow = true,
        WindowStyle = ProcessWindowStyle.Hidden,
      });
    }
    catch (Exception exception)
    {
      Dispose();
      MelonLogger.Warning("Background button capture unavailable; using action history: " + exception.Message);
    }
  }

  public void Dispose()
  {
    _stop?.Set();
    if (_process != null)
    {
      if (!_process.WaitForExit(2000))
        _process.Kill();
      _process.Dispose();
      _process = null;
    }
    _stop?.Dispose();
    _stop = null;
    _buffer?.Dispose();
    _buffer = null;
  }

  public void Reset()
  {
    ++_generation; // Reject a sample still in flight from the previous scene.
    _bindings = null;
    _buffer?.SkipToLatest();
  }

  // All methods in this class are main-thread owned. The helper only sees raw
  // controller states, never Unity objects or game action handles.
  public bool Configure(ControllerRig rig)
  {
    var jump = JumpButtonTiming.Controller(rig);
    var slow = SlowMotionButtonTiming.Controller(rig);
    var jumpMask = JumpButtonTiming.UsesAButton(jump) ? Mask(jump.controllerType) : 0;
    var slowMask = SlowMotionButtonTiming.IsSupported(slow) ? Mask(slow.controllerType) : 0;
    var previous = _bindings;
    if (previous != null && previous.JumpMask == jumpMask &&
        previous.SlowMotionMask == slowMask && previous.JumpRight == rig.isRightHanded)
      return false;
    _bindings = new Bindings(jumpMask, slowMask, rig.isRightHanded, ++_generation);
    _buffer?.SkipToLatest();
    return true;
  }

  private static ulong Mask(ControllerInfo.Type type)
  {
    switch (type)
    {
      case ControllerInfo.Type.OCCULUS_TOUCH: return 1UL << (int)EVRButtonId.k_EButton_A;
      case ControllerInfo.Type.KNUCKLES: return 1UL << (int)EVRButtonId.k_EButton_IndexController_A;
      case ControllerInfo.Type.VIVE_WANDS: return 1UL << (int)EVRButtonId.k_EButton_ApplicationMenu;
      case ControllerInfo.Type.HOLOGRAPHIC: return 1UL << (int)EVRButtonId.k_EButton_SteamVR_Touchpad;
      default: return 0;
    }
  }

  public bool TryRead(bool inputAvailable, out SampledButtons buttons, out bool lost)
  {
    buttons = default;
    lost = false;
    if (_process != null && !_reportedExit && _process.HasExited)
    {
      _reportedExit = true;
      MelonLogger.Warning("Background button reader exited with code " + _process.ExitCode + "; using action history.");
    }
    var bindings = _bindings;
    if (_buffer == null || bindings == null || !_buffer.TryRead(out var timestamp,
        out var left, out var right, out var valid, out lost))
      return false;
    if (!inputAvailable)
      valid = 0;
    buttons = new SampledButtons
    {
      Timestamp = _clock.ToManaged(timestamp),
      Generation = bindings.Generation,
      Jump = State((valid & (bindings.JumpRight ? 2 : 1)) != 0,
          bindings.JumpRight ? right : left, bindings.JumpMask),
      SlowMotion = State((valid & (bindings.JumpRight ? 1 : 2)) != 0,
          bindings.JumpRight ? left : right, bindings.SlowMotionMask),
    };
    return true;
  }

  private static SampledButton State(bool valid, ulong pressed, ulong mask) =>
      !valid || mask == 0 ? SampledButton.Unavailable :
      (pressed & mask) != 0 ? SampledButton.Held : SampledButton.Released;
}

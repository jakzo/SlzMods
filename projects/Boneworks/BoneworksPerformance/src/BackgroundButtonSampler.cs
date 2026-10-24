using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using StressLevelZero.Player;
using StressLevelZero.Rig;
using Valve.VR;

namespace Sst.BoneworksPerformance;

// Reads legacy physical button states, without updating SteamVR's shared action cache.
// Masks match the game's shipped bindings. Remapped actions still need validation.
internal sealed class BackgroundButtonSampler
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

  private static readonly uint StateSize = (uint)Marshal.SizeOf(typeof(VRControllerState_t));
  private Bindings _bindings;
  private int _generation;
  private long _refreshDevicesAt;
  private uint _left = OpenVR.k_unTrackedDeviceIndexInvalid;
  private uint _right = OpenVR.k_unTrackedDeviceIndexInvalid;

  public int Generation => _generation;

  public void Reset()
  {
    ++_generation; // Reject a sample still in flight from the previous scene.
    Volatile.Write(ref _bindings, null);
  }

  // Only called on the main thread. No Unity/game objects are read by Sample().
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
    Volatile.Write(ref _bindings, new Bindings(jumpMask, slowMask, rig.isRightHanded, ++_generation));
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

  public SampledButtons Sample(CVRSystem system)
  {
    var bindings = Volatile.Read(ref _bindings);
    if (bindings == null)
      return default;
    var started = Stopwatch.GetTimestamp();
    if (started >= _refreshDevicesAt)
    {
      _left = system.GetTrackedDeviceIndexForControllerRole(ETrackedControllerRole.LeftHand);
      _right = system.GetTrackedDeviceIndexForControllerRole(ETrackedControllerRole.RightHand);
      _refreshDevicesAt = started + Stopwatch.Frequency;
    }
    var left = default(VRControllerState_t);
    var right = default(VRControllerState_t);
    var focused = system.IsInputAvailable();
    var leftValid = focused && _left != OpenVR.k_unTrackedDeviceIndexInvalid &&
        system.GetControllerState(_left, ref left, StateSize);
    var rightValid = focused && _right != OpenVR.k_unTrackedDeviceIndexInvalid &&
        system.GetControllerState(_right, ref right, StateSize);
    return new SampledButtons
    {
      Timestamp = started + (Stopwatch.GetTimestamp() - started) / 2,
      Generation = bindings.Generation,
      Jump = State(bindings.JumpRight ? rightValid : leftValid,
          bindings.JumpRight ? right.ulButtonPressed : left.ulButtonPressed, bindings.JumpMask),
      SlowMotion = State(bindings.JumpRight ? leftValid : rightValid,
          bindings.JumpRight ? left.ulButtonPressed : right.ulButtonPressed, bindings.SlowMotionMask),
    };
  }

  private static SampledButton State(bool valid, ulong pressed, ulong mask) =>
      !valid || mask == 0 ? SampledButton.Unavailable :
      (pressed & mask) != 0 ? SampledButton.Held : SampledButton.Released;
}

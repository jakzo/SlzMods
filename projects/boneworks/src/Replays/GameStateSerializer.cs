using MelonLoader;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using UnityEngine;
using Valve.VR;

namespace Sst.Replays {
class GameStateSerializer {
  private (Bwr.SettingType, float)[] _prevSettings;
  private byte _buttonsPressedLeft;
  private byte _buttonsPressedRight;
  private int _skippedFrames = -1;

  public void OnUpdate() {
    if (Mod.GameState.rigManager == null)
      return;

    _skippedFrames++;
    RegisterButtonPresses(Mod.GameState.rigManager.ControllerRig.leftController,
                          ref _buttonsPressedLeft);
    RegisterButtonPresses(
        Mod.GameState.rigManager.ControllerRig.rightController,
        ref _buttonsPressedRight);
  }

  private void
  RegisterButtonPresses(StressLevelZero.Rig.BaseController controller,
                        ref byte pressed) {
    if (controller.GetAButton())
      pressed |= (byte)Bwr.ButtonPress.A;
    if (controller.GetBButton())
      pressed |= (byte)Bwr.ButtonPress.B;
    if (controller.GetMenuButton())
      pressed |= (byte)Bwr.ButtonPress.MENU;
    if (controller.GetSecondaryMenuButton())
      pressed |= (byte)Bwr.ButtonPress.SECONDARY_MENU;
    if (controller.GetThumbStick())
      pressed |= (byte)Bwr.ButtonPress.THUMB_STICK;
    if (controller.GetPrimaryInteractionButton())
      pressed |= (byte)Bwr.ButtonPress.PRIMARY_INTERACTION;
    if (controller.GetSecondaryInteractionButton())
      pressed |= (byte)Bwr.ButtonPress.SECONDARY_INTERACTION;
    if (controller.GetGrabbedState())
      pressed |= (byte)Bwr.ButtonPress.GRABBED_STATE;
  }

  public byte[] BuildFrame(float secondsElapsed) {
    var rigManager = Mod.GameState.rigManager;
    if (rigManager == null)
      throw new System.Exception("No rig manager found");

    var builder = new FlatBuffers.FlatBufferBuilder(1024);

    var dataPlayer = Data_Manager.Instance.data_player;
    var settings = new(Bwr.SettingType, float)[] {
      (Bwr.SettingType.REFRESH_RATE,
       SteamVR.instance?.hmd_DisplayFrequency ?? 0),
      (Bwr.SettingType.BELT_RIGHT_SIDE, dataPlayer.beltRightSide ? 1 : 0),
      (Bwr.SettingType.IS_INVERTED, dataPlayer.isInverted ? 1 : 0),
      (Bwr.SettingType.IS_RIGHT_HANDED, dataPlayer.isRightHanded ? 1 : 0),
      (Bwr.SettingType.JOY_SENSITIVITY, dataPlayer.joySensitivityNew),
      (Bwr.SettingType.LOCO_CURVE, dataPlayer.loco_Curve),
      (Bwr.SettingType.LOCO_DEGREES_PER_SNAP, dataPlayer.loco_DegreesPerSnap),
      (Bwr.SettingType.LOCO_DIRECTION, dataPlayer.loco_Direction),
      (Bwr.SettingType.LOCO_SNAP_DEGREES_PER_FRAME,
       dataPlayer.loco_SnapDegreesPerFrame),
      (Bwr.SettingType.OFFSET_FLOOR, dataPlayer.offset_Floor),
      (Bwr.SettingType.OFFSET_SITTING, dataPlayer.offset_Sitting),
      (Bwr.SettingType.PHYSICS_UPDATE_RATE, dataPlayer.physicsUpdateRate),
      (Bwr.SettingType.PLAYER_HEIGHT, dataPlayer.player_Height),
      (Bwr.SettingType.SNAP_ENABLED, dataPlayer.SnapEnabled ? 1 : 0),
      (Bwr.SettingType.STANDING, dataPlayer.standing ? 1 : 0),
      (Bwr.SettingType.VIRTUAL_CROUCHING, dataPlayer.VirtualCrouching ? 1 : 0),
    };
    var changedSettings =
        _prevSettings == null
            ? settings
            : settings
                  .Where((setting, i) =>
                             setting.Item2 != _prevSettings[i].Item2)
                  .ToArray();
    FlatBuffers.VectorOffset? changedSettingsOffset = null;
    if (changedSettings.Length > 0) {
      Bwr.Frame.StartChangedSettingsVector(builder, changedSettings.Length);
      foreach (var (type, value) in changedSettings)
        Bwr.ChangedSetting.CreateChangedSetting(builder, type, value);
      changedSettingsOffset = builder.EndVector();
      _prevSettings = settings;
    }

    Bwr.Frame.StartFrame(builder);
    Bwr.Frame.AddTime(builder, secondsElapsed);
    Bwr.Frame.AddSkippedFrames(
        builder,
        (byte)Mathf.Clamp(_skippedFrames, byte.MinValue, byte.MaxValue));
    if (changedSettingsOffset.HasValue)
      Bwr.Frame.AddChangedSettings(builder, changedSettingsOffset.Value);

    var hmd = rigManager.ControllerRig.hmdTransform.parent;
    var hmdPosition = rigManager.ControllerRig._lastTrackedHead;
    var hmdEulerAngles = hmd.localRotation.eulerAngles;
    var controllerLeft = rigManager.ControllerRig.leftController;
    var controllerLeftThumbstickAxis = controllerLeft.GetThumbStickAxis();
    var controllerLeftPosition = controllerLeft.transform.localPosition;
    var controllerLeftEulerAngles = controllerLeft.transform.localEulerAngles;
    var controllerRight = rigManager.ControllerRig.rightController;
    var controllerRightThumbstickAxis = controllerRight.GetThumbStickAxis();
    var controllerRightPosition = controllerRight.transform.localPosition;
    var controllerRightEulerAngles = controllerRight.transform.localEulerAngles;
    Bwr.Frame.AddVrInput(
        builder,
        Bwr.VrInput.CreateVrInput(
            builder, hmdPosition.x, hmdPosition.y, hmdPosition.z,
            hmdEulerAngles.x, hmdEulerAngles.y, hmdEulerAngles.z,
            _buttonsPressedLeft, controllerLeftThumbstickAxis.x,
            controllerLeftThumbstickAxis.y, controllerLeftPosition.x,
            controllerLeftPosition.y, controllerLeftPosition.z,
            controllerLeftEulerAngles.x, controllerLeftEulerAngles.y,
            controllerLeftEulerAngles.z, _buttonsPressedRight,
            controllerRightThumbstickAxis.x, controllerRightThumbstickAxis.y,
            controllerRightPosition.x, controllerRightPosition.y,
            controllerRightPosition.z, controllerRightEulerAngles.x,
            controllerRightEulerAngles.y, controllerRightEulerAngles.z));

    var bodyPosition = rigManager.gameWorldSkeletonRig.transform.position;
    var handLeft = rigManager.physicsRig.leftHand.palmPositionTransform;
    var handLeftEulerAngles = handLeft.eulerAngles;
    var handRight = rigManager.physicsRig.rightHand.palmPositionTransform;
    var handRightEulerAngles = handRight.eulerAngles;
    Bwr.Frame.AddPlayerState(
        builder,
        Bwr.PlayerState.CreatePlayerState(
            builder, bodyPosition.x, bodyPosition.y, bodyPosition.z,
            rigManager.ControllerRig.vrRoot.localEulerAngles.y,
            rigManager.ControllerRig.feetOffset, hmd.position.x, hmd.position.y,
            hmd.position.z, handLeft.position.x, handLeft.position.y,
            handLeft.position.z, handLeftEulerAngles.x, handLeftEulerAngles.y,
            handLeftEulerAngles.z, handRight.position.x, handRight.position.y,
            handRight.position.z, handRightEulerAngles.x,
            handRightEulerAngles.y, handRightEulerAngles.z));

    var frame = Bwr.Frame.EndFrame(builder);
    builder.Finish(frame.Value);

    _buttonsPressedLeft = _buttonsPressedRight = 0;
    _skippedFrames = -1;

    // Utils.LogDebug($"Recorded frame: {currentSceneIdx}
    // {cam.position.ToString()}");
    return builder.SizedByteArray();
  }
}
}

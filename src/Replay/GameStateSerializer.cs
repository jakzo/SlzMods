using MelonLoader;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using UnityEngine;

namespace SpeedrunTools.Replay
{
  class GameStateSerializer
  {
    private StressLevelZero.Rig.RigManager _rigManager;
    private (Bwr.SettingType, float)[] _prevSettings;
    private byte _buttonsPressedLeft;
    private byte _buttonsPressedRight;

    public void OnSceneChange()
    {
      _rigManager = null;
    }

    public void OnUpdate()
    {
      if (_rigManager == null) return;

      RegisterButtonPresses(_rigManager.ControllerRig.leftController, ref _buttonsPressedLeft);
      RegisterButtonPresses(_rigManager.ControllerRig.rightController, ref _buttonsPressedRight);
    }
    private void RegisterButtonPresses(StressLevelZero.Rig.BaseController controller, ref byte pressed)
    {
      if (controller.GetAButton()) pressed |= (byte)Bwr.ButtonPress.A;
      if (controller.GetBButton()) pressed |= (byte)Bwr.ButtonPress.B;
      if (controller.GetMenuButton()) pressed |= (byte)Bwr.ButtonPress.MENU;
      if (controller.GetSecondaryMenuButton()) pressed |= (byte)Bwr.ButtonPress.SECONDARY_MENU;
      if (controller.GetThumbStick()) pressed |= (byte)Bwr.ButtonPress.THUMB_STICK;
      if (controller.GetPrimaryInteractionButton()) pressed |= (byte)Bwr.ButtonPress.PRIMARY_INTERACTION;
      if (controller.GetSecondaryInteractionButton()) pressed |= (byte)Bwr.ButtonPress.SECONDARY_INTERACTION;
      if (controller.GetGrabbedState()) pressed |= (byte)Bwr.ButtonPress.GRABBED_STATE;
    }

    public byte[] BuildFrame()
    {
      if (_rigManager == null)
      {
        _rigManager = Object.FindObjectOfType<StressLevelZero.Rig.RigManager>();
      }

      var builder = new FlatBuffers.FlatBufferBuilder(1024);

      Bwr.Frame.StartFrame(builder);
      Bwr.Frame.AddTime(builder, Time.time);

      var dataPlayer = Object.FindObjectOfType<Data_Manager>().data_player;
      var settings = new (Bwr.SettingType, float)[] {
        (Bwr.SettingType.BELT_RIGHT_SIDE, dataPlayer.beltRightSide ? 1 : 0),
        (Bwr.SettingType.IS_INVERTED, dataPlayer.isInverted ? 1 : 0),
        (Bwr.SettingType.IS_RIGHT_HANDED, dataPlayer.isRightHanded ? 1 : 0),
        (Bwr.SettingType.JOY_SENSITIVITY, dataPlayer.joySensitivityNew),
        (Bwr.SettingType.LOCO_CURVE, dataPlayer.loco_Curve),
        (Bwr.SettingType.LOCO_DEGREES_PER_SNAP, dataPlayer.loco_DegreesPerSnap),
        (Bwr.SettingType.LOCO_DIRECTION, dataPlayer.loco_Direction),
        (Bwr.SettingType.LOCO_SNAP_DEGREES_PER_FRAME, dataPlayer.loco_SnapDegreesPerFrame),
        (Bwr.SettingType.OFFSET_FLOOR, dataPlayer.offset_Floor),
        (Bwr.SettingType.OFFSET_SITTING, dataPlayer.offset_Sitting),
        (Bwr.SettingType.PHYSICS_UPDATE_RATE, dataPlayer.physicsUpdateRate),
        (Bwr.SettingType.PLAYER_HEIGHT, dataPlayer.player_Height),
        (Bwr.SettingType.SNAP_ENABLED, dataPlayer.SnapEnabled ? 1 : 0),
        (Bwr.SettingType.STANDING, dataPlayer.standing ? 1 : 0),
        (Bwr.SettingType.VIRTUAL_CROUCHING, dataPlayer.VirtualCrouching ? 1 : 0),
      };
      var changedSettings = _prevSettings == null
        ? settings
        : settings.Where((setting, i) => setting.Item2 != _prevSettings[i].Item2).ToArray();
      if (changedSettings.Length > 0)
      {
        Bwr.Frame.StartChangedSettingsVector(builder, changedSettings.Length);
        foreach (var (type, value) in changedSettings)
          Bwr.ChangedSetting.CreateChangedSetting(builder, type, value);
        builder.EndVector();
        _prevSettings = settings;
      }

      var hmdTransform = _rigManager.ControllerRig.hmdTransform;
      var hmdEulerAngles = hmdTransform.eulerAngles;
      var controllerLeft = _rigManager.ControllerRig.leftController;
      var controllerLeftThumbstickAxis = controllerLeft.GetThumbStickAxis();
      var controllerLeftTransform = controllerLeft.transform;
      var controllerLeftEulerAngles = controllerLeftTransform.eulerAngles;
      var controllerRight = _rigManager.ControllerRig.rightController;
      var controllerRightThumbstickAxis = controllerRight.GetThumbStickAxis();
      var controllerRightTransform = controllerRight.transform;
      var controllerRightEulerAngles = controllerRightTransform.eulerAngles;
      var vrRoot = _rigManager.ControllerRig.vrRoot;
      Bwr.Frame.AddVrInput(builder, Bwr.VrInput.CreateVrInput(
        builder,
        hmdTransform.position.x,
        hmdTransform.position.y,
        hmdTransform.position.z,
        hmdEulerAngles.x,
        hmdEulerAngles.y,
        hmdEulerAngles.z,
        controllerLeftTransform.position.x,
        controllerLeftTransform.position.y,
        controllerLeftTransform.position.z,
        controllerLeftEulerAngles.x,
        controllerLeftEulerAngles.y,
        controllerLeftEulerAngles.z,
        controllerRightTransform.position.x,
        controllerRightTransform.position.y,
        controllerRightTransform.position.z,
        controllerRightEulerAngles.x,
        controllerRightEulerAngles.y,
        controllerRightEulerAngles.z,
        vrRoot.position.x,
        vrRoot.position.y,
        vrRoot.position.z,
        vrRoot.eulerAngles.y,
        _buttonsPressedLeft,
        controllerLeftThumbstickAxis.x,
        controllerLeftThumbstickAxis.y,
        _buttonsPressedRight,
        controllerRightThumbstickAxis.x,
        controllerRightThumbstickAxis.y
      ));

      var frame = Bwr.Frame.EndFrame(builder);
      builder.Finish(frame.Value);

      _buttonsPressedLeft = _buttonsPressedRight = 0;

      // Utils.LogDebug($"Recorded frame: {currentSceneIdx} {cam.position.ToString()}");
      return builder.SizedByteArray();
    }
  }
}

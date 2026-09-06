using StressLevelZero.Player;
using StressLevelZero.Rig;
using Valve.VR;

namespace Sst.BoneworksPerformance;

internal static class SlowMotionButtonTiming {
  public static BaseController Controller(ControllerRig rig) =>
      !rig ? null : rig.isRightHanded
          ? rig.leftController
          : rig.rightController;

  public static bool IsSupported(BaseController controller) =>
      controller && controller.controllerType !=
      ControllerInfo.Type.UNDEFINED;

  public static SteamVR_Input_Sources Source(ControllerRig rig) =>
      rig != null && rig.isRightHanded
          ? SteamVR_Input_Sources.LeftHand
          : SteamVR_Input_Sources.RightHand;

  public static SteamVR_Action_Boolean Action(BaseController controller) {
    if (!IsSupported(controller))
      return null;
    if (controller.controllerType == ControllerInfo.Type.VIVE_WANDS)
      return SteamVR_Actions.vivewands_ApplicationClick;
    return controller.controllerType == ControllerInfo.Type.OCCULUS_TOUCH
        ? SteamVR_Actions.oculustouch_AClick
        : SteamVR_Actions.default_AClick;
  }

  public static bool IsPressed(BaseController controller) =>
      controller && controller.controllerType == ControllerInfo.Type.VIVE_WANDS
          ? controller.GetBButton()
          : controller && controller.GetAButton();

  public static bool WasPressed(BaseController controller) =>
      controller && controller.controllerType == ControllerInfo.Type.VIVE_WANDS
          ? controller.GetBButtonDown()
          : controller && controller.GetAButtonDown();

  public static bool WasReleased(BaseController controller) =>
      controller && controller.controllerType == ControllerInfo.Type.VIVE_WANDS
          ? controller.GetBButtonUp()
          : controller && controller.GetAButtonUp();

}

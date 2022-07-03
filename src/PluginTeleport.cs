using MelonLoader;
using UnityEngine;

namespace SpeedrunTools
{
  class PluginTeleport : Plugin
  {
    private static StressLevelZero.Rig.RigManager s_rigManager;
    private static GameControl s_gameControl;
    private static Vector3? s_teleportPos;
    private static Vector3? s_resetPos;
    private static int s_currentSceneIdx;

    public readonly Hotkey HotkeySet = new Hotkey()
    {
      Predicate = (cl, cr) =>
        s_rigManager != null && s_currentSceneIdx != Utils.SCENE_MENU_IDX &&
        cl.GetBButton() && cr.GetBButton(),
      Handler = () =>
      {
        var pos = Utils.GetPlayerPos(s_rigManager);
        MelonLogger.Msg($"Setting teleport position: {pos.ToString()}");
        s_teleportPos = pos;
      }
    };

    public readonly Hotkey HotkeyTeleport = new Hotkey()
    {
      Predicate = (cl, cr) =>
        s_rigManager != null && s_currentSceneIdx != Utils.SCENE_MENU_IDX &&
        s_teleportPos.HasValue && cr.GetThumbStick(),
      Handler = () =>
      {
        MelonLogger.Msg("Teleporting");
        s_rigManager.Teleport(s_teleportPos.Value);
      }
    };

    public readonly Hotkey HotkeyReset = new Hotkey()
    {
      Predicate = (cl, cr) =>
        s_currentSceneIdx != Utils.SCENE_MENU_IDX &&
        cl.GetAButton() && cl.GetBButton(),
      Handler = () =>
      {
        MelonLogger.Msg("Resetting level");
        s_resetPos = Utils.GetPlayerPos(s_rigManager);
        s_gameControl.RELOADLEVEL();
      }
    };

    public override void OnSceneWasInitialized(int buildIndex, string sceneName)
    {
      // Init teleport
      if (buildIndex != s_currentSceneIdx)
      {
        s_currentSceneIdx = buildIndex;
        s_teleportPos = null;
      }
      s_rigManager = Object.FindObjectOfType<StressLevelZero.Rig.RigManager>();

      // Init reset
      s_gameControl = Object.FindObjectOfType<GameControl>();
      if (s_resetPos.HasValue)
      {
        Utils.LogDebug($"Teleporting on reset to: {s_resetPos.Value.ToString()}");
        s_rigManager.Teleport(s_resetPos.Value);
        s_resetPos = null;
      }
    }
  }
}

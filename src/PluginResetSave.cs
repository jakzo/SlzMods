using MelonLoader;

namespace SpeedrunTools
{
  class PluginResetSave : Plugin
  {
    private const int SCENE_MENU_IDX = 1;
    private static int s_currentSceneIdx;

    public readonly Hotkey HotkeyReset = new Hotkey()
    {
      Predicate = (cl, cr) => cl.GetAButton() && cl.GetBButton(),
      Handler = () =>
      {
        if (s_currentSceneIdx != SCENE_MENU_IDX)
        {
          MelonLogger.Warning("Can only reset save when in menu. Not resetting save.");
          return;
        }

        MelonLogger.Msg("Resetting save");
        UnityEngine.Object.FindObjectOfType<Data_Manager>().DATA_DEFAULT();
        StressLevelZero.Utilities.BoneworksSceneManager.ReloadScene();
      }
    };

    public override void OnSceneWasInitialized(int buildIndex, string sceneName)
    {
      s_currentSceneIdx = buildIndex;
    }
  }
}

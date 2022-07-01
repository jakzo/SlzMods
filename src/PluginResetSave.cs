using MelonLoader;

namespace SpeedrunTools
{
  class PluginResetSave : Plugin
  {
    private const string SCENE_MENU = "";
    private const string UUID = "00000000-0000-0000-0000-000000000000";

    public readonly Hotkey HotkeyReset = new Hotkey()
    {
      Predicate = (cl, cr) => cl.GetAButton() && cl.GetBButton(),
      Handler = () =>
      {
        MelonLogger.Msg("Resetting save and returning to menu");
        foreach (var key in SaveTracker.SaveStates.Keys)
        {
          SaveTracker.RemoveSaveState(key);
        }
        SaveTracker.AddSaveState(UUID, new SaveState());
        SaveTracker.Load();
        StressLevelZero.Utilities.BoneworksSceneManager.LoadScene(SCENE_MENU);
      }
    };
  }
}

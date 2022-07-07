using MelonLoader;
using UnityEngine;

namespace SpeedrunTools
{
  class PluginBlindfold : Plugin
  {
    private const float FADE_TIME = 0.15f;

    static private bool s_isBlindfolded = false;

    public readonly Hotkey HotkeyReset = new Hotkey()
    {
      Predicate = (cl, cr) => Input.GetKey(KeyCode.LeftControl) && Input.GetKey(KeyCode.B),
      Handler = () =>
      {
        var controlCharacter = Object.FindObjectOfType<Control_Character>();
        if (controlCharacter == null)
        {
          MelonLogger.Warning("No character to blindfold");
          return;
        }
        var vcontroller = controlCharacter.ctrl_Camera.Vcontroller;
        if (s_isBlindfolded)
        {
          MelonLogger.Msg("Unblindfolding...");
          vcontroller.FadeOn(FADE_TIME);
        } else
        {
          MelonLogger.Msg("Blindfolding...");
          vcontroller.FadeOff(FADE_TIME);
        }
      }
    };
  }
}

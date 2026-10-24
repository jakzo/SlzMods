using MelonLoader;
namespace Sst.BoneworksFrameFix;

internal sealed partial class PhysicsRateMenu
{
  private int _lastSelectedRate = int.MinValue;
  private void DebugResetRateLog() => _lastSelectedRate = int.MinValue;
  private void DebugRateSelected(int selectedRate)
  {
    if (selectedRate != _lastSelectedRate)
    {
      _lastSelectedRate = selectedRate;
      MelonLogger.Msg(
          selectedRate > 0
              ? (_customRate > 0
                  ? "Custom physics tick rate selected " + selectedRate +
                    " Hz."
                  : "Physics-rate menu selected " + selectedRate + " Hz.")
              : "Physics-rate menu has no valid selected rate."
      );
    }

  }
}

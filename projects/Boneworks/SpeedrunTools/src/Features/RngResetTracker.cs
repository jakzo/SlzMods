using System.Collections.Generic;
using Sst.Common.Boneworks;

namespace Sst.Features {
// Count a visit only when the player attempts an eligible item and then
// leaves through level select without getting that item. Progression and other
// loads discard the visit.
class RngResetTracker {
  private int? _currentScene;
  private readonly HashSet<string> _attemptedItems = new HashSet<string>();

  public void Clear() {
    _currentScene = null;
    _attemptedItems.Clear();
  }

  public bool OnLoadRequested(
      bool fromLevelMenu, HundredPercentState state
  ) {
    var changed = false;
    if (fromLevelMenu) {
      foreach (var uuid in _attemptedItems) {
        if (state.rngUnlocks.TryGetValue(uuid, out var item) && !item.hasDropped) {
          item.resets++;
          changed = true;
        }
      }
    }
    Clear();
    return changed;
  }

  public void OnLoadingScreen() {
    Clear();
  }

  public void OnSceneInitialized(int sceneIndex) {
    Clear();
    _currentScene = sceneIndex;
  }

  public void OnAttempt(
      string uuid, HundredPercentState.RngState item, bool isReclaimed
  ) {
    var itemScene = item.name == "Baton" ? 6 : 5;
    if (_currentScene == itemScene && !item.hasDropped && !isReclaimed)
      _attemptedItems.Add(uuid);
  }
}
}

using System;
using System.Linq;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using LiveSplit.UI;
using LiveSplit.UI.Components;
using LiveSplit.Model;
using Sst.Common.LiveSplit;
using Sst.Common.Boneworks;

namespace Sst.Livesplit.BoneworksHundredStatus {
public class Component : IComponent {
  public const string NAME = "Boneworks 100% Status";

  public float HorizontalWidth { get => 300; }
  public float VerticalHeight { get => 240; }
  public float MinimumWidth { get => HorizontalWidth; }
  public float MinimumHeight { get => VerticalHeight; }

  public float PaddingTop { get => 0; }
  public float PaddingLeft { get => 0; }
  public float PaddingBottom { get => 0; }
  public float PaddingRight { get => 0; }

  public IDictionary<string, Action> ContextMenuControls { get => null; }

  private SimpleLabel _progressLabel = new SimpleLabel();
  private BoneworksStateUpdater _stateUpdater = new BoneworksStateUpdater();
  private int _collectiblePos = 0;
  private HashSet<HundredPercentState.Collectible> _missingCollectibles =
      new HashSet<HundredPercentState.Collectible>();
  private bool _isDirty = true;

  public Component(LiveSplitState state) {
    Log.Initialize();
    _stateUpdater.OnReceivedState += OnReceivedState;
  }

  private void OnReceivedState(HundredPercentState receivedState) {
    _isDirty = true;
    if (receivedState == null)
      return;

    if (receivedState.levelCollectibles != null) {
      _collectiblePos = 0;
      _missingCollectibles = new HashSet<HundredPercentState.Collectible>();
    }
    if (receivedState.justCollected != null) {
      foreach (var collectible in receivedState.justCollected) {
        if (!_stateUpdater.LevelCollectableIndexes.TryGetValue(collectible.Uuid,
                                                               out var index))
          continue;

        if (index >= _collectiblePos) {
          for (var i = _collectiblePos; i < index; i++) {
            _missingCollectibles.Add(_stateUpdater.LevelCollectibles[i]);
          }
          _collectiblePos = index + 1;

          // Right before we get the last collectible, mark it as missing so
          // that we notice it before we reach the finish in case we forgot
          // about it
          var lastCollectibleIndex = _stateUpdater.LevelCollectibles.Length - 1;
          if (index == lastCollectibleIndex - 1) {
            _missingCollectibles.Add(
                _stateUpdater.LevelCollectibles[lastCollectibleIndex]);
          }
        }

        if (_missingCollectibles.Contains(
                _stateUpdater.LevelCollectibles[index])) {
          _missingCollectibles.Remove(_stateUpdater.LevelCollectibles[index]);
        }
      }
    }
  }

  public void DrawHorizontal(Graphics g, LiveSplitState state, float height,
                             Region clipRegion) {
    DrawGeneral(g, state, HorizontalWidth, height, LayoutMode.Horizontal);
  }

  public void DrawVertical(System.Drawing.Graphics g, LiveSplitState state,
                           float width, Region clipRegion) {
    DrawGeneral(g, state, width, VerticalHeight, LayoutMode.Vertical);
  }

  private void DrawGeneral(Graphics g, LiveSplitState state, float width,
                           float height, LayoutMode mode) {
    _progressLabel.HorizontalAlignment = StringAlignment.Near;
    _progressLabel.VerticalAlignment = StringAlignment.Far;
    _progressLabel.X = 4;
    _progressLabel.Y = 0;
    _progressLabel.Width = width;
    _progressLabel.Height = height;
    _progressLabel.Font = state.LayoutSettings.TextFont;
    _progressLabel.Brush = new SolidBrush(state.LayoutSettings.TextColor);
    _progressLabel.HasShadow = state.LayoutSettings.DropShadows;
    _progressLabel.ShadowColor = state.LayoutSettings.ShadowsColor;
    _progressLabel.OutlineColor = state.LayoutSettings.TextOutlineColor;
    _progressLabel.Draw(g);
  }

  public string ComponentName { get => NAME; }

  public Control GetSettingsControl(LayoutMode mode) => null;
  public System.Xml.XmlNode GetSettings(System.Xml.XmlDocument document) =>
      document.CreateElement("Settings");
  public void SetSettings(System.Xml.XmlNode settings) {}

  // TODO: Settings page
  // public Control GetSettingsControl(LayoutMode mode) {
  //   Settings.Mode = mode;
  //   return Settings;
  // }

  // public System.Xml.XmlNode GetSettings(System.Xml.XmlDocument document) {
  //   return Settings.GetSettings(document);
  // }

  // public void SetSettings(System.Xml.XmlNode settings) {
  //   Settings.SetSettings(settings);
  // }

  public void Dispose() { _stateUpdater.Dispose(); }

  public void Update(IInvalidator invalidator, LiveSplitState livesplitState,
                     float width, float height, LayoutMode mode) {
    if (!_isDirty)
      return;

    _isDirty = false;
    var state = _stateUpdater.State;
    if (state == null) {
      _progressLabel.Text = "";
    } else {
      var overallChance = state.rngUnlocks.Aggregate(
          1f, (chance, pair) =>
                  chance * (1f - pair.Value.probabilityNotDroppedYet));
      var overallChanceStr = (overallChance * 100f).ToString("N0");
      _progressLabel.Text = string.Join(
          "\n",
          _missingCollectibles.Select(c => $"Missed: {c.DisplayName}")
              .Concat(state.rngUnlocks.All(pair => pair.Value.hasDropped)
                          ? new[] { $"Overall RNG chance: {overallChanceStr}%" }
                          : new string[] {})
              .Concat(new[] {
                $"Level unlocks: {state.unlockLevelCount} / {state.unlockLevelMax}",
                $"Level Ammo: {state.ammoLevelCount} / {state.ammoLevelMax}",
              })
              .Concat(state.rngUnlocks.Select(pair => {
                var u = pair.Value;
                var status = u.hasDropped ? "✅" : "❌";
                var triesStr = u.attempts == 1 ? "try" : "tries";
                var attemptChanceStr =
                    (u.prevAttemptChance * 100f).ToString("N0");
                var total = 1f - u.probabilityNotDroppedYet;
                var totalStr = (total * 100f).ToString("N0");
                return $"{status} {u.name}: {u.attempts} {triesStr} @ {attemptChanceStr}% per try = {totalStr}% total";
              })));
    }
    invalidator?.Invalidate(0, 0, width, height);
  }
}
}

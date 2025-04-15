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
using System.Xml;

namespace Sst.Livesplit.BoneworksHundredStatus {
public class Component : IComponent {
  public const string NAME = "Boneworks 100% Status";

  private float _height = 240;
  private bool _showMissingCollectibles = true;

  public float HorizontalWidth { get => 300; }
  public float VerticalHeight { get => _height; }
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
  private HashSet<HundredPercentState.Collectible> _remainingCollectibles =
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
      _remainingCollectibles = new HashSet<HundredPercentState.Collectible>();
    }
    if (receivedState.justCollected != null) {
      foreach (var collectible in receivedState.justCollected) {
        if (!_stateUpdater.LevelCollectableIndexes.TryGetValue(
                collectible.Uuid, out var index
            ))
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
            _remainingCollectibles.Add(
                _stateUpdater.LevelCollectibles[lastCollectibleIndex]
            );
          }
        }

        if (_missingCollectibles.Contains(_stateUpdater.LevelCollectibles[index]
            )) {
          _missingCollectibles.Remove(_stateUpdater.LevelCollectibles[index]);
        }
        if (_remainingCollectibles.Contains(
                _stateUpdater.LevelCollectibles[index]
            )) {
          _remainingCollectibles.Remove(_stateUpdater.LevelCollectibles[index]);
        }
      }
    }
  }

  public void DrawHorizontal(
      Graphics g, LiveSplitState state, float height, Region clipRegion
  ) {
    DrawGeneral(g, state, HorizontalWidth, height, LayoutMode.Horizontal);
  }

  public void DrawVertical(
      System.Drawing.Graphics g, LiveSplitState state, float width,
      Region clipRegion
  ) {
    DrawGeneral(g, state, width, VerticalHeight, LayoutMode.Vertical);
  }

  private void DrawGeneral(
      Graphics g, LiveSplitState state, float width, float height,
      LayoutMode mode
  ) {
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

  public XmlNode GetSettings(XmlDocument document) {
    var root = document.CreateElement("Settings");

    var height = document.CreateElement("Height");
    height.InnerText = _height.ToString();
    root.AppendChild(height);

    var showMissingCollectibles =
        document.CreateElement("ShowMissingCollectibles");
    showMissingCollectibles.InnerText = _showMissingCollectibles.ToString();
    root.AppendChild(height);

    var splitOnArena = document.CreateElement("SplitOnArena");
    splitOnArena.InnerText = _showMissingCollectibles.ToString();
    root.AppendChild(height);

    return root;
  }

  public void SetSettings(XmlNode settings) {
    foreach (var n in settings.ChildNodes) {
      var node = n as XmlNode;
      switch (node.Name) {
      case "Height":
        _height = float.Parse(node.InnerText);
        break;
      case "ShowMissingCollectibles":
        _showMissingCollectibles = bool.Parse(node.InnerText);
        break;
      }
    }
  }

  public void Dispose() { _stateUpdater.Dispose(); }

  public void Update(
      IInvalidator invalidator, LiveSplitState livesplitState, float width,
      float height, LayoutMode mode
  ) {
    if (!_isDirty)
      return;

    _isDirty = false;
    var state = _stateUpdater.State;
    if (state == null) {
      _progressLabel.Text = "";
    } else {
      var missingCollectibleLines = _showMissingCollectibles
          ? _missingCollectibles.Select(c => $"Missed: {c.DisplayName}")
                .Concat(_remainingCollectibles.Select(
                    c => $"Remaining: {c.DisplayName}"
                ))
          : new string[] {};
      var overallChance = PercentileMapping(
          state.rngUnlocks
              .Select(pair => (double)pair.Value.probabilityNotDroppedYet)
              .ToArray()
      );
      var overallChanceStr = (overallChance * 100f).ToString("N0");
      _progressLabel.Text = string.Join(
          "\n",
          missingCollectibleLines
              .Concat(
                  state.rngUnlocks.All(pair => pair.Value.hasDropped)
                      ? new[] { $"Overall RNG chance: {overallChanceStr}%" }
                      : new string[] {}
              )
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
              }))
      );
    }
    invalidator?.Invalidate(0, 0, width, height);
  }

  // TODO: I would much rather a formula which produces a uniform distribution
  //       than using a lookup table but I can't think of one
  private static double[] MAPPINGS = {
    0.0585, 0.1256, 0.1477, 0.1638, 0.1765, 0.188,  0.1974, 0.2066, 0.2145,
    0.2229, 0.2309, 0.2384, 0.2454, 0.252,  0.2581, 0.2642, 0.2704, 0.2766,
    0.2827, 0.288,  0.2937, 0.2989, 0.3045, 0.3097, 0.3147, 0.3198, 0.3249,
    0.3297, 0.3349, 0.3398, 0.3447, 0.3497, 0.3544, 0.3591, 0.364,  0.3686,
    0.3731, 0.3779, 0.3826, 0.3873, 0.3921, 0.3966, 0.4013, 0.4062, 0.4109,
    0.4156, 0.4204, 0.4253, 0.4301, 0.4351, 0.44,   0.4451, 0.4502, 0.4554,
    0.4608, 0.4664, 0.4717, 0.477,  0.4825, 0.4879, 0.4935, 0.4989, 0.5045,
    0.51,   0.5155, 0.521,  0.5268, 0.5326, 0.5384, 0.5443, 0.5502, 0.5564,
    0.5625, 0.569,  0.5755, 0.5823, 0.5892, 0.5959, 0.6029, 0.61,   0.6173,
    0.6248, 0.6326, 0.6405, 0.6488, 0.6574, 0.6662, 0.6753, 0.6849, 0.6948,
    0.7055, 0.7168, 0.7287, 0.7416, 0.7557, 0.7711, 0.7887, 0.8092, 0.8347,
    0.8701, 1.0,
  };
  private static double PercentileMapping(double[] probabilitiesNotDroppedYet) {
    var geometricMean = Math.Pow(
        probabilitiesNotDroppedYet.Aggregate(
            1.0, (acc, prob) => acc * (1.0 - prob)
        ),
        1.0 / probabilitiesNotDroppedYet.Length
    );
    var index = (int)(geometricMean * (MAPPINGS.Length - 1));
    var lowerValue = MAPPINGS[index];
    var upperValue = MAPPINGS[index + 1];
    var lowerPercentile = (double)index / (MAPPINGS.Length - 1);
    var upperPercentile = (double)(index + 1) / (MAPPINGS.Length - 1);
    var ratio = (geometricMean - lowerValue) / (upperValue - lowerValue);
    return lowerPercentile + ratio * (upperPercentile - lowerPercentile);
  }
}
}

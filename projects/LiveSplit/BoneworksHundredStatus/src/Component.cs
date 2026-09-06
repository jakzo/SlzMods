using System;
using System.Linq;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
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
  private bool _showResets = true;
  private (string Name, string Count, string Details, Color NameColor, bool HasDropped)[] _rngRows = [];

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
      Graphics g, LiveSplitState state, float width, Region clipRegion
  ) {
    DrawGeneral(g, state, width, VerticalHeight, LayoutMode.Vertical);
  }

  private void DrawGeneral(
      Graphics g, LiveSplitState state, float width, float height,
      LayoutMode mode
  ) {
    DrawRngDisplay(g, state, width, height);
  }

  private void DrawRngDisplay(
      Graphics g, LiveSplitState state, float width, float height
  ) {
    var font = state.LayoutSettings.TextFont;
    var iconWidth = font.GetHeight(g);
    var availableWidth = Math.Max(1, width - 8);
    using (var format = (StringFormat)StringFormat.GenericTypographic.Clone()) {
      // GraphicsPath and DrawString round line heights differently. Do not
      // discard a whole outlined row when its final pixel exceeds the bounds.
      format.FormatFlags &= ~StringFormatFlags.LineLimit;
      format.FormatFlags |= StringFormatFlags.MeasureTrailingSpaces;
      var separatorWidth = g.MeasureString(": ", font, int.MaxValue, format).Width;
      var progressHeight = g.MeasureString(
          _progressLabel.Text, font, (int)availableWidth, format
      ).Height;
      var rows = _rngRows.Select(row => {
        var nameWidth = g.MeasureString(row.Name, font, int.MaxValue, format).Width;
        var countWidth = row.Count.Length == 0 ? 0 :
            g.MeasureString(row.Count, font, int.MaxValue, format).Width;
        var detailsWidth = Math.Max(1, availableWidth - iconWidth - nameWidth - separatorWidth - countWidth);
        var rowHeight = g.MeasureString(
            row.Details, font, (int)detailsWidth, format
        ).Height;
        return (row, nameWidth, countWidth, detailsWidth, rowHeight);
      }).ToArray();
      var y = height - progressHeight - rows.Sum(row => row.rowHeight);
      DrawResetText(g, state, _progressLabel.Text, state.LayoutSettings.TextColor,
                    4, y, availableWidth, progressHeight, format);
      y += progressHeight;
      foreach (var row in rows) {
        DrawStatusIcon(g, row.row.HasDropped, 4, y, iconWidth);
        DrawResetText(g, state, row.row.Name,
                      row.row.NameColor,
                      4 + iconWidth, y, row.nameWidth + 1, row.rowHeight, format);
        var x = 4 + iconWidth + row.nameWidth;
        DrawResetText(g, state, ": ", state.LayoutSettings.TextColor,
                      x, y, separatorWidth + 1, row.rowHeight, format);
        x += separatorWidth;
        DrawResetText(g, state, row.row.Count,
                      state.LayoutSettings.TextColor,
                      x, y, row.countWidth + 1, row.rowHeight, format);
        x += row.countWidth;
        DrawResetText(g, state, row.row.Details, state.LayoutSettings.TextColor,
                      x, y, row.detailsWidth, row.rowHeight, format);
        y += row.rowHeight;
      }
    }
  }

  private static void DrawStatusIcon(
      Graphics g, bool hasDropped, float x, float y, float size
  ) {
    var previousSmoothing = g.SmoothingMode;
    g.SmoothingMode = SmoothingMode.AntiAlias;
    using (var pen = new Pen(hasDropped ? Color.Lime : Color.White,
                             Math.Max(1.4f, size / 10f))) {
      pen.StartCap = pen.EndCap = LineCap.Round;
      if (hasDropped) {
        g.DrawLines(pen, new[] {
          new PointF(x + size * 0.15f, y + size * 0.50f),
          new PointF(x + size * 0.35f, y + size * 0.72f),
          new PointF(x + size * 0.75f, y + size * 0.25f),
        });
      } else {
        g.DrawLine(pen, x + size * 0.20f, y + size * 0.30f,
                   x + size * 0.65f, y + size * 0.70f);
        g.DrawLine(pen, x + size * 0.65f, y + size * 0.30f,
                   x + size * 0.20f, y + size * 0.70f);
      }
    }
    g.SmoothingMode = previousSmoothing;
  }

  private void DrawResetText(
      Graphics g, LiveSplitState state, string text, Color color,
      float x, float y, float width, float height, StringFormat format
  ) {
    if (string.IsNullOrEmpty(text))
      return;
    using (var brush = new SolidBrush(color)) {
      var font = state.LayoutSettings.TextFont;
      var bounds = new RectangleF(x, y, width, height);
      if (state.LayoutSettings.DropShadows) {
        using (var shadow = new SolidBrush(state.LayoutSettings.ShadowsColor)) {
          g.DrawString(text, font, shadow,
                       new RectangleF(x + 1, y + 1, width, height), format);
          g.DrawString(text, font, shadow,
                       new RectangleF(x + 2, y + 2, width, height), format);
        }
      }
      if (state.LayoutSettings.TextOutlineColor.A > 0) {
        using (var path = new GraphicsPath())
        using (var pen = new Pen(state.LayoutSettings.TextOutlineColor, 1) {
          LineJoin = LineJoin.Round,
        }) {
          path.AddString(text, font.FontFamily, (int)font.Style,
                         font.SizeInPoints * g.DpiY / 72f, bounds, format);
          g.DrawPath(pen, path);
          g.FillPath(brush, path);
        }
      } else {
        g.DrawString(text, font, brush, bounds, format);
      }
    }
  }

  public string ComponentName { get => NAME; }

  public Control GetSettingsControl(LayoutMode mode) {
    var panel = new FlowLayoutPanel {
      AutoSize = true, FlowDirection = FlowDirection.TopDown,
      Padding = new Padding(8),
    };
    var showResets = new CheckBox {
      Text = "Show level resets instead of item drop tries",
      AutoSize = true, Checked = _showResets,
    };
    showResets.CheckedChanged += (sender, args) => {
      _showResets = showResets.Checked;
      _isDirty = true;
    };
    panel.Controls.Add(showResets);
    return panel;
  }

  public XmlNode GetSettings(XmlDocument document) {
    var root = document.CreateElement("Settings");

    var height = document.CreateElement("Height");
    height.InnerText = _height.ToString();
    root.AppendChild(height);

    var showMissingCollectibles =
        document.CreateElement("ShowMissingCollectibles");
    showMissingCollectibles.InnerText = _showMissingCollectibles.ToString();
    root.AppendChild(showMissingCollectibles);

    var showResets = document.CreateElement("ShowResets");
    showResets.InnerText = _showResets.ToString();
    root.AppendChild(showResets);

    return root;
  }

  public void SetSettings(XmlNode settings) {
    _showResets = true;
    foreach (var n in settings.ChildNodes) {
      var node = n as XmlNode;
      switch (node.Name) {
      case "Height":
        _height = float.Parse(node.InnerText);
        break;
      case "ShowMissingCollectibles":
        _showMissingCollectibles = bool.Parse(node.InnerText);
        break;
      case "ShowResets":
        _showResets = bool.Parse(node.InnerText);
        break;
      }
    }
    _isDirty = true;
  }

  public void Dispose() {
    _stateUpdater.Dispose();
    _progressLabel.Brush?.Dispose();
  }

  public void Update(
      IInvalidator invalidator, LiveSplitState livesplitState, float width,
      float height, LayoutMode mode
  ) {
    if (!_isDirty)
      return;

    _isDirty = false;
    var state = _stateUpdater.State;
    _rngRows = state == null ? [] : state.rngUnlocks.Values
        .Select(item => (item.name,
                         _showResets ? RngDisplay.ResetCount(item) : RngDisplay.TriesCount(item),
                         _showResets ? RngDisplay.ResetSuffix(item) : RngDisplay.TriesSuffix(item),
                         RngDisplay.NameColor(item),
                         item.hasDropped))
        .ToArray();
    if (state == null) {
      _progressLabel.Text = "";
    } else {
      var missingCollectibleLines = _showMissingCollectibles
          ? _missingCollectibles.Select(c => $"Missed: {c.DisplayName}")
                .Concat(_remainingCollectibles.Select(
                    c => $"Remaining: {c.DisplayName}"
                ))
          : [];
      var allDropped = state.rngUnlocks.Values.All(item => item.hasDropped);
      var overallChance = allDropped ? OverallRngLuck.Percentile(
          state.rngUnlocks.Values.First(item => item.name == "Baseball").attempts,
          state.rngUnlocks.Values.First(item => item.name == "Golf Club").attempts,
          state.rngUnlocks.Values.First(item => item.name == "Baton").attempts
      ) : 0;
      var overallChanceStr = Math.Floor(overallChance * 100).ToString("N0");
      _progressLabel.Text = string.Join(
          "\n",
          missingCollectibleLines
              .Concat(
                  allDropped
                      ? [$"Overall RNG chance: {overallChanceStr}%"]
                      : []
              )
              .Concat([
                $"Level unlocks: {state.unlockLevelCount} / {state.unlockLevelMax}",
                $"Level Ammo: {state.ammoLevelCount} / {state.ammoLevelMax}",
              ])
      );
    }
    invalidator?.Invalidate(0, 0, width, height);
  }

}
}

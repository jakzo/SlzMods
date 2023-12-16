using System;
using System.Linq;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using LiveSplit.UI;
using LiveSplit.UI.Components;
using LiveSplit.Model;
using Sst.Common.LiveSplit;

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
  private int _eventIndex = 0;
  private bool _isDirty = true;
  private TimerModel _timer;
  private string _prevLevelBarcode;

  public Component(LiveSplitState state) {
    Log.Initialize();
    _timer = new TimerModel() { CurrentState = state };
    _stateUpdater.OnReceivedState += receivedState => _isDirty = true;
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

  public void Dispose() { _stateUpdater.Dispose(); }

  public void Update(IInvalidator invalidator, LiveSplitState livesplitState,
                     float width, float height, LayoutMode mode) {
    if (_isDirty) {
      _isDirty = false;
      var state = _stateUpdater.State;
      _progressLabel.Text = string.Join("\n", new[] {
        $"RNG Unlocks: {state.unlockRngCount} / {state.unlockRngMax}",
        $"Other Unlocks: {state.unlockNormalCount} / {state.unlockNormalMax} (level = ~{state.unlockNormalLevel})",
        $"Level Ammo: {state.levelAmmoCount} / {state.levelAmmoMax}",
      });
      invalidator.Invalidate(0, 0, width, height);
    }
  }
}
}

using System;
using System.Linq;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using LiveSplit.UI;
using LiveSplit.UI.Components;
using LiveSplit.Model;

namespace Sst.Livesplit.BonelabHundredPercentStatus {
public class Component : IComponent {
  public float HorizontalWidth { get => 300; }
  public float VerticalHeight { get => 200; }
  public float MinimumWidth { get => HorizontalWidth; }
  public float MinimumHeight { get => VerticalHeight; }

  public float PaddingTop { get => 0; }
  public float PaddingLeft { get => 0; }
  public float PaddingBottom { get => 0; }
  public float PaddingRight { get => 0; }

  public IDictionary<string, Action> ContextMenuControls { get => null; }

  private SimpleLabel _progressLabel = new SimpleLabel();
  private BonelabStateUpdater _stateUpdater = new BonelabStateUpdater();
  private int _eventIndex = 0;
  private bool _isDirty = true;

  public Component(LiveSplitState state) {
    _stateUpdater.OnReceivedState += receivedState => { _isDirty = true; };
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
    _progressLabel.X = 0;
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

  public string ComponentName { get => "Bonelab 100% Status"; }

  // TODO: Return null for no settings page?
  private UserControl _settings = new UserControl();
  public Control GetSettingsControl(LayoutMode mode) => _settings;
  public System.Xml.XmlNode GetSettings(System.Xml.XmlDocument document) =>
      document.CreateElement("Settings");
  public void SetSettings(System.Xml.XmlNode settings) {}

  public void Update(IInvalidator invalidator, LiveSplitState livesplitState,
                     float width, float height, LayoutMode mode) {
    var threshold = DateTime.Now - TimeSpan.FromSeconds(5);
    while (_eventIndex < _stateUpdater.Events.Count &&
           _stateUpdater.Events[_eventIndex].time < threshold) {
      _eventIndex++;
    }
    if (_isDirty) {
      var state = _stateUpdater.State;
      var events = _stateUpdater.Events.Skip(_eventIndex);
      _progressLabel.Text = string.Join(
          "\n",
          _stateUpdater.Events.Skip(_eventIndex)
              .Select(evt => {
                switch (evt.type) {
                case BonelabStateUpdater.CompletionEventType.CAPSULE:
                  return $"Unlocked capsule: {evt.name}";
                case BonelabStateUpdater.CompletionEventType.ACHIEVEMENT:
                  return $"Unlocked achievement: {evt.name}";
                default:
                  return evt.name;
                }
              })
              .Concat(new[] {
                $"Unlocks: {state.capsulesUnlocked} / {state.capsulesTotal}",
                $"Achievements: {state.achievementsUnlocked} / {state.achievementsTotal}",
                $"Progress: {(state.percentageComplete * 100f):N1}% / {(state.percentageTotal * 100f):N1}%",
              }));
      invalidator.Invalidate(0, 0, width, height);
    }
  }

  public void Dispose() { _stateUpdater.Dispose(); }
}
}

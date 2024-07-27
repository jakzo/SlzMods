using System;
using System.Linq;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using System.Xml;
using LiveSplit.UI;
using LiveSplit.UI.Components;
using LiveSplit.Model;
using Sst.Common.LiveSplit;
using System.Reflection;

namespace Sst.Livesplit.BoneworksDebugStats {
public class Component : IComponent {
  public const string NAME = "Boneworks Debug Stats";

  public float HorizontalWidth { get => 300; }
  public float VerticalHeight { get => 240; }
  public float MinimumWidth { get => HorizontalWidth; }
  public float MinimumHeight { get => VerticalHeight; }

  public float PaddingTop { get => 0; }
  public float PaddingLeft { get => 0; }
  public float PaddingBottom { get => 0; }
  public float PaddingRight { get => 0; }

  public IDictionary<string, Action> ContextMenuControls { get => null; }

  private SimpleLabel _statsLabel = new SimpleLabel();
  private StateUpdater _stateUpdater = new StateUpdater();
  private bool _isDirty = true;

  public Component(LiveSplitState state) {
    Log.Initialize();
    _stateUpdater.OnReceivedState += receivedState => _isDirty = true;
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
    _statsLabel.HorizontalAlignment = StringAlignment.Near;
    _statsLabel.VerticalAlignment = StringAlignment.Far;
    _statsLabel.X = 4;
    _statsLabel.Y = 0;
    _statsLabel.Width = width;
    _statsLabel.Height = height;
    _statsLabel.Font = state.LayoutSettings.TextFont;
    _statsLabel.Brush = new SolidBrush(state.LayoutSettings.TextColor);
    _statsLabel.HasShadow = state.LayoutSettings.DropShadows;
    _statsLabel.ShadowColor = state.LayoutSettings.ShadowsColor;
    _statsLabel.OutlineColor = state.LayoutSettings.TextOutlineColor;
    _statsLabel.Draw(g);
  }

  public string ComponentName { get => NAME; }

  public Control GetSettingsControl(LayoutMode mode) => null;
  public XmlNode GetSettings(XmlDocument document
  ) => document.CreateElement("Settings");
  public void SetSettings(XmlNode settings) {}

  public void Dispose() { _stateUpdater.Dispose(); }

  public void Update(
      IInvalidator invalidator, LiveSplitState livesplitState, float width,
      float height, LayoutMode mode
  ) {
    if (!_isDirty)
      return;

    _isDirty = false;
    var state = _stateUpdater.State;
    _statsLabel.Text = PrintFields(state);
    // _statsLabel.Text = string.Join("\n", new[] {
    //   $"FPS: {state.fps}",
    //   $"Dropped frames: {state.droppedFrames}",
    //   $"numFramesMagNotTouching: {state.numFramesMagNotTouching}",
    // });
    invalidator.Invalidate(0, 0, width, height);
  }

  public static string PrintFields(object obj) => string.Join(
      "\n",
      obj.GetType()
          .GetFields(
              BindingFlags.Public | BindingFlags.NonPublic |
              BindingFlags.Instance
          )
          .Select(field => $"{field.Name}: {field.GetValue(obj)}")
  );
}
}

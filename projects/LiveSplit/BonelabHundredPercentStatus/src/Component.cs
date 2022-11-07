using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using LiveSplit.UI;
using LiveSplit.UI.Components;
using LiveSplit.Model;

namespace Sst.BonelabHundredPercentStatus {
public class Component : IComponent {
  public float HorizontalWidth = 300;
  public float VerticalHeight = 200;

  protected SimpleLabel ProgressLabel = new SimpleLabel();

  public Component(LiveSplitState state) {}

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
    ProgressLabel.HorizontalAlignment = StringAlignment.Near;
    ProgressLabel.VerticalAlignment = StringAlignment.Far;
    ProgressLabel.X = 5;
    ProgressLabel.Y = 0;
    ProgressLabel.Width = width;
    ProgressLabel.Height = height;
    ProgressLabel.Font = state.LayoutSettings.TextFont;
    ProgressLabel.Brush = new SolidBrush(state.LayoutSettings.TextColor);
    ProgressLabel.HasShadow = state.LayoutSettings.DropShadows;
    ProgressLabel.ShadowColor = state.LayoutSettings.ShadowsColor;
    ProgressLabel.OutlineColor = state.LayoutSettings.TextOutlineColor;
    ProgressLabel.Draw(g);
  }
}
}

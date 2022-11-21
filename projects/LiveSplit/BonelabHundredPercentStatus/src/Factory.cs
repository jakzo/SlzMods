using LiveSplit.Model;
using System;

namespace LiveSplit.UI.Components {
public class Factory : IComponentFactory {
  public string ComponentName =>
      Sst.Livesplit.BonelabHundredPercentStatus.Component.NAME;

  public string Description => Sst.BuildInfo.DESCRIPTION;

  public ComponentCategory Category => ComponentCategory.Information;

  public IComponent Create(LiveSplitState state) =>
      new Sst.Livesplit.BonelabHundredPercentStatus.Component(state);

  public string UpdateName => ComponentName;

  public string UpdateURL =>
      "https://raw.githubusercontent.com/jakzo/SlzSpeedrunTools/main/projects/LiveSplit/BonelabHundredPercentStatus";

  public string XMLURL =>
      $"{UpdateURL}/update.LiveSplit.BonelabHundredPercentStatus.xml";

  public Version Version =>
      Version.Parse(Sst.Livesplit.BonelabHundredPercentStatus.AppVersion.Value);
}
}

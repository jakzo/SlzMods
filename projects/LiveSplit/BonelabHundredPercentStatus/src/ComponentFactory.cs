using LiveSplit.Model;
using System;

namespace LiveSplit.UI.Components {
public class ComponentFactory : IComponentFactory {
  public string ComponentName => "BonelabHundredPercentStatus";

  public string Description =>
      Sst.BonelabHundredPercentStatus.BuildInfo.DESCRIPTION;

  public ComponentCategory Category => ComponentCategory.Other;

  public IComponent Create(LiveSplitState state) =>
      new Sst.BonelabHundredPercentStatus.Component(state);

  public string UpdateName => ComponentName;

  public string UpdateURL =>
      "https://raw.githubusercontent.com/jakzo/SlzSpeedrunTools/master/projects/LiveSplit/BonelabHundredPercentStatus";

  public string XMLURL =>
      UpdateURL + "/update.LiveSplit.BonelabHundredPercentStatus.xml";

  public Version Version =>
      Version.Parse(Sst.BonelabHundredPercentStatus.AppVersion.Value);
}
}

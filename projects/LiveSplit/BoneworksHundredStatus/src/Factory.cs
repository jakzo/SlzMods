using LiveSplit.Model;
using System;

namespace LiveSplit.UI.Components {
public class Factory : IComponentFactory {
  public string ComponentName =>
      Sst.Livesplit.BoneworksHundredStatus.Component.NAME;

  public string Description => Sst.BuildInfo.DESCRIPTION;

  public ComponentCategory Category => ComponentCategory.Other;

  public IComponent Create(LiveSplitState state
  ) => new Sst.Livesplit.BoneworksHundredStatus.Component(state);

  public string UpdateName => ComponentName;

  public string UpdateURL =>
      "https://raw.githubusercontent.com/jakzo/SlzMods/main/projects/" +
      "LiveSplit/BoneworksHundredStatus/";

  public string XMLURL =>
      UpdateURL + "update.LiveSplit.BoneworksHundredStatus.xml";

  public Version Version =>
      Version.Parse(Sst.Livesplit.BoneworksHundredStatus.AppVersion.Value);
}
}

using MelonLoader;

namespace Sst {
public class Mod : MelonMod {
  public override void OnInitializeMelon() { Dbg.Init(BuildInfo.NAME); }
}
}

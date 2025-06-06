using MelonLoader;
using System.Linq;
using System.Collections.Generic;

namespace Sst.Speedruns {
class AntiCheat {
  private static HashSet<string> ALLOWED_MODS = new HashSet<string>() {
    "LootDropBugfix",
    "PhysicsTickFixer",
  };
  private static HashSet<string> ALLOWED_PLUGINS = new HashSet<string>() {
    "Backwards Compatibility Plugin",
  };

  public enum RunIllegitimacyReason {
    DISALLOWED_MODS,
    DISALLOWED_PLUGINS,
  }

  public static Dictionary<RunIllegitimacyReason, string>
  ComputeRunLegitimacy() {
    var illegitimacyReasons = new Dictionary<RunIllegitimacyReason, string>();

#if !DEBUG
    var disallowedMods = MelonHandler.Mods.Where(
        mod => !(mod is Mod) && !ALLOWED_MODS.Contains(mod.Info.Name)
    );
    if (disallowedMods.Count() > 0) {
      var disallowedModNames =
          string.Join(", ", disallowedMods.Select(mod => mod.Info.Name));
      illegitimacyReasons[RunIllegitimacyReason.DISALLOWED_MODS] =
          $"Disallowed mods are active: {disallowedModNames}";
    }

    var disallowedPlugins = MelonHandler.Plugins.Where(
        plugin => !ALLOWED_PLUGINS.Contains(plugin.Info.Name)
    );
    if (disallowedPlugins.Count() > 0) {
      var disallowedPluginNames =
          disallowedPlugins.Select(mod => mod.Info.Name);
      illegitimacyReasons[RunIllegitimacyReason.DISALLOWED_PLUGINS] =
          $"Disallowed plugins are active: {string.Join(", ", disallowedPluginNames)}";
    }
#endif

    return illegitimacyReasons;
  }
}
}

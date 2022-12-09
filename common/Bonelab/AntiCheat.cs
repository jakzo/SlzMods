using System.Linq;
using MelonLoader;
using System.Collections.Generic;

namespace Sst.Utilities {
class AntiCheat {
  private static HashSet<string> ALLOWED_MODS = new HashSet<string>() {
    "LoadMirror",
    "ProgressFix",
    "LootDropBugfix",
    "MelonPreferencesManager",
  };
  private static HashSet<string> ALLOWED_PLUGINS = new HashSet<string>() {
    "Backwards Compatibility Plugin",
  };

  public enum RunIllegitimacyReason {
    DISALLOWED_MODS,
    DISALLOWED_PLUGINS,
  }

  private static bool _hasPrintedIllegitimacyReasons = false;

  public static Dictionary<RunIllegitimacyReason, string>
  ComputeRunLegitimacy<Mod>() {
#if DEBUG
    return new Dictionary<RunIllegitimacyReason, string>();
#else
    return ComputeRunLegitimacyInternal<Mod>();
#endif
  }

  public static bool CheckRunLegitimacy<Mod>() {
    var illegitimacyReasons = ComputeRunLegitimacy<Mod>();
    if (illegitimacyReasons.Count == 0) {
      _hasPrintedIllegitimacyReasons = false;
      return true;
    }

    if (!_hasPrintedIllegitimacyReasons) {
      _hasPrintedIllegitimacyReasons = true;
      var reasonMessages = string.Join(
          "", illegitimacyReasons.Select(reason => $"\n» {reason.Value}"));
      MelonLogger.Msg(
          $"Cannot show timer due to run being illegitimate because:{reasonMessages}");
    }
    return false;
  }

  private static Dictionary<RunIllegitimacyReason, string>
  ComputeRunLegitimacyInternal<Mod>() {
    var illegitimacyReasons = new Dictionary<RunIllegitimacyReason, string>();

    var disallowedMods = MelonMod.RegisteredMelons.Where(
        mod => !(mod is Mod) && !ALLOWED_MODS.Contains(mod.Info.Name));
    if (disallowedMods.Count() > 0) {
      var disallowedModNames =
          string.Join(", ", disallowedMods.Select(mod => mod.Info.Name));
      illegitimacyReasons[RunIllegitimacyReason.DISALLOWED_MODS] =
          $"Disallowed mods are active: {disallowedModNames}";
    }

    var disallowedPlugins = MelonPlugin.RegisteredMelons.Where(
        plugin => !ALLOWED_PLUGINS.Contains(plugin.Info.Name));
    if (disallowedPlugins.Count() > 0) {
      var disallowedPluginNames =
          disallowedPlugins.Select(mod => mod.Info.Name);
      illegitimacyReasons[RunIllegitimacyReason.DISALLOWED_PLUGINS] =
          $"Disallowed plugins are active: {string.Join(", ", disallowedPluginNames)}";
    }

    return illegitimacyReasons;
  }
}
}

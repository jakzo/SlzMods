using System;
using System.Drawing;
using Sst.Common.Boneworks;

namespace Sst.Livesplit.BoneworksHundredStatus {
static class RngDisplay {
  public static string TriesCount(HundredPercentState.RngState item) =>
      $"{item.attempts} {(item.attempts == 1 ? "try" : "tries")}";

  public static string TriesSuffix(HundredPercentState.RngState item) =>
      $" @ {(item.prevAttemptChance * 100f):N0}% per try = {Math.Floor((1f - item.probabilityNotDroppedYet) * 100f):N0}% total";

  public static string ResetDetails(HundredPercentState.RngState item) {
    return ": " + ResetCount(item) + ResetSuffix(item);
  }

  public static string ResetCount(HundredPercentState.RngState item) =>
      $"{item.resets} {(item.resets == 1 ? "reset" : "resets")}";

  public static string ResetSuffix(HundredPercentState.RngState item) {
    var tries = item.attempts == 1 ? "try" : "tries";
    var chance = (item.prevAttemptChance * 100f).ToString("N0");
    var total = Math.Floor((1f - item.probabilityNotDroppedYet) * 100f).ToString("N0");
    var attempts = item.hasDropped
        ? $"{item.attempts} {tries} = {total}% total"
        : $"{item.attempts} x {chance}% per try";
    return $" ({attempts})";
  }

  public static Color NameColor(HundredPercentState.RngState item) {
    return !item.hasDropped && item.resets == 0 ? Color.White : ResetColor(item);
  }
  private static Color ResetColor(HundredPercentState.RngState item) {
    if (item.resets == 0)
      return Color.Gold;
    var fraction = Math.Min(1f, Math.Max(0f, (item.resets - 1) / 9f));
    return Color.FromArgb(
        (int)Math.Round(255 * fraction),
        (int)Math.Round(255 * (1f - fraction)), 0
    );
  }
}
}

using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using System.Xml;
using LiveSplit.Model;
using LiveSplit.UI;
using Sst.Common.Boneworks;
using Sst.Features;
using Sst.Livesplit.BoneworksHundredStatus;

static class Program {
  private static int _checks;
  private static void Check(bool condition, string message) {
    _checks++;
    if (!condition) throw new Exception(message);
  }

  [STAThread]
  private static void Main() {
    var state = new HundredPercentState();
    var baseball = state.rngUnlocks.Single(p => p.Value.name == "Baseball");
    var golf = state.rngUnlocks.Single(p => p.Value.name == "Golf Club");
    var baton = state.rngUnlocks.Single(p => p.Value.name == "Baton");
    var tracker = new RngResetTracker();
    Action<int> enter = scene => tracker.OnSceneInitialized(scene);
    Action<string, HundredPercentState.RngState> attempt =
        (uuid, rngItem) => tracker.OnAttempt(uuid, rngItem, false);
    Action<bool> leave = menu => tracker.OnLoadRequested(menu, state);

    enter(5);
    attempt(baseball.Key, baseball.Value);
    Check(baseball.Value.resets == 0, "A box break alone must not count a reset");
    leave(false);
    Check(baseball.Value.resets == 0, "Finishing Streets must not count");
    enter(5);
    attempt(baseball.Key, baseball.Value);
    attempt(baseball.Key, baseball.Value);
    Check(tracker.OnLoadRequested(true, state), "Menu exit must report changed counts");
    Check(baseball.Value.resets == 1, "Multiple boxes then menu exit count once");
    Check(golf.Value.resets == 0, "Unattempted Golf Club must not count");
    Check(!tracker.OnLoadRequested(true, state), "Repeated load callbacks must not count twice");
    attempt(baseball.Key, baseball.Value);
    leave(true);
    Check(baseball.Value.resets == 1, "Rolls while loading must not arm a reset");
    enter(5);
    leave(true);
    Check(baseball.Value.resets == 1, "Menu exit with no attempts must not count");
    enter(5);
    attempt(baseball.Key, baseball.Value);
    attempt(golf.Key, golf.Value);
    attempt(baton.Key, baton.Value);
    leave(true);
    Check(baseball.Value.resets == 2 && golf.Value.resets == 1,
          "Both attempted Streets items count separately");
    Check(baton.Value.resets == 0, "Baton cannot count in Streets");
    enter(6);
    attempt(baton.Key, baton.Value);
    leave(true);
    Check(baton.Value.resets == 1, "Attempted Baton counts on a Runoff menu exit");
    enter(6);
    attempt(baton.Key, baton.Value);
    leave(false);
    enter(6);
    leave(true);
    Check(baton.Value.resets == 1, "Death/reload must discard the previous attempts");
    enter(5);
    attempt(baseball.Key, baseball.Value);
    tracker.OnLoadingScreen();
    enter(1);
    leave(true);
    Check(baseball.Value.resets == 2, "Other loading paths must discard attempts");
    enter(5);
    attempt(baseball.Key, baseball.Value);
    enter(6);
    leave(true);
    Check(baseball.Value.resets == 2, "Direct scene initialization must discard attempts");
    enter(5);
    tracker.OnAttempt(baseball.Key, baseball.Value, true);
    attempt(golf.Key, golf.Value);
    leave(true);
    Check(baseball.Value.resets == 2 && golf.Value.resets == 2,
          "Already reclaimed items are excluded individually");
    baseball.Value.hasDropped = true;
    enter(5);
    attempt(baseball.Key, baseball.Value);
    leave(true);
    Check(baseball.Value.resets == 2, "Already achieved items must not arm another reset");
    baseball.Value.hasDropped = false;
    enter(5);
    attempt(baseball.Key, baseball.Value);
    baseball.Value.hasDropped = true;
    leave(true);
    Check(baseball.Value.resets == 2,
          "Successful roll then menu exit must preserve the previous reset count");
    enter(5);
    baseball.Value.hasDropped = false;
    attempt(baseball.Key, baseball.Value);
    attempt(golf.Key, golf.Value);
    baseball.Value.hasDropped = true;
    leave(true);
    Check(baseball.Value.resets == 2 && golf.Value.resets == 3,
          "Menu exit counts only the unsuccessful Streets item");
    var firstTryState = new HundredPercentState();
    var firstTryBaton = firstTryState.rngUnlocks[baton.Key];
    enter(6);
    attempt(baton.Key, firstTryBaton);
    firstTryBaton.hasDropped = true;
    Check(!tracker.OnLoadRequested(true, firstTryState) && firstTryBaton.resets == 0,
          "First-try Baton then quitting Runoff remains zero resets");
    enter(6);
    attempt(baton.Key, baton.Value);
    tracker.Clear();
    leave(true);
    Check(baton.Value.resets == 1, "Run reset/disable must clear pending attempts");
    var item = baseball.Value;
    item.hasDropped = false;
    item.resets = 5;
    item.attempts = 7;
    item.probabilityNotDroppedYet = (float)Math.Pow(0.9, 7);
    Check(item.name + RngDisplay.ResetDetails(item) ==
          "Baseball: 5 resets (7 x 10% per try)", "Pending text");
    Check(RngDisplay.NameColor(item) != Color.White, "Pending item with resets has a colored name");
    var pendingColor = RngDisplay.NameColor(item);

    item.hasDropped = true;

    Check(RngDisplay.NameColor(item) == pendingColor, "Name color at nonzero resets is unchanged by collection");
    Check(item.name + RngDisplay.ResetDetails(item) ==
          "Baseball: 5 resets (7 tries = 52% total)", "Completed text and probability");
    var fractionalTotal = new HundredPercentState.RngState {
      name = "Baseball", attempts = 2, resets = 1, hasDropped = true,
      prevAttemptChance = 0.1f, probabilityNotDroppedYet = 0.804f,
    };
    Check(RngDisplay.ResetDetails(fractionalTotal) == ": 1 reset (2 tries = 19% total)",
          "Reset display floors a 19.6% total");
    Check(RngDisplay.TriesSuffix(fractionalTotal) == " @ 10% per try = 19% total",
          "Tries display floors the total and preserves per-try chance");
    item.resets = 0;
    Check(RngDisplay.NameColor(item) == Color.Gold, "Zero resets is gold");
    Check(RngDisplay.ResetCount(item) == "0 resets",
          "Zero reset count is shown in normal color after success");
    Check(item.name + RngDisplay.ResetDetails(item) == "Baseball: 0 resets (7 tries = 52% total)",
          "Zero resets show the count and reset label after success");
    item.hasDropped = false;
    Check(item.name + RngDisplay.ResetDetails(item) == "Baseball: 0 resets (7 x 10% per try)",
          "Zero resets show the count and reset label before success");
    Check(RngDisplay.NameColor(item) == Color.White, "Pending zero-reset name is white");
    item.hasDropped = true;
    item.resets = 1;
    Check(RngDisplay.ResetCount(item) == "1 reset", "Colored count includes singular reset label");
    Check(RngDisplay.NameColor(item).ToArgb() == Color.Lime.ToArgb(), "One reset is green");
    var previous = RngDisplay.NameColor(item);
    for (var resets = 2; resets <= 10; resets++) {
      item.resets = resets;
      var next = RngDisplay.NameColor(item);
      Check(next.R > previous.R && next.G < previous.G, "Continuous green-to-red fade");
      previous = next;
    }
    Check(previous.ToArgb() == Color.Red.ToArgb(), "Ten resets is red");
    item.resets = 20;
    Check(RngDisplay.ResetCount(item) == "20 resets", "Colored count includes plural resets label");
    Check(RngDisplay.NameColor(item).ToArgb() == Color.Red.ToArgb(), "Color caps at red");
    foreach (var resets in new[] { 0, 1, 5, 10, 20 }) {
      item.resets = resets;
      item.hasDropped = false;
      var before = RngDisplay.NameColor(item);
      item.hasDropped = true;
      var after = RngDisplay.NameColor(item);
      Check(resets == 0 ? before == Color.White && after == Color.Gold : before == after,
            "Only zero-reset names change color on collection");
    }
    var serializer = new JavaScriptSerializer();
    var restored = serializer.Deserialize<HundredPercentState>(serializer.Serialize(state));
    Check(restored.rngUnlocks[baseball.Key].resets == 20, "Reset count survives IPC JSON");
    var oldItem = serializer.Deserialize<HundredPercentState.RngState>("{\"attempts\":7}");
    Check(oldItem.resets == 0, "Old messages default resets to zero");

    CheckOverallLuck();

    Environment.SetEnvironmentVariable("SST_LIVESPLIT_LOG_PATH",
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "component.log"));
    using (var component = new Component(null)) {
      var document = new XmlDocument();
      var defaults = component.GetSettings(document);
      Check(defaults["ShowResets"].InnerText == "True", "Reset display is enabled by default");
      using (var settings = component.GetSettingsControl(LayoutMode.Vertical)) {
        ((CheckBox)settings.Controls[0]).Checked = false;
        var disabled = component.GetSettings(document);
        component.SetSettings(disabled);
        Check(component.GetSettings(document)["ShowResets"].InnerText == "False",
              "Explicitly disabled reset display survives a settings round-trip");
        ((CheckBox)settings.Controls[0]).Checked = true;
      }
      var saved = component.GetSettings(document);
      Check(saved["ShowResets"].InnerText == "True", "Checkbox saves to XML");
      Check(saved["ShowMissingCollectibles"] != null, "Existing setting is preserved");
      component.SetSettings(defaults);
      component.SetSettings(saved);
      Check(component.GetSettings(document)["ShowResets"].InnerText == "True", "Setting round-trip");

      var updater = typeof(Component).GetField("_stateUpdater", BindingFlags.Instance | BindingFlags.NonPublic)
          .GetValue(component);
      updater.GetType().GetField("State").SetValue(updater, state);
      item.resets = 0;
      golf.Value.hasDropped = true;
      golf.Value.resets = 1;
      golf.Value.attempts = 12;
      golf.Value.probabilityNotDroppedYet = (float)Math.Pow(0.98, 12);
      baton.Value.hasDropped = true;
      baton.Value.resets = 10;
      baton.Value.attempts = 7;
      baton.Value.probabilityNotDroppedYet = (float)Math.Pow(0.9, 7);
      var liveState = new LiveSplitState(null, null, null,
          new LiveSplit.Options.LayoutSettings(), null);
      liveState.LayoutSettings.TextFont = new Font("Arial", 12);
      liveState.LayoutSettings.TextColor = Color.White;
      component.Update(null, liveState, 620, 240, LayoutMode.Vertical);
      using (var bitmap = new Bitmap(620, 480))
      using (var graphics = Graphics.FromImage(bitmap))
      using (var clip = new Region(new Rectangle(0, 0, 620, 480))) {
        graphics.Clear(Color.Black);
        component.DrawVertical(graphics, liveState, 620, clip);
        item.hasDropped = false;
        item.resets = 5;
        golf.Value.resets = 5;
        component.SetSettings(saved);
        component.Update(null, liveState, 300, 240, LayoutMode.Horizontal);
        graphics.TranslateTransform(0, 240);
        liveState.LayoutSettings.TextOutlineColor = Color.DarkGray;
        component.DrawHorizontal(graphics, liveState, 240, clip);
        var outlinedGreenPixels = 0;
        var outlinedRedPixels = 0;
        for (var y = 240; y < bitmap.Height; y++) {
          for (var x = 0; x < 100; x++) {
            var pixel = bitmap.GetPixel(x, y);
            if (pixel.G > pixel.R + 10 && pixel.G > pixel.B + 30)
              outlinedGreenPixels++;
            if (pixel.R > pixel.G + 50 && pixel.R > pixel.B + 50)
              outlinedRedPixels++;
          }
        }
        Check(outlinedGreenPixels > 10, "Outlined Golf Club name must render");
        Check(outlinedRedPixels > 10, "Outlined Baton name must render");
        bitmap.Save(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "reset-display.png"));
      }
      // Inspect only the icon column, with a non-white layout color so a
      // hard-coded white cross or name-colored tick cannot pass unnoticed.
      foreach (var showResets in new[] { true, false }) {
        var iconSettings = saved.CloneNode(true);
        iconSettings["ShowResets"].InnerText = showResets.ToString();
        component.SetSettings(iconSettings);
        liveState.LayoutSettings.TextColor = Color.Cyan;
        component.Update(null, liveState, 620, 240, LayoutMode.Vertical);
        using (var bitmap = new Bitmap(620, 240))
        using (var graphics = Graphics.FromImage(bitmap))
        using (var clip = new Region(new Rectangle(0, 0, 620, 240))) {
          graphics.Clear(Color.Black);
          component.DrawVertical(graphics, liveState, 620, clip);
          var crossPixels = 0;
          var tickPixels = 0;
          for (var y = 0; y < bitmap.Height; y++)
            for (var x = 4; x < 18; x++) {
              var pixel = bitmap.GetPixel(x, y);
              if (pixel.R > 180 && pixel.G > 180 && pixel.B > 180) crossPixels++;
              if (pixel.R < 40 && pixel.G > 180 && pixel.B < 40) tickPixels++;
            }
          Check(crossPixels > 10, "Cross stays white in each display mode even with cyan layout text");
          Check(tickPixels > 10, "Tick is green regardless of achieved name color in each display mode");
        }
      }
      RenderStates(component, updater, saved, liveState);
      updater.GetType().GetField("State").SetValue(updater, null);
      component.SetSettings(saved);
      component.Update(null, liveState, 300, 240, LayoutMode.Vertical);
      using (var bitmap = new Bitmap(300, 240))
      using (var graphics = Graphics.FromImage(bitmap))
      using (var clip = new Region(new Rectangle(0, 0, 300, 240))) {
        component.DrawVertical(graphics, liveState, 300, clip);
      }
      var oldSettings = document.CreateElement("Settings");
      component.SetSettings(oldSettings);
      Check(component.GetSettings(document)["ShowResets"].InnerText == "True",
            "Layouts without ShowResets default to reset display");
      liveState.LayoutSettings.TextFont.Dispose();
    }
    Console.WriteLine($"Passed {_checks} checks.");
  }

  private static void RenderStates(Component component, object updater, XmlNode settings, LiveSplitState liveState) {
    var scenarios = new[] {
      ("No drops yet / zero resets", new[] { 0, 0, 0 }, new[] { false, false, false }, new[] { 0, 0, 0 }),
      ("Collected with zero resets", new[] { 0, 0, 0 }, new[] { true, true, true }, new[] { 3, 2, 1 }),
      ("Still trying / 1, 5 and 10 resets", new[] { 1, 5, 10 }, new[] { false, false, false }, new[] { 2, 12, 15 }),
      ("Collected / 1, 5 and 10 resets", new[] { 1, 5, 10 }, new[] { true, true, true }, new[] { 3, 13, 16 }),
      ("Mixed progress", new[] { 0, 3, 0 }, new[] { true, false, false }, new[] { 1, 8, 2 }),
      ("Red stays capped at 10+ resets", new[] { 10, 15, 25 }, new[] { false, true, true }, new[] { 15, 30, 40 }),
    };
    using (var bitmap = new Bitmap(1000, 750))
    using (var graphics = Graphics.FromImage(bitmap))
    using (var heading = new Font("Arial", 13, FontStyle.Bold)) {
      graphics.Clear(Color.FromArgb(24, 24, 24));
      liveState.LayoutSettings.TextColor = Color.White;
      liveState.LayoutSettings.TextOutlineColor = Color.Transparent;
      for (var i = 0; i < scenarios.Length; i++) {
        var scenario = scenarios[i];
        var sample = new HundredPercentState();
        var j = 0;
        foreach (var rngItem in sample.rngUnlocks.Values) {
          rngItem.resets = scenario.Item2[j];
          rngItem.hasDropped = scenario.Item3[j];
          rngItem.attempts = scenario.Item4[j];
          rngItem.probabilityNotDroppedYet = (float)Math.Pow(1 - (double)rngItem.prevAttemptChance, rngItem.attempts);
          j++;
        }
        sample.unlockLevelCount = 4;
        sample.unlockLevelMax = 8;
        sample.ammoLevelCount = 12;
        sample.ammoLevelMax = 20;
        updater.GetType().GetField("State").SetValue(updater, sample);
        component.SetSettings(settings);
        component.Update(null, liveState, 470, 240, LayoutMode.Vertical);
        var savedGraphics = graphics.Save();
        graphics.TranslateTransform(i % 2 * 500 + 12, i / 2 * 250);
        graphics.DrawString(scenario.Item1, heading, Brushes.White, 0, 15);
        using (var clip = new Region(new Rectangle(0, 40, 470, 200))) {
          graphics.SetClip(clip, System.Drawing.Drawing2D.CombineMode.Replace);
          component.DrawVertical(graphics, liveState, 470, clip);
        }
        graphics.Restore(savedGraphics);
      }
      bitmap.Save(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "component-states.png"));
    }
  }

  private static void CheckOverallLuck() {
    var lucky = OverallRngLuck.Percentile(3, 2, 1);
    Check(Math.Abs(lucky - 0.003612764432) < 1e-11,
          "Lucky example matches directly enumerated outcome probabilities, including ties");
    Check(Math.Abs(OverallRngLuck.Percentile(1, 1, 1) - 0.0001) < 1e-12,
          "First try on all items receives half its 0.02% probability mass");
    Check(OverallRngLuck.Percentile(1, 2, 3) == lucky,
          "Swapping Baseball and Baton preserves ties exactly");
    Check(OverallRngLuck.Percentile(0, 1, 1) == 0, "Incomplete attempts do not receive a completed-run score");
    Check(OverallRngLuck.Percentile(int.MaxValue, int.MaxValue, int.MaxValue) == 1,
          "Extreme unlucky outcome remains bounded at 100%");
    Check(OverallRngLuck.PercentileForScore(0) == 0, "Lower endpoint is zero");
    Check(OverallRngLuck.PercentileForScore(1) == 1, "Upper endpoint is 100%");
    Check(OverallRngLuck.PercentileForScore(double.NaN) == 0, "Invalid score cannot propagate NaN");

    // An independent triple sum checks the analytic Golf Club sum in a region
    // where all outcomes with score <= the threshold have finite count bounds.
    foreach (var counts in new[] { new[] { 1, 1, 1 }, new[] { 3, 2, 1 },
                                  new[] { 1, 3, 1 }, new[] { 2, 2, 2 } }) {
      var score = (1 - Math.Pow(0.9, counts[0])) * (1 - Math.Pow(0.98, counts[1])) *
                  (1 - Math.Pow(0.9, counts[2]));
      var expected = 0.0;
      for (var a = 1; a <= 100; a++)
        for (var b = 1; b <= 100; b++)
          for (var c = 1; c <= 100; c++) {
            var other = (1 - Math.Pow(0.9, a)) * (1 - Math.Pow(0.98, b)) * (1 - Math.Pow(0.9, c));
            var weight = 0.1 * Math.Pow(0.9, a - 1) * 0.02 * Math.Pow(0.98, b - 1) *
                         0.1 * Math.Pow(0.9, c - 1);
            if (Math.Abs(other - score) <= score * 1e-12) expected += weight * 0.5;
            else if (other < score) expected += weight;
          }
      Check(Math.Abs(OverallRngLuck.Percentile(counts[0], counts[1], counts[2]) - expected) < 1e-11,
            "Analytic conditional sum agrees with independent triple enumeration");
    }

    var previous = -1.0;
    for (var i = 0; i <= 500; i++) {
      var percentile = OverallRngLuck.PercentileForScore(i / 500.0);
      if (double.IsNaN(percentile) || percentile < previous || percentile < 0 || percentile > 1)
        throw new Exception("Overall luck must be monotonic and bounded over the full score range");
      previous = percentile;
    }
    Check(true, "Overall luck is monotonic and bounded across 501 scores");

    var random = new Random(12345);
    var samples = new double[5000];
    var failureChances = new[] { 0.9, 0.98, 0.9 };
    var attempts = new int[3];
    var timer = System.Diagnostics.Stopwatch.StartNew();
    for (var sample = 0; sample < samples.Length; sample++) {
      for (var item = 0; item < attempts.Length; item++)
        attempts[item] = (int)Math.Ceiling(Math.Log(1.0 - random.NextDouble()) / Math.Log(failureChances[item]));
      samples[sample] = OverallRngLuck.Percentile(attempts[0], attempts[1], attempts[2]);
    }
    timer.Stop();
    Array.Sort(samples);
    var mean = samples.Average();
    var median = samples[samples.Length / 2];
    Check(Math.Abs(mean - 0.5) < 0.02, "Simulated mean luck stays near 50%");
    Check(Math.Abs(median - 0.5) < 0.025, "Simulated median luck stays near 50%");
    for (var decile = 1; decile < 10; decile++)
      Check(Math.Abs(samples[decile * samples.Length / 10] - decile / 10.0) < 0.025,
            "Simulated luck percentiles remain calibrated across every decile");
    Console.WriteLine($"Deterministic luck example: {lucky:P6}; simulated mean: {mean:P2}; median: {median:P2}; average calculation: {timer.Elapsed.TotalMilliseconds / samples.Length:N2} ms.");
  }
}

using System;

namespace Sst.Livesplit.BoneworksHundredStatus {
static class OverallRngLuck {
  // Standard independent drop chances: Baseball/Baton 10%, Golf Club 2%.
  // Rank the product of their cumulative drop probabilities. Taking its cube
  // root (the previous geometric mean) gives exactly the same ordering.
  // Percentile = P(other score < score) + 0.5 * P(other score == score).
  // The midpoint treatment of ties gives an expected percentile of 0.5.
  private const double TailLimit = 1e-12;
  private const double TieTolerance = 1e-12;
  private static readonly double GolfLogFailure = Math.Log(0.98);
  private static readonly int MaxTenPercentAttempts =
      (int)Math.Ceiling(Math.Log(TailLimit) / Math.Log(0.9));
  private static readonly double[] TenPercentCdf = new double[MaxTenPercentAttempts];
  private static readonly double[] TenPercentMass = new double[MaxTenPercentAttempts];
  private static readonly object CacheLock = new object();
  private static (int Baseball, int Golf, int Baton) _cachedAttempts;
  private static double _cachedPercentile;

  static OverallRngLuck() {
    for (var i = 0; i < MaxTenPercentAttempts; i++) {
      TenPercentCdf[i] = 1 - Math.Pow(0.9, i + 1);
      TenPercentMass[i] = 0.1 * Math.Pow(0.9, i);
    }
  }

  public static double Percentile(int baseballAttempts, int golfAttempts, int batonAttempts) {
    if (baseballAttempts < 1 || golfAttempts < 1 || batonAttempts < 1)
      return 0;
    // Canonical order preserves ties when Baseball and Baton counts swap.
    var attempts = (Math.Min(baseballAttempts, batonAttempts), golfAttempts,
                    Math.Max(baseballAttempts, batonAttempts));
    lock (CacheLock) {
      if (attempts == _cachedAttempts)
        return _cachedPercentile;
      var score = (1 - Math.Pow(0.9, attempts.Item1)) *
                  (1 - Math.Pow(0.9, attempts.Item3)) *
                  (1 - Math.Pow(0.98, attempts.Item2));
      _cachedPercentile = PercentileForScore(score);
      _cachedAttempts = attempts;
      return _cachedPercentile;
    }
  }

  internal static double PercentileForScore(double score) {
    if (double.IsNaN(score) || score <= 0)
      return 0;
    if (score >= 1)
      return 1;

    var percentile = 0.0;
    for (var baseball = 0; baseball < MaxTenPercentAttempts; baseball++) {
      for (var baton = baseball; baton < MaxTenPercentAttempts; baton++) {
        var pairScore = TenPercentCdf[baseball] * TenPercentCdf[baton];
        var mass = TenPercentMass[baseball] * TenPercentMass[baton];
        if (baseball != baton)
          mass *= 2;
        percentile += mass * GolfMidrank(score, pairScore);
      }
    }
    // The Golf Club tail is summed analytically. Only the two 10% item tails
    // are truncated; their combined omitted probability is below 2e-12.
    // Normalize the retained mass, with error at that same scale.
    var retained = 1 - Math.Pow(0.9, MaxTenPercentAttempts);
    return Math.Max(0, Math.Min(1, percentile / (retained * retained)));
  }

  private static double GolfMidrank(double score, double pairScore) {
    var threshold = score / pairScore;
    if (threshold >= 1)
      return 1;
    // Invert 1 - 0.98^n <= threshold, then check both neighboring integers
    // so floating-point rounding cannot give a tied outcome full/zero credit.
    var last = Math.Max(0, (int)Math.Floor(Math.Log(1 - threshold) / GolfLogFailure));
    for (var candidate = Math.Max(1, last); candidate <= last + 1; candidate++) {
      var failure = Math.Pow(0.98, candidate);
      var candidateScore = pairScore * (1 - failure);
      if (Math.Abs(candidateScore - score) <= TieTolerance * score) {
        var previousFailure = Math.Pow(0.98, candidate - 1);
        return 1 - previousFailure + 0.5 * 0.02 * previousFailure;
      }
    }
    return 1 - Math.Pow(0.98, last);
  }
}
}

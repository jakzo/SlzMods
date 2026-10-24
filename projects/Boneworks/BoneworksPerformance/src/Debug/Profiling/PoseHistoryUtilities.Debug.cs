using System;
using System.Collections.Generic;
using System.Threading;

namespace Sst.BoneworksPerformance;

internal sealed partial class PoseHistoryBuffer
{
  public PoseHistorySample[] CopyChronological()
  {
    var end = Volatile.Read(ref _nextSequence);
    var start = Math.Max(0, end - _items.Length);
    var result = new List<PoseHistorySample>((int)(end - start));
    for (var sequence = start; sequence < end; sequence++)
    {
      var index = (int)(sequence % _items.Length);
      if (Volatile.Read(ref _publishedSequences[index]) != sequence)
        continue;
      var sample = _items[index];
      Thread.MemoryBarrier();
      if (Volatile.Read(ref _publishedSequences[index]) == sequence &&
          sample.Sequence == sequence)
        result.Add(sample);
    }
    return result.ToArray();
  }
}

internal static partial class PoseHistoryMath
{
  public static PoseMatrix3x4 ToMatrix(TrackedPoseValue pose)
  {
    var x = pose.Qx;
    var y = pose.Qy;
    var z = pose.Qz;
    var w = pose.Qw;
    var xx = x * x;
    var yy = y * y;
    var zz = z * z;
    var xy = x * y;
    var xz = x * z;
    var yz = y * z;
    var wx = w * x;
    var wy = w * y;
    var wz = w * z;
    return new PoseMatrix3x4
    {
      M0 = 1f - 2f * (yy + zz), M1 = 2f * (xy - wz),
      M2 = 2f * (xz + wy), M3 = pose.X,
      M4 = 2f * (xy + wz), M5 = 1f - 2f * (xx + zz),
      M6 = 2f * (yz - wx), M7 = pose.Y,
      M8 = 2f * (xz - wy), M9 = 2f * (yz + wx),
      M10 = 1f - 2f * (xx + yy), M11 = pose.Z,
    };
  }
}

internal static partial class TimedButtonMath
{
  public static bool StateAt(bool initialState, IReadOnlyList<TimedButtonEdge> edges, long timestamp)
  {
    var state = initialState;
    if (edges == null) return state;
    for (var i = 0; i < edges.Count && edges[i].Timestamp <= timestamp; i++)
      state = edges[i].Pressed;
    return state;
  }

  public static int FirstTickAtOrAfter(
      long edgeTimestamp, long[] fixedTickTimestamps
  )
  {
    if (fixedTickTimestamps == null || fixedTickTimestamps.Length == 0)
      return -1;
    var low = 0;
    var high = fixedTickTimestamps.Length - 1;
    var answer = -1;
    while (low <= high)
    {
      var middle = low + (high - low) / 2;
      if (fixedTickTimestamps[middle] >= edgeTimestamp)
      {
        answer = middle;
        high = middle - 1;
      } else
      {
        low = middle + 1;
      }
    }
    return answer;
  }
}

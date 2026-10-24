namespace Sst.BoneworksPerformance;

internal static class SamplerSchedule
{
  public static long Next(long deadline, long completed, long interval) =>
      deadline + interval > completed ? deadline + interval : completed + interval;
}

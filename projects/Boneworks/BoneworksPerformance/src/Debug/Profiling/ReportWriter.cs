#if DEBUG
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using MelonLoader;
using UnityEngine;

namespace Sst.BoneworksPerformance;

internal static class ReportWriter {
  private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

  public static void Write(CaptureResult result) {
    try {
      WriteMetadata(result);
      WriteFrames(result);
      WritePlayerLoopPhases(result);
      WriteMarkers(result);
      WriteMonoGcEvents(result);
      WritePoseHistory(result);
      WriteResampledFixedPoses(result);
      WriteTargetedDiagnostics(result);
      WriteStalls(result);
      WriteCpuSamples(result);
      WriteWaitCorrelations(result);
      WriteCompletionWaits(result);
      WriteGraphics(result);
      WriteSampledPhases(result);
      WriteHtml(result);
      MelonLogger.Msg("Profiler report written: " + result.Directory);
    } catch (Exception exception) {
      MelonLogger.Error("Could not write profiler report: " + exception);
    }
  }

  private static void WriteMetadata(CaptureResult result) {
    var compositorFrames = 0;
    var totalFrameMs = 0.0;
    var totalProcessCpuMs = 0.0;
    for (var i = 0; i < result.FrameCount; i++) {
      totalFrameMs += result.Frames[i].FrameMs;
      totalProcessCpuMs += result.Frames[i].ProcessCpuMs;
      if (result.Frames[i].HasCompositorTiming)
        compositorFrames++;
    }

    var text = new StringBuilder();
    text.AppendLine("mod=" + BuildInfo.NAME);
    text.AppendLine("version=" + AppVersion.Value);
    text.AppendLine("mode=" + result.Mode);
    text.AppendLine("preset=" + result.Options.Preset);
    text.AppendLine("cpuSamplingEnabled=" + result.Options.EnableCpuSampling);
    text.AppendLine(
        "completionWaitsEnabled=" + result.Options.EnableCompletionWaits
    );
    text.AppendLine(
        "graphicsInstrumentationEnabled=" + result.Options.EnableGraphics
    );
    text.AppendLine(
        "unityBinaryProfilerRequested=" +
        result.Options.EnableUnityBinaryProfiler
    );
    text.AppendLine(
        "monoGcEventsEnabled=" + result.Options.EnableMonoGcEvents
    );
    text.AppendLine(
        "targetedDiagnosticsEnabled=" +
        result.Options.EnableTargetedDiagnostics
    );
    text.AppendLine(
        "poseHistoryEnabled=" + result.Options.EnablePoseHistory
    );
    text.AppendLine(
        "poseHistoryStatus=" + result.PoseHistory.Status
    );
    text.AppendLine(
        "poseHistoryRequestedHz=" + result.PoseHistory.RequestedSampleRate
    );
    text.AppendLine(
        "poseHistoryActualHz=" + Format(result.PoseHistory.ActualSampleRate)
    );
    text.AppendLine(
        "poseHistoryInterpolationDelayTicks=" +
        result.PoseHistory.InterpolationDelayTicks
    );
    text.AppendLine(
        "poseHistoryBackgroundSamples=" +
        result.PoseHistory.BackgroundSamples.Length
    );
    text.AppendLine(
        "poseHistoryFixedSamples=" + result.PoseHistory.FixedSamples.Length
    );
    text.AppendLine(
        "poseHistoryLeftDeviceIndex=" + result.PoseHistory.LeftDeviceIndex
    );
    text.AppendLine(
        "poseHistoryRightDeviceIndex=" + result.PoseHistory.RightDeviceIndex
    );
    text.AppendLine(
        "poseHistoryFixedSamplesDropped=" +
        result.PoseHistory.FixedSamplesDropped
    );
    text.AppendLine(
        "poseHistoryQueryErrors=" + result.PoseHistory.QueryErrors
    );
    text.AppendLine(
        "poseHistoryScheduleOverruns=" +
        result.PoseHistory.ScheduleOverruns
    );
    text.AppendLine(
        "poseHistoryAverageQueryUs=" + Format(
            result.PoseHistory.QueryCount == 0
                ? 0.0
                : result.PoseHistory.QueryTicks * 1000000.0 /
                  Stopwatch.Frequency / result.PoseHistory.QueryCount
        )
    );
    text.AppendLine(
        "poseHistoryMaximumQueryUs=" + Format(
            result.PoseHistory.MaximumQueryTicks * 1000000.0 /
            Stopwatch.Frequency
        )
    );
    var exactPoseTicks = 0;
    var interpolatedPoseTicks = 0;
    var beforeHistoryPoseTicks = 0;
    var afterHistoryPoseTicks = 0;
    var unavailablePoseTicks = 0;
    for (var i = 0;
         i < result.PoseHistory.ResampledFixedSamples.Length; i++) {
      switch (result.PoseHistory.ResampledFixedSamples[i].Status) {
        case PoseBracketStatus.Exact:
          exactPoseTicks++;
          break;
        case PoseBracketStatus.Interpolated:
          interpolatedPoseTicks++;
          break;
        case PoseBracketStatus.BeforeHistory:
          beforeHistoryPoseTicks++;
          break;
        case PoseBracketStatus.AfterHistory:
          afterHistoryPoseTicks++;
          break;
        default:
          unavailablePoseTicks++;
          break;
      }
    }
    text.AppendLine("poseResamplingExactTicks=" + exactPoseTicks);
    text.AppendLine(
        "poseResamplingInterpolatedTicks=" + interpolatedPoseTicks
    );
    text.AppendLine(
        "poseResamplingBeforeHistoryTicks=" + beforeHistoryPoseTicks
    );
    text.AppendLine(
        "poseResamplingAfterHistoryTicks=" + afterHistoryPoseTicks
    );
    text.AppendLine(
        "poseResamplingUnavailableTicks=" + unavailablePoseTicks
    );
    text.AppendLine("indicatorEnabled=" + result.Options.ShowIndicator);
    text.AppendLine("utc=" + DateTime.UtcNow.ToString("O", Invariant));
    text.AppendLine("gameVersion=" + Application.version);
    text.AppendLine("unityVersion=" + Application.unityVersion);
    text.AppendLine("frames=" + result.FrameCount);
    text.AppendLine(
        "durationMs=" + Format(result.FrameCount == 0
            ? 0.0
            : result.Frames[result.FrameCount - 1].ElapsedMs)
    );
    text.AppendLine(
        "averageFrameMs=" + Format(
            result.FrameCount == 0 ? 0.0 : totalFrameMs / result.FrameCount
        )
    );
    text.AppendLine(
        "averageProcessCpuMs=" + Format(
            result.FrameCount == 0
                ? 0.0
                : totalProcessCpuMs / result.FrameCount
        )
    );
    text.AppendLine("compositorTimingFrames=" + compositorFrames);
    text.AppendLine("cpuSamples=" + result.CpuSampleCount);
    text.AppendLine("cpuSamplingHz=" + result.CpuSamplesPerSecond);
    text.AppendLine("stackAddresses=" + result.StackAddressCount);
    text.AppendLine("monoGcCallbackInstalled=" + result.MonoGc.CallbackInstalled);
    text.AppendLine("monoGcEvents=" + result.MonoGc.EventCount);
    text.AppendLine("monoGcStatus=" + result.MonoGc.Status);
    text.AppendLine(
        "physBodyUpdateCollidersCalls=" +
        result.TargetedDiagnostics.PhysBodyCalls
    );
    text.AppendLine(
        "physBodyUnchangedColliderCalls=" +
        result.TargetedDiagnostics.PhysBodyUnchangedCalls
    );
    text.AppendLine(
        "physBodyUpdateCollidersMs=" + Format(
            result.TargetedDiagnostics.PhysBodyTicks * 1000.0 /
            Stopwatch.Frequency
        )
    );
    text.AppendLine(
        "capturedExceptions=" + result.TargetedDiagnostics.ExceptionCount
    );
    text.AppendLine(
        "averageSamplerWorkUs=" + Format(result.AverageSamplingMicroseconds)
    );
    text.AppendLine(
        "averageThreadSuspensionUs=" +
        Format(result.AverageSuspensionMicroseconds)
    );
    text.AppendLine(
        "maximumThreadSuspensionUs=" +
        Format(result.MaximumSuspensionMicroseconds)
    );
    text.AppendLine(
        "completionWaitHookInstalled=" +
        result.CompletionWaits.HookInstalled
    );
    text.AppendLine(
        "completionWaits=" + result.CompletionWaits.WaitCount
    );
    var waitSnapshotCount = 0;
    for (var i = 0; i < result.CpuSampleCount; i++) {
      if (result.CpuSamples[i].IsWaitSnapshot)
        waitSnapshotCount++;
    }
    text.AppendLine("completionWaitWorkerSnapshots=" + waitSnapshotCount);
    text.AppendLine(
        "graphicsPendingWorkHookInstalled=" +
        result.Graphics.PendingWorkHookInstalled
    );
    text.AppendLine(
        "graphicsPendingWorkCalls=" + result.Graphics.PendingWorkCount
    );
    text.AppendLine(
        "graphicsPendingWorkTarget=0x" +
        result.Graphics.PendingWorkTargetAddress.ToString("X")
    );
    text.AppendLine(
        "graphicsPendingWorkUnityOffset=0x" +
        Math.Max(
            0, result.Graphics.PendingWorkTargetAddress -
               result.Graphics.UnityPlayerBaseAddress
        ).ToString("X")
    );
    text.AppendLine("unityGraphicsDevice=" + result.Graphics.UnityDeviceName);
    text.AppendLine(
        "unityGraphicsVendor=" + result.Graphics.UnityDeviceVendor
    );
    text.AppendLine(
        "unityGraphicsVendorId=0x" +
        result.Graphics.UnityDeviceVendorId.ToString("X")
    );
    text.AppendLine(
        "unityGraphicsDeviceId=0x" +
        result.Graphics.UnityDeviceId.ToString("X")
    );
    text.AppendLine(
        "steamVrGraphicsAdapterLuid=0x" +
        result.Graphics.SteamVrAdapterLuid.ToString("X16")
    );
    text.AppendLine("dxgiAdapters=" + result.Graphics.Adapters.Length);
    text.AppendLine(
        "gpuEngineCounters=" + result.Graphics.EngineCounters.Length
    );
    text.AppendLine(
        "gpuEngineSampleTicks=" + result.Graphics.EngineSampleTicks
    );
    text.AppendLine(
        "gpuEngineSamples=" + result.Graphics.EngineSampleCount
    );
    text.AppendLine(
        "gpuEngineSamplerStatus=" + result.Graphics.EngineSamplerStatus
    );
    text.AppendLine("il2cppMethodCatalogComplete=" + NativeMethodCatalog.IsComplete);
    text.AppendLine("il2cppMethodAddresses=" + NativeMethodCatalog.SymbolCount);
    text.AppendLine(
        "il2cppDumperMethodAddresses=" + NativeMethodCatalog.DumperSymbolCount
    );
    text.AppendLine(
        "unityPlayerPdbLoaded=" + NativePdbSymbolResolver.UnityPlayerLoaded
    );
    var unityProfilerStatus = result.UnityProfilerPath == null
        ? "disabled"
        : File.Exists(result.UnityProfilerPath)
            ? "unity-profiler.data"
            : "requested-but-not-produced-by-player";
    text.AppendLine("unityBinaryProfiler=" + unityProfilerStatus);
    File.WriteAllText(Path.Combine(result.Directory, "metadata.txt"), text.ToString());
  }

  private static void WriteFrames(CaptureResult result) {
    using (var writer = new StreamWriter(
               Path.Combine(result.Directory, "frames.csv"), false,
               new UTF8Encoding(false), 65536
           )) {
      writer.WriteLine(
          "sample,unity_frame,elapsed_ms,frame_ms,process_cpu_ms," +
          "fixed_updates,fixed_update_ms,physics_fixed_update_ms," +
          "script_fixed_update_ms,simulation_debt_ms,fixed_delta_ms," +
          "mono_heap_capacity_bytes,mono_heap_used_bytes," +
          "total_allocated_bytes,gc_gen0_collections,gc_gen1_collections," +
          "gc_gen2_collections,physbody_update_colliders_calls," +
          "physbody_unchanged_collider_calls," +
          "physbody_update_colliders_ms,exception_count," +
          "profiler_update_ms," +
          "profiler_fixed_update_ms,profiler_gui_ms,profiler_capture_ms," +
          "profiler_gui_heap_delta_bytes,profiler_gui_calls," +
          "profiler_gui_layout_calls,profiler_gui_repaint_calls," +
          "profiler_gui_other_calls," +
          "has_compositor_timing,client_frame_interval_ms," +
          "compositor_cpu_ms,compositor_gpu_ms,compositor_idle_cpu_ms," +
          "present_wait_cpu_ms,total_render_gpu_ms,pre_submit_gpu_ms," +
          "post_submit_gpu_ms,dropped_frames"
      );
      for (var i = 0; i < result.FrameCount; i++) {
        var frame = result.Frames[i];
        writer.Write(i);
        writer.Write(',');
        writer.Write(frame.Frame);
        Write(writer, frame.ElapsedMs);
        Write(writer, frame.FrameMs);
        Write(writer, frame.ProcessCpuMs);
        writer.Write(',');
        writer.Write(frame.FixedUpdates);
        Write(writer, frame.FixedUpdateMs);
        Write(writer, frame.PhysicsFixedUpdateMs);
        Write(writer, frame.ScriptFixedUpdateMs);
        Write(writer, frame.SimulationDebtMs);
        Write(writer, frame.FixedDeltaMs);
        writer.Write(',');
        writer.Write(frame.ManagedHeapCapacityBytes);
        writer.Write(',');
        writer.Write(frame.ManagedHeapBytes);
        writer.Write(',');
        writer.Write(frame.TotalAllocatedBytes);
        writer.Write(',');
        writer.Write(frame.GcGen0Collections);
        writer.Write(',');
        writer.Write(frame.GcGen1Collections);
        writer.Write(',');
        writer.Write(frame.GcGen2Collections);
        writer.Write(',');
        writer.Write(frame.PhysBodyUpdateCollidersCalls);
        writer.Write(',');
        writer.Write(frame.PhysBodyUnchangedColliderCalls);
        Write(writer, frame.PhysBodyUpdateCollidersMs);
        writer.Write(',');
        writer.Write(frame.ExceptionCount);
        Write(writer, frame.ProfilerUpdateMs);
        Write(writer, frame.ProfilerFixedUpdateMs);
        Write(writer, frame.ProfilerGuiMs);
        Write(writer, frame.ProfilerCaptureMs);
        writer.Write(',');
        writer.Write(frame.ProfilerGuiHeapDeltaBytes);
        writer.Write(',');
        writer.Write(frame.ProfilerGuiCalls);
        writer.Write(',');
        writer.Write(frame.ProfilerGuiLayoutCalls);
        writer.Write(',');
        writer.Write(frame.ProfilerGuiRepaintCalls);
        writer.Write(',');
        writer.Write(frame.ProfilerGuiOtherCalls);
        writer.Write(',');
        writer.Write(frame.HasCompositorTiming ? "1" : "0");
        Write(writer, frame.ClientFrameIntervalMs);
        Write(writer, frame.CompositorCpuMs);
        Write(writer, frame.CompositorGpuMs);
        Write(writer, frame.CompositorIdleCpuMs);
        Write(writer, frame.PresentWaitCpuMs);
        Write(writer, frame.TotalRenderGpuMs);
        Write(writer, frame.PreSubmitGpuMs);
        Write(writer, frame.PostSubmitGpuMs);
        writer.Write(',');
        writer.WriteLine(frame.DroppedFrames);
      }
    }
  }

  private static void WritePoseHistory(CaptureResult result) {
    using (var writer = new StreamWriter(
               Path.Combine(result.Directory, "openvr-pose-history.csv"),
               false, new UTF8Encoding(false), 65536
           )) {
      writer.Write("sample,timestamp_us,query_us,head_device_index,");
      writer.Write("left_device_index,right_device_index");
      WritePoseHeader(writer, "head");
      WritePoseHeader(writer, "left");
      WritePoseHeader(writer, "right");
      writer.WriteLine();
      var samples = result.PoseHistory.BackgroundSamples;
      for (var i = 0; i < samples.Length; i++) {
        writer.Write(i);
        Write(writer, TicksToMicroseconds(samples[i].Timestamp));
        Write(writer, TicksToMicroseconds(samples[i].QueryTicks));
        writer.Write(',');
        writer.Write(samples[i].HeadDeviceIndex);
        writer.Write(',');
        writer.Write(samples[i].LeftDeviceIndex);
        writer.Write(',');
        writer.Write(samples[i].RightDeviceIndex);
        WritePose(writer, samples[i].Head);
        WritePose(writer, samples[i].Left);
        WritePose(writer, samples[i].Right);
        writer.WriteLine();
      }
    }

    using (var writer = new StreamWriter(
               Path.Combine(result.Directory, "fixed-tick-poses.csv"),
               false, new UTF8Encoding(false), 65536
           )) {
      writer.Write(
          "sample,unity_frame,fixed_time_s,realtime_s,timestamp_us," +
          "query_us,head_device_index,left_device_index,right_device_index"
      );
      WritePoseHeader(writer, "head");
      WritePoseHeader(writer, "left");
      WritePoseHeader(writer, "right");
      writer.WriteLine();
      var samples = result.PoseHistory.FixedSamples;
      for (var i = 0; i < samples.Length; i++) {
        var observation = samples[i];
        var sample = observation.Sample;
        writer.Write(i);
        writer.Write(',');
        writer.Write(observation.UnityFrame);
        Write(writer, observation.FixedTime);
        Write(writer, observation.RealtimeSinceStartup);
        Write(writer, TicksToMicroseconds(sample.Timestamp));
        Write(writer, TicksToMicroseconds(sample.QueryTicks));
        writer.Write(',');
        writer.Write(sample.HeadDeviceIndex);
        writer.Write(',');
        writer.Write(sample.LeftDeviceIndex);
        writer.Write(',');
        writer.Write(sample.RightDeviceIndex);
        WritePose(writer, sample.Head);
        WritePose(writer, sample.Left);
        WritePose(writer, sample.Right);
        writer.WriteLine();
      }
    }
  }

  private static void WriteResampledFixedPoses(CaptureResult result) {
    using (var writer = new StreamWriter(
               Path.Combine(
                   result.Directory, "resampled-fixed-tick-poses.csv"
               ), false, new UTF8Encoding(false), 65536
           )) {
      writer.Write(
          "sample,unity_frame,fixed_time_s,simulation_timestamp_us," +
          "target_timestamp_us,delay_ticks,delay_us,query_timestamp_us," +
          "target_age_us,anchor_timestamp_us," +
          "anchor_game_time_s,anchor_time_scale,status,before_sequence," +
          "after_sequence,before_timestamp_us,after_timestamp_us," +
          "before_offset_us,after_offset_us,bracket_span_us," +
          "interpolation_amount,head_raw_difference_m," +
          "head_raw_difference_deg,left_raw_difference_m," +
          "left_raw_difference_deg,right_raw_difference_m," +
          "right_raw_difference_deg"
      );
      WritePoseHeader(writer, "head");
      WritePoseHeader(writer, "left");
      WritePoseHeader(writer, "right");
      writer.WriteLine();
      var resampled = result.PoseHistory.ResampledFixedSamples;
      var raw = result.PoseHistory.FixedSamples;
      for (var i = 0; i < resampled.Length; i++) {
        var row = resampled[i];
        writer.Write(i);
        writer.Write(',');
        writer.Write(row.UnityFrame);
        Write(writer, row.FixedTime);
        Write(writer, TicksToMicroseconds(row.SimulationTimestamp));
        Write(writer, TicksToMicroseconds(row.TargetTimestamp));
        writer.Write(',');
        writer.Write(result.PoseHistory.InterpolationDelayTicks);
        Write(
            writer,
            TicksToMicroseconds(
                row.SimulationTimestamp - row.TargetTimestamp
            )
        );
        Write(writer, TicksToMicroseconds(row.QueryTimestamp));
        Write(
            writer,
            TicksToMicroseconds(row.QueryTimestamp - row.TargetTimestamp)
        );
        Write(writer, TicksToMicroseconds(row.AnchorTimestamp));
        Write(writer, row.AnchorGameTime);
        Write(writer, row.AnchorTimeScale);
        writer.Write(',');
        writer.Write(row.Status);
        writer.Write(',');
        writer.Write(row.BeforeSequence);
        writer.Write(',');
        writer.Write(row.AfterSequence);
        Write(writer, TicksToMicroseconds(row.BeforeTimestamp));
        Write(writer, TicksToMicroseconds(row.AfterTimestamp));
        Write(
            writer,
            TicksToMicroseconds(row.TargetTimestamp - row.BeforeTimestamp)
        );
        Write(
            writer,
            TicksToMicroseconds(row.AfterTimestamp - row.TargetTimestamp)
        );
        Write(
            writer,
            TicksToMicroseconds(row.AfterTimestamp - row.BeforeTimestamp)
        );
        Write(writer, row.InterpolationAmount);
        var rawRow = i < raw.Length
            ? raw[i].Sample
            : new PoseHistorySample();
        Write(writer, PositionDifference(rawRow.Head, row.Head));
        Write(writer, RotationDifferenceDegrees(rawRow.Head, row.Head));
        Write(writer, PositionDifference(rawRow.Left, row.Left));
        Write(writer, RotationDifferenceDegrees(rawRow.Left, row.Left));
        Write(writer, PositionDifference(rawRow.Right, row.Right));
        Write(writer, RotationDifferenceDegrees(rawRow.Right, row.Right));
        WritePose(writer, row.Head);
        WritePose(writer, row.Left);
        WritePose(writer, row.Right);
        writer.WriteLine();
      }
    }
  }

  private static double PositionDifference(
      TrackedPoseValue first, TrackedPoseValue second
  ) {
    if (!first.IsValid || !second.IsValid)
      return 0.0;
    var x = first.X - second.X;
    var y = first.Y - second.Y;
    var z = first.Z - second.Z;
    return Math.Sqrt(x * x + y * y + z * z);
  }

  private static double RotationDifferenceDegrees(
      TrackedPoseValue first, TrackedPoseValue second
  ) {
    if (!first.IsValid || !second.IsValid)
      return 0.0;
    double dot = Math.Abs(
        first.Qx * second.Qx + first.Qy * second.Qy +
        first.Qz * second.Qz + first.Qw * second.Qw
    );
    dot = Math.Max(-1.0, Math.Min(1.0, dot));
    return 2.0 * Math.Acos(dot) * 180.0 / Math.PI;
  }

  private static void WritePoseHeader(StreamWriter writer, string prefix) {
    writer.Write("," + prefix + "_valid," + prefix + "_connected," +
                 prefix + "_tracking_result," + prefix + "_x," +
                 prefix + "_y," + prefix + "_z," + prefix + "_qx," +
                 prefix + "_qy," + prefix + "_qz," + prefix + "_qw");
  }

  private static void WritePose(StreamWriter writer, TrackedPoseValue pose) {
    writer.Write(',');
    writer.Write(pose.IsValid ? 1 : 0);
    writer.Write(',');
    writer.Write(pose.IsConnected ? 1 : 0);
    writer.Write(',');
    writer.Write(pose.TrackingResult);
    Write(writer, pose.X);
    Write(writer, pose.Y);
    Write(writer, pose.Z);
    Write(writer, pose.Qx);
    Write(writer, pose.Qy);
    Write(writer, pose.Qz);
    Write(writer, pose.Qw);
  }

  private static double TicksToMicroseconds(long ticks) =>
      ticks * 1000000.0 / Stopwatch.Frequency;

  private static void WriteTargetedDiagnostics(CaptureResult result) {
    using (var writer = new StreamWriter(
               Path.Combine(
                   result.Directory, "physbody-update-colliders.csv"
               ), false, new UTF8Encoding(false), 65536
           )) {
      writer.WriteLine(
          "instance_address,calls,unchanged_output_calls," +
          "unchanged_output_percent,total_ms,average_us"
      );
      for (var i = 0;
           i < result.TargetedDiagnostics.PhysBodies.Length;
           i++) {
        var row = result.TargetedDiagnostics.PhysBodies[i];
        writer.Write("0x" + row.InstanceAddress.ToString("X"));
        writer.Write(',');
        writer.Write(row.Calls);
        writer.Write(',');
        writer.Write(row.UnchangedCalls);
        Write(
            writer,
            row.Calls == 0 ? 0.0 : row.UnchangedCalls * 100.0 / row.Calls
        );
        Write(
            writer,
            row.DurationTicks * 1000.0 / Stopwatch.Frequency
        );
        Write(
            writer,
            row.Calls == 0 ? 0.0 :
                row.DurationTicks * 1000000.0 /
                Stopwatch.Frequency / row.Calls
        );
        writer.WriteLine();
      }
    }

    using (var writer = new StreamWriter(
               Path.Combine(result.Directory, "exceptions.csv"), false,
               new UTF8Encoding(false), 65536
           )) {
      writer.WriteLine("identity,count,condition,stack_trace");
      for (var i = 0;
           i < result.TargetedDiagnostics.Exceptions.Length;
           i++) {
        var row = result.TargetedDiagnostics.Exceptions[i];
        writer.Write("0x" + row.Identity.ToString("X16"));
        writer.Write(',');
        writer.Write(row.Count);
        writer.Write(',');
        WriteCsvString(writer, row.Condition);
        writer.Write(',');
        WriteCsvString(writer, row.StackTrace);
        writer.WriteLine();
      }
    }
  }

  private static void WriteMonoGcEvents(CaptureResult result) {
    using (var writer = new StreamWriter(
               Path.Combine(result.Directory, "mono-gc-events.csv"), false,
               new UTF8Encoding(false), 65536
           )) {
      writer.WriteLine(
          "event_index,elapsed_ms,event,generation,serial,thread_id," +
          "player_loop_phase"
      );
      for (var i = 0; i < result.MonoGc.EventCount; i++) {
        var sample = result.MonoGc.Events[i];
        writer.Write(i);
        Write(
            writer,
            (sample.Timestamp - result.StartTimestamp) * 1000.0 /
            Stopwatch.Frequency
        );
        writer.Write(',');
        WriteCsvString(writer, MonoGcProfiler.EventName(sample.Event));
        writer.Write(',');
        writer.Write(sample.Generation);
        writer.Write(',');
        writer.Write(sample.Serial ? "1" : "0");
        writer.Write(',');
        writer.Write(sample.ThreadId);
        writer.Write(',');
        WriteCsvString(writer, sample.Phase.ToString());
        writer.WriteLine();
      }
    }
  }

  private static void WriteStalls(CaptureResult result) {
    const double thresholdMs = 1000.0 / 90.0;
    using (var writer = new StreamWriter(
               Path.Combine(result.Directory, "stalls.csv"), false,
               new UTF8Encoding(false), 65536
           )) {
      writer.WriteLine(
          "sample,unity_frame,elapsed_ms,frame_ms,process_cpu_ms," +
          "fixed_updates,simulation_debt_ms,longest_player_loop_phase," +
          "longest_phase_ms,profiler_self_ms,profiler_gui_heap_delta_bytes," +
          "mono_heap_used_bytes,mono_heap_capacity_bytes,gc_gen0_delta," +
          "gc_gen1_delta,gc_gen2_delta,mono_gc_events," +
          "physbody_update_colliders_calls," +
          "physbody_unchanged_collider_calls," +
          "physbody_update_colliders_ms,exception_count"
      );
      var previousGen0 = result.FrameCount == 0
          ? 0
          : result.Frames[0].GcGen0Collections;
      var previousGen1 = result.FrameCount == 0
          ? 0
          : result.Frames[0].GcGen1Collections;
      var previousGen2 = result.FrameCount == 0
          ? 0
          : result.Frames[0].GcGen2Collections;
      var gcIndex = 0;
      for (var frameIndex = 0; frameIndex < result.FrameCount; frameIndex++) {
        var frame = result.Frames[frameIndex];
        var frameStartMs = frame.ElapsedMs - frame.FrameMs;
        var frameGcEvents = new StringBuilder();
        while (gcIndex < result.MonoGc.EventCount) {
          var gc = result.MonoGc.Events[gcIndex];
          var gcMs = (gc.Timestamp - result.StartTimestamp) * 1000.0 /
                     Stopwatch.Frequency;
          if (gcMs > frame.ElapsedMs)
            break;
          if (gcMs >= frameStartMs) {
            if (frameGcEvents.Length > 0)
              frameGcEvents.Append(';');
            frameGcEvents.Append(MonoGcProfiler.EventName(gc.Event))
                .Append("(gen").Append(gc.Generation).Append(')');
          }
          gcIndex++;
        }
        var gen0Delta = frame.GcGen0Collections - previousGen0;
        var gen1Delta = frame.GcGen1Collections - previousGen1;
        var gen2Delta = frame.GcGen2Collections - previousGen2;
        previousGen0 = frame.GcGen0Collections;
        previousGen1 = frame.GcGen1Collections;
        previousGen2 = frame.GcGen2Collections;
        if (frame.FrameMs < thresholdMs)
          continue;

        var longestPhase = PlayerLoopPhase.None;
        var longestPhaseMs = 0.0;
        var phaseOffset = frameIndex * (int)PlayerLoopPhase.Count;
        for (var phaseIndex = 1;
             phaseIndex < (int)PlayerLoopPhase.Count;
             phaseIndex++) {
          var phaseMs = result.PlayerLoopPhaseMilliseconds[
              phaseOffset + phaseIndex
          ];
          if (phaseMs <= longestPhaseMs)
            continue;
          longestPhaseMs = phaseMs;
          longestPhase = (PlayerLoopPhase)phaseIndex;
        }
        writer.Write(frameIndex);
        writer.Write(',');
        writer.Write(frame.Frame);
        Write(writer, frame.ElapsedMs);
        Write(writer, frame.FrameMs);
        Write(writer, frame.ProcessCpuMs);
        writer.Write(',');
        writer.Write(frame.FixedUpdates);
        Write(writer, frame.SimulationDebtMs);
        writer.Write(',');
        WriteCsvString(writer, longestPhase.ToString());
        Write(writer, longestPhaseMs);
        Write(
            writer, frame.ProfilerUpdateMs + frame.ProfilerFixedUpdateMs +
                    frame.ProfilerGuiMs + frame.ProfilerCaptureMs
        );
        writer.Write(',');
        writer.Write(frame.ProfilerGuiHeapDeltaBytes);
        writer.Write(',');
        writer.Write(frame.ManagedHeapBytes);
        writer.Write(',');
        writer.Write(frame.ManagedHeapCapacityBytes);
        writer.Write(',');
        writer.Write(gen0Delta);
        writer.Write(',');
        writer.Write(gen1Delta);
        writer.Write(',');
        writer.Write(gen2Delta);
        writer.Write(',');
        WriteCsvString(writer, frameGcEvents.ToString());
        writer.Write(',');
        writer.Write(frame.PhysBodyUpdateCollidersCalls);
        writer.Write(',');
        writer.Write(frame.PhysBodyUnchangedColliderCalls);
        Write(writer, frame.PhysBodyUpdateCollidersMs);
        writer.Write(',');
        writer.Write(frame.ExceptionCount);
        writer.WriteLine();
      }
    }
  }

  private static void WriteMarkers(CaptureResult result) {
    using (var writer = new StreamWriter(
               Path.Combine(result.Directory, "markers.csv"), false,
               new UTF8Encoding(false), 65536
           )) {
      writer.WriteLine("sample,unity_frame,marker,elapsed_ms,sample_count");
      for (var frameIndex = 0; frameIndex < result.FrameCount; frameIndex++) {
        var offset = frameIndex * result.MarkerNames.Length;
        for (var markerIndex = 0;
             markerIndex < result.MarkerNames.Length;
             markerIndex++) {
          var milliseconds =
              result.MarkerMilliseconds[offset + markerIndex];
          var count = result.MarkerSampleCounts[offset + markerIndex];
          if (milliseconds == 0.0 && count == 0)
            continue;
          writer.Write(frameIndex);
          writer.Write(',');
          writer.Write(result.Frames[frameIndex].Frame);
          writer.Write(',');
          WriteCsvString(writer, result.MarkerNames[markerIndex]);
          Write(writer, milliseconds);
          writer.Write(',');
          writer.WriteLine(count);
        }
      }
    }
  }

  private static void WritePlayerLoopPhases(CaptureResult result) {
    using (var writer = new StreamWriter(
               Path.Combine(result.Directory, "player-loop-phases.csv"),
               false, new UTF8Encoding(false), 65536
           )) {
      writer.WriteLine("sample,unity_frame,phase,elapsed_ms");
      var phaseCount = (int)PlayerLoopPhase.Count;
      for (var frameIndex = 0; frameIndex < result.FrameCount; frameIndex++) {
        var offset = frameIndex * phaseCount;
        for (var phaseIndex = 1; phaseIndex < phaseCount; phaseIndex++) {
          var milliseconds =
              result.PlayerLoopPhaseMilliseconds[offset + phaseIndex];
          if (milliseconds <= 0.0)
            continue;
          writer.Write(frameIndex);
          writer.Write(',');
          writer.Write(result.Frames[frameIndex].Frame);
          writer.Write(',');
          WriteCsvString(writer, ((PlayerLoopPhase)phaseIndex).ToString());
          Write(writer, milliseconds);
          writer.WriteLine();
        }
      }
    }
  }

  private static void WriteCpuSamples(CaptureResult result) {
    var totals = new Dictionary<string, CpuAggregate>();
    using (var writer = new StreamWriter(
               Path.Combine(result.Directory, "cpu-samples.csv"), false,
               new UTF8Encoding(false), 65536
           )) {
      writer.WriteLine(
          "sample,tick,elapsed_ms,thread_id,thread_role,player_loop_phase," +
          "instruction_pointer," +
          "module,assembly,type,method,stack_depth,exact_stack_addresses," +
          "stack,is_wait_snapshot,completion_wait_id,thread_cycle_delta," +
          "estimated_thread_state_ms"
      );
      for (var i = 0; i < result.CpuSampleCount; i++) {
        var sample = result.CpuSamples[i];
        var resolved = NativeMethodCatalog.Resolve(sample.InstructionPointer);
        var elapsed = (sample.Timestamp - result.CpuSamples[0].Timestamp) *
                      1000.0 / Stopwatch.Frequency;
        var role = sample.IsMainThread ? "Main thread" : "Worker threads";
        writer.Write(i);
        writer.Write(',');
        writer.Write(sample.Tick);
        Write(writer, elapsed);
        writer.Write(',');
        writer.Write(sample.ThreadId);
        writer.Write(',');
        WriteCsvString(writer, role);
        writer.Write(',');
        WriteCsvString(writer, sample.PlayerLoopPhase.ToString());
        writer.Write(',');
        writer.Write("0x" + sample.InstructionPointer.ToString("X"));
        writer.Write(',');
        WriteCsvString(writer, resolved.Module ?? "unknown");
        writer.Write(',');
        WriteCsvString(writer, resolved.Assembly ?? "");
        writer.Write(',');
        WriteCsvString(writer, resolved.Type ?? "");
        writer.Write(',');
        WriteCsvString(writer, resolved.Method ?? "");
        writer.Write(',');
        writer.Write(sample.StackDepth);
        writer.Write(',');
        WriteCsvString(writer, BuildExactAddressStack(result, sample));
        writer.Write(',');
        var path = BuildSampleStack(result, sample, role);
        WriteCsvString(writer, path);
        writer.Write(',');
        writer.Write(sample.IsWaitSnapshot ? "1" : "0");
        writer.Write(',');
        writer.Write(sample.WaitId);
        writer.Write(',');
        writer.Write(sample.CycleDelta);
        Write(writer, sample.WeightMilliseconds);
        writer.WriteLine();

        if (sample.IsWaitSnapshot)
          continue;
        if (!totals.TryGetValue(path, out var aggregate)) {
          aggregate = new CpuAggregate {
            Path = path,
            Name = resolved.DisplayName,
            ThreadRole = role,
          };
          totals.Add(path, aggregate);
        }
        aggregate.Samples++;
        aggregate.EstimatedMilliseconds += sample.WeightMilliseconds;
      }
    }

    using (var csv = new StreamWriter(
               Path.Combine(result.Directory, "cpu-flamegraph.csv"), false,
               new UTF8Encoding(false), 65536
           ))
    using (var folded = new StreamWriter(
               Path.Combine(result.Directory, "cpu-flamegraph.folded"), false,
               new UTF8Encoding(false), 65536
           )) {
      csv.WriteLine("stack,samples,estimated_thread_state_ms");
      foreach (var aggregate in totals.Values) {
        WriteCsvString(csv, aggregate.Path);
        csv.Write(',');
        csv.Write(aggregate.Samples);
        Write(csv, aggregate.EstimatedMilliseconds);
        csv.WriteLine();
        folded.Write(SanitizeFolded(aggregate.Path));
        folded.Write(' ');
        folded.WriteLine(aggregate.Samples);
      }
    }
  }

  private static void WriteSampledPhases(CaptureResult result) {
    var phases = AggregateSampledPhases(result);
    phases.Sort((left, right) =>
        (right.MainMilliseconds + right.WorkerMilliseconds).CompareTo(
            left.MainMilliseconds + left.WorkerMilliseconds
        ));
    using (var writer = new StreamWriter(
               Path.Combine(result.Directory, "sampled-phases.csv"), false,
               new UTF8Encoding(false), 65536
           )) {
      writer.WriteLine(
          "phase,main_thread_ms,aggregate_worker_thread_ms,samples"
      );
      foreach (var phase in phases) {
        WriteCsvString(writer, phase.Name);
        Write(writer, phase.MainMilliseconds);
        Write(writer, phase.WorkerMilliseconds);
        writer.Write(',');
        writer.WriteLine(phase.Samples);
      }
    }
  }

  private static void WriteWaitCorrelations(CaptureResult result) {
    var workerByTick = new Dictionary<int, CpuSample>();
    for (var i = 0; i < result.CpuSampleCount; i++) {
      var sample = result.CpuSamples[i];
      if (sample.IsWaitSnapshot)
        continue;
      if (!sample.IsMainThread)
        workerByTick[sample.Tick] = sample;
    }
    var totals = new Dictionary<string, WaitCorrelation>();
    for (var i = 0; i < result.CpuSampleCount; i++) {
      var main = result.CpuSamples[i];
      if (main.IsWaitSnapshot)
        continue;
      if (!main.IsMainThread ||
          !string.Equals(
              NativeMethodCatalog.Resolve(main.InstructionPointer).Module,
              "ntdll.dll", StringComparison.OrdinalIgnoreCase
          ) || !workerByTick.TryGetValue(main.Tick, out var worker))
        continue;
      var mainStack = BuildSampleStack(result, main, "Main thread");
      var workerStack = BuildSampleStack(result, worker, "Worker threads");
      var key = mainStack + "\n" + workerStack;
      if (!totals.TryGetValue(key, out var correlation)) {
        correlation = new WaitCorrelation {
          MainStack = mainStack,
          WorkerStack = workerStack,
        };
        totals.Add(key, correlation);
      }
      correlation.Pairs++;
      correlation.EstimatedMainWaitMilliseconds += main.WeightMilliseconds;
    }

    var rows = new List<WaitCorrelation>(totals.Values);
    rows.Sort((left, right) => right.Pairs.CompareTo(left.Pairs));
    using (var writer = new StreamWriter(
               Path.Combine(result.Directory, "wait-correlations.csv"), false,
               new UTF8Encoding(false), 65536
           )) {
      writer.WriteLine(
          "main_wait_stack,worker_stack,paired_samples," +
          "estimated_main_wait_ms"
      );
      foreach (var row in rows) {
        WriteCsvString(writer, row.MainStack);
        writer.Write(',');
        WriteCsvString(writer, row.WorkerStack);
        writer.Write(',');
        writer.Write(row.Pairs);
        Write(writer, row.EstimatedMainWaitMilliseconds);
        writer.WriteLine();
      }
    }
  }

  private static void WriteCompletionWaits(CaptureResult result) {
    var mainSampleByWait = new Dictionary<int, int>();
    for (var i = 0; i < result.CpuSampleCount; i++) {
      var sample = result.CpuSamples[i];
      if (sample.IsMainThread && sample.WaitId > 0 &&
          !mainSampleByWait.ContainsKey(sample.WaitId))
        mainSampleByWait.Add(sample.WaitId, i);
    }
    using (var writer = new StreamWriter(
               Path.Combine(result.Directory, "completion-waits.csv"), false,
               new UTF8Encoding(false), 65536
           )) {
      writer.WriteLine(
          "wait_id,elapsed_ms,duration_ms,player_loop_phase,state_address," +
          "event_handle,target_count,initial_count,final_count," +
          "caller_stack_addresses,caller_stack"
      );
      var waits = result.CompletionWaits;
      for (var i = 0; i < waits.WaitCount; i++) {
        var wait = waits.Waits[i];
        var mainSampleIndex = mainSampleByWait.TryGetValue(
            wait.Id, out var foundSample
        ) ? foundSample : -1;
        writer.Write(wait.Id);
        Write(
            writer, (wait.Started - result.StartTimestamp) * 1000.0 /
                    Stopwatch.Frequency
        );
        Write(
            writer, wait.DurationTicks * 1000.0 / Stopwatch.Frequency
        );
        writer.Write(',');
        WriteCsvString(writer, wait.Phase.ToString());
        writer.Write(',');
        writer.Write("0x" + wait.StateAddress.ToString("X"));
        writer.Write(',');
        writer.Write("0x" + wait.EventHandle.ToString("X"));
        writer.Write(',');
        writer.Write(wait.TargetCount);
        writer.Write(',');
        writer.Write(wait.InitialCount);
        writer.Write(',');
        writer.Write(wait.FinalCount);
        writer.Write(',');
        WriteCsvString(
            writer, mainSampleIndex >= 0
                ? BuildExactAddressStack(
                    result, result.CpuSamples[mainSampleIndex]
                )
                : ""
        );
        writer.Write(',');
        WriteCsvString(
            writer, mainSampleIndex >= 0
                ? BuildResolvedStack(
                    result.StackAddresses, result.StackAddressCount,
                    result.CpuSamples[mainSampleIndex].StackOffset,
                    result.CpuSamples[mainSampleIndex].StackDepth, true
                )
                : ""
        );
        writer.WriteLine();
      }
    }

    using (var writer = new StreamWriter(
               Path.Combine(
                   result.Directory, "completion-wait-workers.csv"
               ), false, new UTF8Encoding(false), 65536
           )) {
      writer.WriteLine(
          "wait_id,elapsed_ms,thread_id,thread_name,thread_cycle_delta," +
          "stack_addresses,stack"
      );
      for (var i = 0; i < result.CpuSampleCount; i++) {
        var sample = result.CpuSamples[i];
        if (!sample.IsWaitSnapshot)
          continue;
        writer.Write(sample.WaitId);
        Write(
            writer, (sample.Timestamp - result.StartTimestamp) * 1000.0 /
                    Stopwatch.Frequency
        );
        writer.Write(',');
        writer.Write(sample.ThreadId);
        writer.Write(',');
        result.ThreadNames.TryGetValue(sample.ThreadId, out var threadName);
        WriteCsvString(writer, threadName ?? "");
        writer.Write(',');
        writer.Write(sample.CycleDelta);
        writer.Write(',');
        WriteCsvString(writer, BuildExactAddressStack(result, sample));
        writer.Write(',');
        WriteCsvString(
            writer, BuildSampleStack(result, sample, "Worker thread")
        );
        writer.WriteLine();
      }
    }
  }

  private static void WriteGraphics(CaptureResult result) {
    using (var writer = new StreamWriter(
               Path.Combine(result.Directory, "graphics-adapters.csv"),
               false, new UTF8Encoding(false), 65536
           )) {
      writer.WriteLine(
          "adapter_index,name,vendor_id,device_id,dedicated_memory_bytes," +
          "luid,matches_unity_vendor_device,is_steamvr_device," +
          "has_process_gpu_engine"
      );
      for (var i = 0; i < result.Graphics.Adapters.Length; i++) {
        var adapter = result.Graphics.Adapters[i];
        writer.Write(adapter.Index);
        writer.Write(',');
        WriteCsvString(writer, adapter.Name);
        writer.Write(',');
        writer.Write("0x" + adapter.VendorId.ToString("X4"));
        writer.Write(',');
        writer.Write("0x" + adapter.DeviceId.ToString("X4"));
        writer.Write(',');
        writer.Write(adapter.DedicatedVideoMemory);
        writer.Write(',');
        writer.Write("0x" + adapter.Luid.ToString("X16"));
        writer.Write(',');
        writer.Write(
            adapter.VendorId == result.Graphics.UnityDeviceVendorId &&
            adapter.DeviceId == result.Graphics.UnityDeviceId ? 1 : 0
        );
        writer.Write(',');
        writer.Write(
            adapter.Luid == result.Graphics.SteamVrAdapterLuid ? 1 : 0
        );
        writer.Write(',');
        var hasProcessEngine = false;
        for (var counterIndex = 0;
             counterIndex < result.Graphics.EngineCounters.Length;
             counterIndex++) {
          if (result.Graphics.EngineCounters[counterIndex].Luid ==
              adapter.Luid) {
            hasProcessEngine = true;
            break;
          }
        }
        writer.Write(hasProcessEngine ? 1 : 0);
        writer.WriteLine();
      }
    }

    using (var writer = new StreamWriter(
               Path.Combine(
                   result.Directory, "graphics-pending-work.csv"
               ), false, new UTF8Encoding(false), 65536
           )) {
      writer.WriteLine(
          "event_id,elapsed_ms,duration_ms,thread_id," +
          "completion_target_serial," +
          "player_loop_phase,first_completion_wait_id," +
          "last_completion_wait_id,target_address,unityplayer_offset"
      );
      for (var i = 0; i < result.Graphics.PendingWorkCount; i++) {
        var sample = result.Graphics.PendingWork[i];
        writer.Write(sample.Id);
        Write(
            writer, (sample.Started - result.StartTimestamp) * 1000.0 /
                    Stopwatch.Frequency
        );
        Write(
            writer, sample.DurationTicks * 1000.0 / Stopwatch.Frequency
        );
        writer.Write(',');
        writer.Write(sample.ThreadId);
        writer.Write(',');
        writer.Write(sample.CompletionTargetSerial);
        writer.Write(',');
        WriteCsvString(writer, sample.Phase.ToString());
        writer.Write(',');
        writer.Write(sample.FirstCompletionWaitId);
        writer.Write(',');
        writer.Write(sample.LastCompletionWaitId);
        writer.Write(',');
        writer.Write(
            "0x" + result.Graphics.PendingWorkTargetAddress.ToString("X")
        );
        writer.Write(',');
        writer.Write(
            "0x" + Math.Max(
                0, result.Graphics.PendingWorkTargetAddress -
                   result.Graphics.UnityPlayerBaseAddress
            ).ToString("X")
        );
        writer.WriteLine();
      }
    }

    using (var writer = new StreamWriter(
               Path.Combine(result.Directory, "gpu-engines.csv"), false,
               new UTF8Encoding(false), 65536
           )) {
      writer.WriteLine(
          "elapsed_ms,counter_index,adapter_luid,adapter_name,engine_type," +
          "utilization_percent,instance_name"
      );
      for (var i = 0; i < result.Graphics.EngineSampleCount; i++) {
        var sample = result.Graphics.EngineSamples[i];
        if (sample.CounterIndex < 0 ||
            sample.CounterIndex >= result.Graphics.EngineCounters.Length)
          continue;
        var counter = result.Graphics.EngineCounters[sample.CounterIndex];
        writer.Write(
            Format((sample.Timestamp - result.StartTimestamp) * 1000.0 /
                   Stopwatch.Frequency)
        );
        writer.Write(',');
        writer.Write(sample.CounterIndex);
        writer.Write(',');
        writer.Write("0x" + counter.Luid.ToString("X16"));
        writer.Write(',');
        var adapterName = "";
        for (var adapterIndex = 0;
             adapterIndex < result.Graphics.Adapters.Length;
             adapterIndex++) {
          if (result.Graphics.Adapters[adapterIndex].Luid == counter.Luid) {
            adapterName = result.Graphics.Adapters[adapterIndex].Name;
            break;
          }
        }
        WriteCsvString(writer, adapterName);
        writer.Write(',');
        WriteCsvString(writer, counter.EngineType);
        Write(writer, sample.UtilizationPercent);
        writer.Write(',');
        WriteCsvString(writer, counter.InstanceName);
        writer.WriteLine();
      }
    }
  }

  private static void WriteHtml(CaptureResult result) {
    var totalMarkerMs = new double[result.MarkerNames.Length];
    var markerFrames = new int[result.MarkerNames.Length];
    var totalFrameMs = 0.0;
    var totalCpuMs = 0.0;
    var totalFixedUpdateMs = 0.0;
    var totalPhysicsMs = 0.0;
    var totalScriptFixedMs = 0.0;
    var totalPlayerLoopPhaseMs = new double[(int)PlayerLoopPhase.Count];
    var totalSimulationDebtMs = 0.0;
    var totalProfilerSelfMs = 0.0;
    var totalProfilerGuiMs = 0.0;
    long totalProfilerGuiHeapDelta = 0;
    var maximumFrameMs = 0.0;
    var stallFrames = 0;
    var totalFixedSteps = 0;
    var framesWithTwoOrMoreFixedSteps = 0;
    var slowFrames = 0;
    var compositorFrames = 0;
    for (var frameIndex = 0; frameIndex < result.FrameCount; frameIndex++) {
      var frame = result.Frames[frameIndex];
      totalFrameMs += frame.FrameMs;
      totalCpuMs += frame.ProcessCpuMs;
      totalFixedUpdateMs += frame.FixedUpdateMs;
      totalPhysicsMs += frame.PhysicsFixedUpdateMs;
      totalScriptFixedMs += frame.ScriptFixedUpdateMs;
      var phaseOffset = frameIndex * (int)PlayerLoopPhase.Count;
      for (var phaseIndex = 1;
           phaseIndex < (int)PlayerLoopPhase.Count;
           phaseIndex++) {
        totalPlayerLoopPhaseMs[phaseIndex] +=
            result.PlayerLoopPhaseMilliseconds[phaseOffset + phaseIndex];
      }
      totalSimulationDebtMs += frame.SimulationDebtMs;
      totalProfilerSelfMs += frame.ProfilerUpdateMs +
                             frame.ProfilerFixedUpdateMs +
                             frame.ProfilerGuiMs +
                             frame.ProfilerCaptureMs;
      totalProfilerGuiMs += frame.ProfilerGuiMs;
      totalProfilerGuiHeapDelta += frame.ProfilerGuiHeapDeltaBytes;
      if (frame.FrameMs > maximumFrameMs)
        maximumFrameMs = frame.FrameMs;
      if (frame.FrameMs >= 1000.0 / 90.0)
        stallFrames++;
      totalFixedSteps += frame.FixedUpdates;
      if (frame.FixedUpdates >= 2)
        framesWithTwoOrMoreFixedSteps++;
      if (frame.FrameMs > 1000.0 / 120.0)
        slowFrames++;
      if (frame.HasCompositorTiming)
        compositorFrames++;
      var offset = frameIndex * result.MarkerNames.Length;
      for (var markerIndex = 0;
           markerIndex < result.MarkerNames.Length;
           markerIndex++) {
        var value = result.MarkerMilliseconds[offset + markerIndex];
        totalMarkerMs[markerIndex] += value;
        if (value > 0.0)
          markerFrames[markerIndex]++;
      }
    }

    var order = new int[result.MarkerNames.Length];
    for (var i = 0; i < order.Length; i++)
      order[i] = i;
    Array.Sort(order, (a, b) => totalMarkerMs[b].CompareTo(totalMarkerMs[a]));

    var markersJson = new StringBuilder("[");
    var first = true;
    for (var i = 0; i < order.Length; i++) {
      var markerIndex = order[i];
      if (totalMarkerMs[markerIndex] <= 0.0)
        continue;
      if (!first)
        markersJson.Append(',');
      first = false;
      markersJson.Append("{\"name\":\"")
          .Append(JsonEscape(result.MarkerNames[markerIndex]))
          .Append("\",\"total\":")
          .Append(Format(totalMarkerMs[markerIndex]))
          .Append(",\"frames\":")
          .Append(markerFrames[markerIndex])
          .Append('}');
    }
    markersJson.Append(']');

    var cpuTotals = AggregateCpuSamples(result);
    cpuTotals.Sort((left, right) =>
        right.EstimatedMilliseconds.CompareTo(left.EstimatedMilliseconds));
    var cpuJson = new StringBuilder("[");
    for (var i = 0; i < cpuTotals.Count; i++) {
      if (i > 0)
        cpuJson.Append(',');
      var item = cpuTotals[i];
      cpuJson.Append("{\"path\":\"")
          .Append(JsonEscape(item.Path))
          .Append("\",\"name\":\"")
          .Append(JsonEscape(item.Name))
          .Append("\",\"thread\":\"")
          .Append(JsonEscape(item.ThreadRole))
          .Append("\",\"samples\":")
          .Append(item.Samples)
          .Append(",\"total\":")
          .Append(Format(item.EstimatedMilliseconds))
          .Append('}');
    }
    cpuJson.Append(']');

    var phaseTotals = AggregateSampledPhases(result);
    phaseTotals.Sort((left, right) =>
        (right.MainMilliseconds + right.WorkerMilliseconds).CompareTo(
            left.MainMilliseconds + left.WorkerMilliseconds
        ));
    var phasesJson = new StringBuilder("[");
    for (var i = 0; i < phaseTotals.Count; i++) {
      if (i > 0)
        phasesJson.Append(',');
      var phase = phaseTotals[i];
      phasesJson.Append("{\"name\":\"")
          .Append(JsonEscape(phase.Name))
          .Append("\",\"main\":")
          .Append(Format(phase.MainMilliseconds))
          .Append(",\"worker\":")
          .Append(Format(phase.WorkerMilliseconds))
          .Append(",\"samples\":")
          .Append(phase.Samples)
          .Append('}');
    }
    phasesJson.Append(']');

    var frameDivisor = Math.Max(1, result.FrameCount);
    var fixedStepDivisor = Math.Max(1, totalFixedSteps);
    var playerLoopJsonBuilder = new StringBuilder("[");
    var firstPlayerLoopPhase = true;
    for (var phaseIndex = 1;
         phaseIndex < (int)PlayerLoopPhase.Count;
         phaseIndex++) {
      var total = totalPlayerLoopPhaseMs[phaseIndex];
      if (total <= 0.0)
        continue;
      if (!firstPlayerLoopPhase)
        playerLoopJsonBuilder.Append(',');
      firstPlayerLoopPhase = false;
      playerLoopJsonBuilder.Append("{\"name\":\"")
          .Append(((PlayerLoopPhase)phaseIndex).ToString())
          .Append("\",\"average\":")
          .Append(Format(total / frameDivisor))
          .Append(",\"total\":")
          .Append(Format(total))
          .Append('}');
    }
    var playerLoopJson = playerLoopJsonBuilder.Append(']').ToString();

    var completionWaitTotalMs = 0.0;
    var completionWaitMaximumMs = 0.0;
    var completionWaitsOverOneMs = 0;
    for (var i = 0; i < result.CompletionWaits.WaitCount; i++) {
      var milliseconds = result.CompletionWaits.Waits[i].DurationTicks *
                         1000.0 / Stopwatch.Frequency;
      completionWaitTotalMs += milliseconds;
      if (milliseconds > completionWaitMaximumMs)
        completionWaitMaximumMs = milliseconds;
      if (milliseconds >= 1.0)
        completionWaitsOverOneMs++;
    }
    var completionWorkerSnapshots = 0;
    for (var i = 0; i < result.CpuSampleCount; i++) {
      if (result.CpuSamples[i].IsWaitSnapshot)
        completionWorkerSnapshots++;
    }

    var pendingWorkTotalMs = 0.0;
    var pendingWorkMaximumMs = 0.0;
    var pendingWorkLinkedWaits = 0;
    for (var i = 0; i < result.Graphics.PendingWorkCount; i++) {
      var sample = result.Graphics.PendingWork[i];
      var milliseconds = sample.DurationTicks * 1000.0 /
                         Stopwatch.Frequency;
      pendingWorkTotalMs += milliseconds;
      if (milliseconds > pendingWorkMaximumMs)
        pendingWorkMaximumMs = milliseconds;
      if (sample.FirstCompletionWaitId > 0)
        pendingWorkLinkedWaits +=
            sample.LastCompletionWaitId -
            sample.FirstCompletionWaitId + 1;
    }
    var adapterRows = new StringBuilder();
    for (var i = 0; i < result.Graphics.Adapters.Length; i++) {
      var adapter = result.Graphics.Adapters[i];
      var unityDevice =
          adapter.VendorId == result.Graphics.UnityDeviceVendorId &&
          adapter.DeviceId == result.Graphics.UnityDeviceId;
      var steamVrDevice =
          adapter.Luid == result.Graphics.SteamVrAdapterLuid;
      var processEngine = false;
      for (var counterIndex = 0;
           counterIndex < result.Graphics.EngineCounters.Length;
           counterIndex++) {
        if (result.Graphics.EngineCounters[counterIndex].Luid ==
            adapter.Luid) {
          processEngine = true;
          break;
        }
      }
      adapterRows.Append("<tr><td>")
          .Append(adapter.Index)
          .Append("</td><td>")
          .Append(HtmlEscape(adapter.Name))
          .Append("</td><td><code>0x")
          .Append(adapter.VendorId.ToString("X4"))
          .Append(":0x")
          .Append(adapter.DeviceId.ToString("X4"))
          .Append("</code></td><td><code>0x")
          .Append(adapter.Luid.ToString("X16"))
          .Append("</code></td><td>")
          .Append(unityDevice ? "yes" : "")
          .Append("</td><td>")
          .Append(steamVrDevice ? "yes" : "")
          .Append("</td><td>")
          .Append(processEngine ? "yes" : "")
          .Append("</td></tr>");
    }

    var averageFrameMs =
        result.FrameCount == 0 ? 0.0 : totalFrameMs / result.FrameCount;
    var averageCpuMs =
        result.FrameCount == 0 ? 0.0 : totalCpuMs / result.FrameCount;
    var durationMs = result.FrameCount == 0
        ? 0.0
        : result.Frames[result.FrameCount - 1].ElapsedMs;
    var gen0Collections = result.FrameCount < 2 ? 0 :
        result.Frames[result.FrameCount - 1].GcGen0Collections -
        result.Frames[0].GcGen0Collections;
    var gen1Collections = result.FrameCount < 2 ? 0 :
        result.Frames[result.FrameCount - 1].GcGen1Collections -
        result.Frames[0].GcGen1Collections;
    var gen2Collections = result.FrameCount < 2 ? 0 :
        result.Frames[result.FrameCount - 1].GcGen2Collections -
        result.Frames[0].GcGen2Collections;
    var physBodyMs = result.TargetedDiagnostics.PhysBodyTicks * 1000.0 /
                     Stopwatch.Frequency;
    var unchangedPhysBodyPercent =
        result.TargetedDiagnostics.PhysBodyCalls == 0 ? 0.0 :
            result.TargetedDiagnostics.PhysBodyUnchangedCalls * 100.0 /
            result.TargetedDiagnostics.PhysBodyCalls;
    var html = @"<!doctype html>
<html lang=""en""><head><meta charset=""utf-8""><title>BONEWORKS profile</title>
<style>
:root{color-scheme:dark;font:14px system-ui,sans-serif;background:#111;color:#eee}
body{max-width:1200px;margin:32px auto;padding:0 24px}h1{margin-bottom:4px}
.muted{color:#aaa}.cards{display:flex;gap:12px;flex-wrap:wrap;margin:24px 0}
.card{background:#1d1d1d;border:1px solid #333;border-radius:7px;padding:14px 18px;min-width:150px}
.value{font-size:24px;font-variant-numeric:tabular-nums}.chart{border:1px solid #333;background:#181818;padding:8px}
.bar{height:32px;line-height:32px;margin:2px 0;padding:0 8px;box-sizing:border-box;white-space:nowrap;overflow:hidden;color:#fff;text-shadow:0 1px 2px #000;cursor:default}
.controls{display:flex;gap:8px;align-items:center;margin:12px 0}.controls button{background:#292929;color:#eee;border:1px solid #555;border-radius:5px;padding:7px 12px;cursor:pointer}.controls button.active{background:#1769aa;border-color:#59aef0}
.flame{position:relative;height:190px;border:1px solid #444;background:#181818;overflow:hidden}.flame-node{position:absolute;height:28px;border:1px solid #111;box-sizing:border-box;padding:5px 6px;white-space:nowrap;overflow:hidden;text-overflow:ellipsis;color:white;text-shadow:0 1px 2px #000;cursor:pointer;font-size:12px}.flame-node:hover{filter:brightness(1.25);z-index:2}
.phase-grid{display:grid;grid-template-columns:1fr 1fr;gap:16px}.phase-grid h3{margin-bottom:8px}.empty{padding:18px;border:1px solid #333;background:#181818;color:#aaa}
.phase-row{display:grid;grid-template-columns:minmax(150px,42%) 1fr 80px;gap:8px;align-items:center;margin:7px 0}.phase-name{overflow:hidden;text-overflow:ellipsis;white-space:nowrap}.phase-meter{height:18px;background:#292929;border:1px solid #3b3b3b}.phase-fill{height:100%}.phase-value{text-align:right;font-variant-numeric:tabular-nums;color:#bbb}
table{border-collapse:collapse;width:100%;margin-top:24px}th,td{padding:7px 9px;border-bottom:1px solid #333;text-align:right}th:first-child,td:first-child{text-align:left}
code{color:#ddd}a{color:#8cc8ff}
</style></head><body>
<h1>BONEWORKS performance capture</h1>
<div class=""muted"">" + HtmlEscape(result.Options.Preset) + @" · " + HtmlEscape(result.Mode) + @" mode. Marker times are Unity recorder totals and can overlap.</div>
<div class=""cards""><div class=""card""><div>Frames</div><div class=""value"">" + result.FrameCount + @"</div></div>
<div class=""card""><div>Duration</div><div class=""value"">" + Format(durationMs / 1000.0) + @" s</div></div>
<div class=""card""><div>Average frame</div><div class=""value"">" + Format(averageFrameMs) + @" ms</div></div>
<div class=""card""><div>Average process CPU</div><div class=""value"">" + Format(averageCpuMs) + @" ms</div></div>
<div class=""card""><div>Frames slower than 120 FPS</div><div class=""value"">" + slowFrames + @"</div></div>
<div class=""card""><div>Frames with 2+ physics ticks</div><div class=""value"">" + framesWithTwoOrMoreFixedSteps + @"</div></div>
<div class=""card""><div>Average simulation debt</div><div class=""value"">" + Format(totalSimulationDebtMs / frameDivisor) + @" ms</div></div>
<div class=""card""><div>Physics time per tick</div><div class=""value"">" + Format(totalPhysicsMs / fixedStepDivisor) + @" ms</div></div>
<div class=""card""><div>Compositor samples</div><div class=""value"">" + compositorFrames + @"</div></div></div>
<h2>Profiler overhead and managed runtime</h2><p class=""muted"">The profiler measures its own callbacks and GUI work so an A/B run can distinguish a game hitch from measurement overhead. <a href=""stalls.csv"">stalls.csv</a> lists every frame below 90 FPS with its longest measured PlayerLoop phase and GC events. <a href=""mono-gc-events.csv"">mono-gc-events.csv</a> contains the raw Mono stop-the-world lifecycle.</p>
<div class=""cards""><div class=""card""><div>Longest frame</div><div class=""value"">" + Format(maximumFrameMs) + @" ms</div></div>
<div class=""card""><div>Frames below 90 FPS</div><div class=""value"">" + stallFrames + @"</div></div>
<div class=""card""><div>Profiler time / frame</div><div class=""value"">" + Format(totalProfilerSelfMs / frameDivisor) + @" ms</div></div>
<div class=""card""><div>GUI time / frame</div><div class=""value"">" + Format(totalProfilerGuiMs / frameDivisor) + @" ms</div></div>
<div class=""card""><div>GUI heap delta</div><div class=""value"">" + totalProfilerGuiHeapDelta + @" B</div></div>
<div class=""card""><div>Mono GC events</div><div class=""value"">" + result.MonoGc.EventCount + @"</div></div>
<div class=""card""><div>GC collections (0 / 1 / 2)</div><div class=""value"">" + gen0Collections + @" / " + gen1Collections + @" / " + gen2Collections + @"</div></div></div>
<h2>Tracked pose history</h2><p class=""muted"">A dedicated thread polls OpenVR independently of rendered frames. <a href=""openvr-pose-history.csv"">openvr-pose-history.csv</a> contains its timestamped head and controller samples. <a href=""fixed-tick-poses.csv"">fixed-tick-poses.csv</a> contains separate main-thread queries for every Unity fixed tick. <a href=""resampled-fixed-tick-poses.csv"">resampled-fixed-tick-poses.csv</a> maps each simulation tick onto the previous rendered-frame clock anchor, brackets that target time in the background history, and interpolates position and rotation. It records unclamped timing offsets and the fallback status so missing future samples are visible. Positions and rotations use OpenVR standing-space coordinates.</p>
<div class=""cards""><div class=""card""><div>Sampler status</div><div class=""value"">" + HtmlEscape(result.PoseHistory.Status) + @"</div></div>
<div class=""card""><div>Background samples</div><div class=""value"">" + result.PoseHistory.BackgroundSamples.Length + @"</div></div>
<div class=""card""><div>Fixed-tick samples</div><div class=""value"">" + result.PoseHistory.FixedSamples.Length + @"</div></div>
<div class=""card""><div>Actual sample rate</div><div class=""value"">" + Format(result.PoseHistory.ActualSampleRate) + @" Hz</div></div>
<div class=""card""><div>Query errors</div><div class=""value"">" + result.PoseHistory.QueryErrors + @"</div></div>
<div class=""card""><div>Schedule overruns</div><div class=""value"">" + result.PoseHistory.ScheduleOverruns + @"</div></div></div>
<h2>Targeted diagnostics</h2><p class=""muted"">Detailed mode times <code>PhysBody.UpdateColliders</code> and compares the resulting body and finger collider geometry with the previous call. An unchanged result identifies candidate redundant work; it does not prove the method made no internal writes. <a href=""physbody-update-colliders.csv"">physbody-update-colliders.csv</a> breaks this down by body. <a href=""exceptions.csv"">exceptions.csv</a> groups exceptions by a stable hash of their stack trace.</p>
<div class=""cards""><div class=""card""><div>UpdateColliders calls</div><div class=""value"">" + result.TargetedDiagnostics.PhysBodyCalls + @"</div></div>
<div class=""card""><div>Unchanged collider outputs</div><div class=""value"">" + Format(unchangedPhysBodyPercent) + @"%</div></div>
<div class=""card""><div>UpdateColliders total</div><div class=""value"">" + Format(physBodyMs) + @" ms</div></div>
<div class=""card""><div>Captured exceptions</div><div class=""value"">" + result.TargetedDiagnostics.ExceptionCount + @"</div></div>
<div class=""card""><div>Exception identities</div><div class=""value"">" + result.TargetedDiagnostics.Exceptions.Length + @"</div></div></div>
<h2>Statistical flamegraph</h2><p class=""muted"">Width is sampled wall time and each row is one native call-stack level. The first branch below each thread is the exact PlayerLoop phase observed with that stack. UnityPlayer names come from a matching PDB when available, and GameAssembly names use runtime metadata plus IL2CppDumper fallback symbols. Main and worker scales are separate. Click any rectangle to zoom; use Reset to return.</p>
<div class=""controls""><button id=""showMain"" class=""active"">Main thread</button><button id=""showWorkers"">Worker threads</button><button id=""resetFlame"">Reset zoom</button><span id=""flamePath"" class=""muted""></span></div>
<div id=""flame"" class=""flame""></div>
<h2>Measured PlayerLoop phases</h2><p class=""muted"">Timestamp probes wrap Unity's actual top-level entries and each direct FixedUpdate subsystem. Nested bars overlap. The values are averages per rendered frame. Use <a href=""player-loop-phases.csv"">player-loop-phases.csv</a> for the per-frame values and <a href=""frames.csv"">frames.csv</a> for tick count, simulation debt, and frame time.</p>
<div id=""exactPhases"" class=""chart""></div>
<h2>Sampled CPU phases</h2><p class=""muted"">These fallback phases are classified from sampled method and native-module names because this Unity player strips the marker recorder. Physics includes fixed-update, rigidbody, collider, joint, puppet, and locomotion symbols. Treat the groups as diagnostic categories rather than exact Unity subsystems.</p>
<div class=""phase-grid""><section><h3>Main thread</h3><div id=""mainPhases"" class=""chart""></div></section><section><h3>Aggregate workers</h3><div id=""workerPhases"" class=""chart""></div></section></div>
<h2>Instrumented completion waits</h2><p class=""muted"">Detailed mode times the verified Unity completion-counter fence directly. <a href=""completion-waits.csv"">completion-waits.csv</a> contains its exact caller addresses, phase, target count, completed counts, and duration. <a href=""completion-wait-workers.csv"">completion-wait-workers.csv</a> contains exact stacks from the busiest worker threads sampled while that fence was active.</p>
<div class=""cards""><div class=""card""><div>Completion waits</div><div class=""value"">" + result.CompletionWaits.WaitCount + @"</div></div>
<div class=""card""><div>Total completion wait</div><div class=""value"">" + Format(completionWaitTotalMs) + @" ms</div></div>
<div class=""card""><div>Waits at least 1 ms</div><div class=""value"">" + completionWaitsOverOneMs + @"</div></div>
<div class=""card""><div>Longest completion wait</div><div class=""value"">" + Format(completionWaitMaximumMs) + @" ms</div></div>
<div class=""card""><div>Busy-worker snapshots</div><div class=""value"">" + completionWorkerSnapshots + @"</div></div></div>
<h2>Graphics device and pending work</h2><p class=""muted"">Detailed mode resolves Unity's live graphics device and times the virtual pending-work call that contains the completion fence. <a href=""graphics-pending-work.csv"">graphics-pending-work.csv</a> links each call to its nested completion-wait IDs. <a href=""graphics-adapters.csv"">graphics-adapters.csv</a> maps Unity and SteamVR to DXGI adapters and LUIDs. <a href=""gpu-engines.csv"">gpu-engines.csv</a> records this process's nonzero Windows GPU-engine counters at 10 Hz.</p>
<div class=""cards""><div class=""card""><div>Unity graphics device</div><div class=""value"">" + HtmlEscape(result.Graphics.UnityDeviceName) + @"</div></div>
<div class=""card""><div>Pending-work calls</div><div class=""value"">" + result.Graphics.PendingWorkCount + @"</div></div>
<div class=""card""><div>Total pending-work time</div><div class=""value"">" + Format(pendingWorkTotalMs) + @" ms</div></div>
<div class=""card""><div>Longest pending-work call</div><div class=""value"">" + Format(pendingWorkMaximumMs) + @" ms</div></div>
<div class=""card""><div>Linked completion waits</div><div class=""value"">" + pendingWorkLinkedWaits + @"</div></div>
<div class=""card""><div>GPU counter ticks</div><div class=""value"">" + result.Graphics.EngineSampleTicks + @"</div></div></div>
<p class=""muted"">Unity reports <code>" + HtmlEscape(result.Graphics.UnityDeviceVendor) + @" 0x" + result.Graphics.UnityDeviceVendorId.ToString("X4") + @":0x" + result.Graphics.UnityDeviceId.ToString("X4") + @"</code>, " + result.Graphics.UnityGraphicsMemoryMb + @" MB, multithreaded=" + result.Graphics.UnityGraphicsMultiThreaded + @". Pending-work target: <code>UnityPlayer.dll+0x" + Math.Max(0, result.Graphics.PendingWorkTargetAddress - result.Graphics.UnityPlayerBaseAddress).ToString("X") + @"</code>. GPU counter status: " + HtmlEscape(result.Graphics.EngineSamplerStatus) + @".</p>
<table><thead><tr><th>Index</th><th>DXGI adapter</th><th>Vendor:device</th><th>LUID</th><th>Matches Unity ID</th><th>SteamVR</th><th>Process GPU engine</th></tr></thead><tbody>" + adapterRows + @"</tbody></table>
<h2>General wait correlation</h2><p class=""muted""><a href=""wait-correlations.csv"">wait-correlations.csv</a> pairs ordinary statistical main-thread wait samples with a round-robin worker sampled on the same tick. Use the instrumented completion-wait files above for the verified graphics-frame fence. This general table remains useful for other waits, but it is correlation rather than proof of a dependency.</p>
<section id=""unityMarkers""><h2>Unity recorder markers</h2><div id=""markerContent""></div></section>
<script>
const cpu=" + cpuJson + @";
const phases=" + phasesJson + @";
const playerLoopPhases=" + playerLoopJson + @";
const markers=" + markersJson + @";
function makeNode(name,parent){return{name,parent,value:0,children:new Map()}}
function buildTree(role){const root=makeNode(role,null);cpu.filter(x=>x.thread===role).forEach(x=>{let n=root;n.value+=x.total;x.path.split(';').slice(2).forEach(part=>{if(!n.children.has(part))n.children.set(part,makeNode(part,n));n=n.children.get(part);n.value+=x.total})});return root}
const roots={'Main thread':buildTree('Main thread'),'Worker threads':buildTree('Worker threads')};
let role='Main thread',focus=roots[role];
function treeDepth(n){let depth=0;n.children.forEach(c=>{depth=Math.max(depth,1+treeDepth(c))});return depth}
function hue(name){let hash=0;for(let i=0;i<name.length;i++)hash=((hash<<5)-hash+name.charCodeAt(i))|0;return Math.abs(hash)%360}
function renderFlame(){const host=document.querySelector('#flame');host.textContent='';const depth=treeDepth(focus),row=29;host.style.height=((depth+1)*row)+'px';document.querySelector('#flamePath').textContent=pathOf(focus);paint(focus,0,100,0,depth,host)}
function paint(node,left,width,depth,maxDepth,host){if(width<0.08)return;const el=document.createElement('div');el.className='flame-node';el.style.left=left+'%';el.style.width=width+'%';el.style.top=((maxDepth-depth)*29)+'px';el.style.background='hsl('+hue(pathOf(node))+' 60% 39%)';el.textContent=width>2?node.name:'';el.title=pathOf(node)+' — '+node.value.toFixed(3)+' ms ('+width.toFixed(2)+'%)';if(node.children.size){el.onclick=()=>{focus=node;renderFlame()}}host.appendChild(el);let childLeft=left;[...node.children.values()].sort((a,b)=>b.value-a.value).forEach(child=>{const childWidth=width*child.value/node.value;paint(child,childLeft,childWidth,depth+1,maxDepth,host);childLeft+=childWidth})}
function pathOf(node){const parts=[];while(node){parts.unshift(node.name);node=node.parent}return parts.join(' › ')}
function selectRole(next){role=next;focus=roots[role];document.querySelector('#showMain').classList.toggle('active',role==='Main thread');document.querySelector('#showWorkers').classList.toggle('active',role==='Worker threads');renderFlame()}
document.querySelector('#showMain').onclick=()=>selectRole('Main thread');document.querySelector('#showWorkers').onclick=()=>selectRole('Worker threads');document.querySelector('#resetFlame').onclick=()=>{focus=roots[role];renderFlame()};
function renderPhases(target,key){const host=document.querySelector(target),max=Math.max(1,...phases.map(x=>x[key]));phases.filter(x=>x[key]>0).forEach((p,i)=>{const row=document.createElement('div');row.className='phase-row';const name=document.createElement('div');name.className='phase-name';name.textContent=p.name;name.title=p.name;const meter=document.createElement('div');meter.className='phase-meter';const fill=document.createElement('div');fill.className='phase-fill';fill.style.width=(p[key]/max*100)+'%';fill.style.background='hsl('+((i*53)%360)+' 58% 40%)';meter.appendChild(fill);const value=document.createElement('div');value.className='phase-value';value.textContent=p[key].toFixed(1)+' ms';row.append(name,meter,value);host.appendChild(row)})}
renderPhases('#mainPhases','main');renderPhases('#workerPhases','worker');
function renderExactPhases(){const host=document.querySelector('#exactPhases'),max=Math.max(0.001,...playerLoopPhases.map(x=>x.average));playerLoopPhases.forEach((p,i)=>{const row=document.createElement('div');row.className='phase-row';const name=document.createElement('div');name.className='phase-name';name.textContent=p.name;const meter=document.createElement('div');meter.className='phase-meter';const fill=document.createElement('div');fill.className='phase-fill';fill.style.width=(p.average/max*100)+'%';fill.style.background='hsl('+((i*67+18)%360)+' 60% 42%)';meter.appendChild(fill);const value=document.createElement('div');value.className='phase-value';value.textContent=p.average.toFixed(3)+' ms';value.title=p.total.toFixed(3)+' ms total';row.append(name,meter,value);host.appendChild(row)})}renderExactPhases();
const markerContent=document.querySelector('#markerContent');if(!markers.length){markerContent.className='empty';markerContent.textContent='Unavailable: Unity Recorder.Get is stripped in this BONEWORKS player. Sampled phases above are used instead.'}else{const chart=document.createElement('div');chart.className='chart';const max=Math.max(...markers.map(x=>x.total));markers.forEach((m,i)=>{const b=document.createElement('div');b.className='bar';b.style.width=Math.max(1,m.total/max*100)+'%';b.style.background='hsl('+((i*47)%360)+' 62% 42%)';b.textContent=m.name;b.title=m.name+': '+m.total.toFixed(3)+' ms';chart.appendChild(b)});markerContent.appendChild(chart)}
renderFlame();
</script></body></html>";
    File.WriteAllText(
        Path.Combine(result.Directory, "report.html"), html,
        new UTF8Encoding(false)
    );
  }

  private static List<CpuAggregate> AggregateCpuSamples(CaptureResult result) {
    var byPath = new Dictionary<string, CpuAggregate>();
    for (var i = 0; i < result.CpuSampleCount; i++) {
      var sample = result.CpuSamples[i];
      if (sample.IsWaitSnapshot)
        continue;
      var resolved = NativeMethodCatalog.Resolve(sample.InstructionPointer);
      var role = sample.IsMainThread ? "Main thread" : "Worker threads";
      var path = BuildSampleStack(result, sample, role);
      if (!byPath.TryGetValue(path, out var aggregate)) {
        aggregate = new CpuAggregate {
          Path = path,
          Name = resolved.DisplayName,
          ThreadRole = role,
        };
        byPath.Add(path, aggregate);
      }
      aggregate.Samples++;
      aggregate.EstimatedMilliseconds += sample.WeightMilliseconds;
    }
    return new List<CpuAggregate>(byPath.Values);
  }

  private static List<PhaseAggregate> AggregateSampledPhases(
      CaptureResult result
  ) {
    var byName = new Dictionary<string, PhaseAggregate>();
    for (var i = 0; i < result.CpuSampleCount; i++) {
      var sample = result.CpuSamples[i];
      if (sample.IsWaitSnapshot)
        continue;
      var resolved = NativeMethodCatalog.Resolve(sample.InstructionPointer);
      var name = ClassifyPhase(resolved);
      if (!byName.TryGetValue(name, out var phase)) {
        phase = new PhaseAggregate {Name = name};
        byName.Add(name, phase);
      }
      if (sample.IsMainThread)
        phase.MainMilliseconds += sample.WeightMilliseconds;
      else
        phase.WorkerMilliseconds += sample.WeightMilliseconds;
      phase.Samples++;
    }
    return new List<PhaseAggregate>(byName.Values);
  }

  private static string ClassifyPhase(ResolvedCpuSample sample) {
    var module = (sample.Module ?? "").ToLowerInvariant();
    var symbol = (
        (sample.Type ?? "") + "." + (sample.Method ?? "")
    ).ToLowerInvariant();

    if (module == "ntdll.dll" || module == "kernelbase.dll" ||
        module == "kernel32.dll")
      return "OS waits / synchronization";
    if (module.Contains("nvwgf") || module.Contains("d3d") ||
        module.Contains("openvr") || module.Contains("vrclient"))
      return "GPU / VR runtime";
    if (module.Contains("mono"))
      return "GC / managed runtime";
    if (sample.Method != null) {
      if (ContainsAny(
              symbol, "physics", "fixedupdate", "onfixed", "rigidbody",
              "collider", "joint", "puppet", "locomotion"
          ))
        return "Physics / fixed simulation";
      if (ContainsAny(
              symbol, "render", "camera", "cull", "shadow", "graphic",
              "material", "mesh"
          ))
        return "Rendering submission";
      if (ContainsAny(
              symbol, "input", "controller", "hand", "interaction",
              "button", "thumbstick"
          ))
        return "Input / interaction";
      return "Game scripts";
    }
    if (module == "unityplayer.dll")
      return "Unity engine (native)";
    if (module == "gameassembly.dll")
      return "Game code (unresolved)";
    return "Other native";
  }

  private static bool ContainsAny(string value, params string[] patterns) {
    for (var i = 0; i < patterns.Length; i++) {
      if (value.Contains(patterns[i]))
        return true;
    }
    return false;
  }

  private static string BuildFlamePath(
      string threadRole, PlayerLoopPhase phase, ResolvedCpuSample resolved
  ) {
    var prefix = "BONEWORKS;" + threadRole + ";" + phase;
    if (resolved.Method == null)
      return prefix + ";Native;" +
             (resolved.Module ?? "unknown");
    return prefix + ";" +
           (resolved.Assembly ?? "unknown assembly") + ";" +
           resolved.Type + ";" + resolved.Method;
  }

  private static string BuildSampleStack(
      CaptureResult result, CpuSample sample, string threadRole
  ) {
    if (sample.StackDepth == 0)
      return BuildFlamePath(
          threadRole, sample.PlayerLoopPhase,
          NativeMethodCatalog.Resolve(sample.InstructionPointer)
      );
    var stack = new StringBuilder("BONEWORKS;");
    stack.Append(threadRole).Append(';').Append(sample.PlayerLoopPhase);
    string previous = null;
    for (var i = sample.StackDepth - 1; i >= 0; i--) {
      var index = sample.StackOffset + i;
      if (index < 0 || index >= result.StackAddressCount)
        continue;
      var resolved = NativeMethodCatalog.Resolve(result.StackAddresses[index]);
      var name = resolved.DisplayName;
      if (name == previous)
        continue;
      previous = name;
      stack.Append(';').Append(
          name.Replace(';', ':').Replace('\r', ' ').Replace('\n', ' ')
      );
    }
    return stack.ToString();
  }

  private static string BuildExactAddressStack(
      CaptureResult result, CpuSample sample
  ) => BuildExactAddressStack(
      result.StackAddresses, result.StackAddressCount, sample.StackOffset,
      sample.StackDepth
  );

  private static string BuildExactAddressStack(
      long[] addresses, int addressCount, int offset, int depth
  ) {
    var stack = new StringBuilder();
    for (var i = depth - 1; i >= 0; i--) {
      var index = offset + i;
      if (index < 0 || index >= addressCount)
        continue;
      if (stack.Length > 0)
        stack.Append(';');
      stack.Append("0x").Append(addresses[index].ToString("X"));
    }
    return stack.ToString();
  }

  private static string BuildResolvedStack(
      long[] addresses, int addressCount, int offset, int depth,
      bool exactNativeOffsets
  ) {
    var stack = new StringBuilder();
    string previous = null;
    for (var i = depth - 1; i >= 0; i--) {
      var index = offset + i;
      if (index < 0 || index >= addressCount)
        continue;
      var resolved = NativeMethodCatalog.Resolve(addresses[index]);
      var name = exactNativeOffsets
          ? resolved.ExactDisplayName
          : resolved.DisplayName;
      if (name == previous)
        continue;
      previous = name;
      if (stack.Length > 0)
        stack.Append(';');
      stack.Append(
          name.Replace(';', ':').Replace('\r', ' ').Replace('\n', ' ')
      );
    }
    return stack.ToString();
  }

  private static string SanitizeFolded(string value) =>
      value.Replace('\r', ' ').Replace('\n', ' ');

  private sealed class CpuAggregate {
    public string Path;
    public string Name;
    public string ThreadRole;
    public int Samples;
    public double EstimatedMilliseconds;
  }

  private sealed class PhaseAggregate {
    public string Name;
    public int Samples;
    public double MainMilliseconds;
    public double WorkerMilliseconds;
  }

  private sealed class WaitCorrelation {
    public string MainStack;
    public string WorkerStack;
    public int Pairs;
    public double EstimatedMainWaitMilliseconds;
  }

  private static void Write(StreamWriter writer, double value) {
    writer.Write(',');
    writer.Write(Format(value));
  }

  private static string Format(double value) =>
      value.ToString("0.######", Invariant);

  private static void WriteCsvString(StreamWriter writer, string value) {
    writer.Write('"');
    writer.Write(value.Replace("\"", "\"\""));
    writer.Write('"');
  }

  private static string JsonEscape(string value) =>
      value.Replace("\\", "\\\\").Replace("\"", "\\\"")
          .Replace("\r", "\\r").Replace("\n", "\\n");

  private static string HtmlEscape(string value) =>
      value.Replace("&", "&amp;").Replace("<", "&lt;")
          .Replace(">", "&gt;").Replace("\"", "&quot;");
}
#endif

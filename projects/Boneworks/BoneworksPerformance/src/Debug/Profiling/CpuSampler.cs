#if DEBUG
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using MelonLoader;

namespace Sst.BoneworksPerformance;

internal sealed class CpuSampler {
  private const int ContextFlagsOffset = 48;
  private const int InstructionPointerOffset = 248;
  private const int ContextSize = 1232;
  private const int ContextAmd64Control = 0x00100001;
  private const int ContextAmd64Full = 0x0010000B;
  private const int StackPointerOffset = 152;
  private const int MaximumStackDepth = 32;
  private const uint ThreadGetContext = 0x0008;
  private const uint ThreadQueryInformation = 0x0040;
  private const uint ThreadQueryLimitedInformation = 0x0800;
  private const uint ThreadSuspendResume = 0x0002;
  private const uint SnapshotThreads = 0x00000004;

  private readonly int _mainThreadId;
  private readonly uint _processId;
  private readonly int _samplesPerSecond;
  private readonly bool _includeWorkers;
  private readonly CpuSample[] _samples;
  private readonly long[] _stackAddresses;
  private volatile bool _running;
  private Thread _thread;
  private int _sampleCount;
  private int[] _workerThreadIds = new int[0];
  private ulong[] _workerCycleCounts = new ulong[0];
  private readonly int[] _selectedWorkerIndices = {-1, -1, -1};
  private readonly ulong[] _selectedWorkerDeltas = new ulong[3];
  private readonly Dictionary<int, string> _threadNames =
      new Dictionary<int, string>();
  private int _nextWorker;
  private int _lastCompletionWaitId;
  private int _snapshottedCompletionWaitId;
  private int _stackAddressCount;
  private int _tick;
  private long _totalSamplingTicks;
  private long _totalSuspensionTicks;
  private long _maximumSuspensionTicks;

  public CpuSampler(
      int mainThreadId, CaptureMode mode, float maximumSeconds
  ) {
    _mainThreadId = mainThreadId;
    _processId = unchecked((uint)Process.GetCurrentProcess().Id);
    _includeWorkers = mode == CaptureMode.Detailed;
    _samplesPerSecond = _includeWorkers ? 250 : 200;
    // Detailed mode normally stores two samples per tick. During the
    // instrumented completion wait it can add three targeted worker stacks.
    var samplesPerTick = _includeWorkers ? 5 : 1;
    var seconds = Math.Max(1.0, Math.Min(maximumSeconds, 1800.0));
    _samples = new CpuSample[
        (int)Math.Ceiling(seconds * _samplesPerSecond * samplesPerTick)
    ];
    var requestedStackAddresses = _includeWorkers
        ? (long)_samples.Length * MaximumStackDepth
        : _samples.Length;
    _stackAddresses = new long[
        (int)Math.Min(requestedStackAddresses, 8L * 1024L * 1024L)
    ];
  }

  public CpuSample[] Samples => _samples;
  public int SampleCount => _sampleCount;
  public int SamplesPerSecond => _samplesPerSecond;
  public long[] StackAddresses => _stackAddresses;
  public int StackAddressCount => _stackAddressCount;
  public Dictionary<int, string> CopyThreadNames() =>
      new Dictionary<int, string>(_threadNames);
  public double AverageSamplingMicroseconds => _sampleCount == 0
      ? 0.0
      : _totalSamplingTicks * 1000000.0 /
        Stopwatch.Frequency / _sampleCount;
  public double AverageSuspensionMicroseconds => _sampleCount == 0
      ? 0.0
      : _totalSuspensionTicks * 1000000.0 /
        Stopwatch.Frequency / _sampleCount;
  public double MaximumSuspensionMicroseconds =>
      _maximumSuspensionTicks * 1000000.0 / Stopwatch.Frequency;

  public void Start() {
    if (_running)
      return;
    _running = true;
    _thread = new Thread(SampleLoop) {
      IsBackground = true,
      Name = BuildInfo.NAME + " CPU sampler",
      // A sampled thread must never remain suspended because the sampler was
      // descheduled. AboveNormal keeps the critical suspend/unwind/resume
      // window short; the sampler sleeps between samples.
      Priority = ThreadPriority.Highest,
    };
    _thread.Start();
  }

  public void Stop() {
    _running = false;
    if (_thread != null && !_thread.Join(2000))
      MelonLogger.Warning("CPU sampler did not stop within two seconds.");
    _thread = null;
  }

  public static int GetCurrentNativeThreadId() =>
      unchecked((int)NativeGetCurrentThreadId());

  private void SampleLoop() {
    var context = Marshal.AllocHGlobal(ContextSize);
    try {
      var period = Math.Max(1L, Stopwatch.Frequency / _samplesPerSecond);
      var nextTick = Stopwatch.GetTimestamp();
      var refreshAt = nextTick;
      while (_running && _sampleCount < _samples.Length) {
        var now = Stopwatch.GetTimestamp();
        if (_includeWorkers && now >= refreshAt) {
          RefreshWorkerThreads();
          refreshAt = now + Stopwatch.Frequency;
        }

        var tick = _tick++;
        var activeWaitId = _includeWorkers
            ? CompletionWaitProfiler.CurrentWaitId
            : 0;
        SampleThread(
            _mainThreadId, true, 1000.0 / _samplesPerSecond, context, tick,
            false, activeWaitId, 0
        );
        var waitId = _includeWorkers
            ? CompletionWaitProfiler.CurrentWaitId
            : 0;
        if (waitId > 0) {
          CaptureBusyWaitWorkers(context, tick, waitId);
        } else if (_includeWorkers && _workerThreadIds.Length > 0) {
          _lastCompletionWaitId = 0;
          _snapshottedCompletionWaitId = 0;
          if (_nextWorker >= _workerThreadIds.Length)
            _nextWorker = 0;
          var workerId = _workerThreadIds[_nextWorker++];
          SampleThread(
              workerId, false,
              1000.0 * _workerThreadIds.Length / _samplesPerSecond,
              context, tick, false, 0, 0
          );
        }

        nextTick += period;
        while (_running) {
          var remaining = nextTick - Stopwatch.GetTimestamp();
          if (remaining <= 0)
            break;
          if (remaining > Stopwatch.Frequency / 500)
            Thread.Sleep(1);
          else
            Thread.Sleep(0);
        }
      }
    } catch (Exception exception) {
      MelonLogger.Warning("CPU sampler stopped unexpectedly: " + exception);
    } finally {
      Marshal.FreeHGlobal(context);
    }
  }

  private void RefreshWorkerThreads() {
    var samplerThreadId = GetCurrentNativeThreadId();
    var threadIds = new int[256];
    var count = 0;
    var snapshot = CreateToolhelp32Snapshot(SnapshotThreads, 0);
    if (snapshot == new IntPtr(-1))
      return;
    try {
      var entry = new ThreadEntry32 {
        Size = (uint)Marshal.SizeOf(typeof(ThreadEntry32)),
      };
      if (!Thread32First(snapshot, ref entry))
        return;
      do {
        var id = unchecked((int)entry.ThreadId);
        if (entry.OwnerProcessId == _processId &&
            id != _mainThreadId && id != samplerThreadId &&
            !GraphicsProfiler.IsInternalThread(id) &&
            !PoseHistoryRecorder.IsInternalThread(id) &&
            !FixedUpdatePoseHistory.IsInternalThread(id)) {
          if (count == threadIds.Length)
            Array.Resize(ref threadIds, threadIds.Length * 2);
          threadIds[count++] = id;
        }
        entry.Size = (uint)Marshal.SizeOf(typeof(ThreadEntry32));
      } while (Thread32Next(snapshot, ref entry));
    } finally {
      CloseHandle(snapshot);
    }
    Array.Resize(ref threadIds, count);
    _workerThreadIds = threadIds;
    _workerCycleCounts = new ulong[count];
    for (var i = 0; i < count; i++) {
      TryReadThreadCycles(threadIds[i], out _workerCycleCounts[i]);
      if (!_threadNames.ContainsKey(threadIds[i]))
        _threadNames[threadIds[i]] = ReadThreadDescription(threadIds[i]);
    }
    _nextWorker = 0;
  }

  private void CaptureBusyWaitWorkers(
      IntPtr context, int tick, int waitId
  ) {
    const int maximumWorkers = 3;
    for (var slot = 0; slot < maximumWorkers; slot++) {
      _selectedWorkerIndices[slot] = -1;
      _selectedWorkerDeltas[slot] = 0;
    }
    var establishBaseline = waitId != _lastCompletionWaitId;
    _lastCompletionWaitId = waitId;
    for (var i = 0; i < _workerThreadIds.Length; i++) {
      if (!TryReadThreadCycles(_workerThreadIds[i], out var cycles))
        continue;
      var previous = i < _workerCycleCounts.Length
          ? _workerCycleCounts[i]
          : 0;
      var delta = cycles >= previous ? cycles - previous : 0;
      if (i < _workerCycleCounts.Length)
        _workerCycleCounts[i] = cycles;
      if (establishBaseline)
        continue;
      for (var slot = 0; slot < maximumWorkers; slot++) {
        if (delta <= _selectedWorkerDeltas[slot])
          continue;
        for (var move = maximumWorkers - 1; move > slot; move--) {
          _selectedWorkerDeltas[move] = _selectedWorkerDeltas[move - 1];
          _selectedWorkerIndices[move] = _selectedWorkerIndices[move - 1];
        }
        _selectedWorkerDeltas[slot] = delta;
        _selectedWorkerIndices[slot] = i;
        break;
      }
    }

    if (establishBaseline ||
        _snapshottedCompletionWaitId == waitId ||
        CompletionWaitProfiler.CurrentWaitId != waitId)
      return;
    _snapshottedCompletionWaitId = waitId;

    for (var slot = 0; slot < maximumWorkers; slot++) {
      if (CompletionWaitProfiler.CurrentWaitId != waitId)
        break;
      var index = _selectedWorkerIndices[slot];
      if (index < 0 || _selectedWorkerDeltas[slot] == 0)
        continue;
      SampleThread(
          _workerThreadIds[index], false, 0.0, context, tick, true,
          waitId, _selectedWorkerDeltas[slot]
      );
    }
  }

  private static bool TryReadThreadCycles(int threadId, out ulong cycles) {
    cycles = 0;
    var handle = OpenThread(
        ThreadQueryLimitedInformation, false, unchecked((uint)threadId)
    );
    if (handle == IntPtr.Zero)
      return false;
    try {
      return QueryThreadCycleTime(handle, out cycles);
    } finally {
      CloseHandle(handle);
    }
  }

  private static string ReadThreadDescription(int threadId) {
    var handle = OpenThread(
        ThreadQueryLimitedInformation, false, unchecked((uint)threadId)
    );
    if (handle == IntPtr.Zero)
      return "";
    IntPtr description = IntPtr.Zero;
    try {
      if (GetThreadDescription(handle, out description) != 0 ||
          description == IntPtr.Zero)
        return "";
      return Marshal.PtrToStringUni(description) ?? "";
    } catch (EntryPointNotFoundException) {
      return "";
    } finally {
      if (description != IntPtr.Zero)
        LocalFree(description);
      CloseHandle(handle);
    }
  }

  private void SampleThread(
      int threadId, bool isMainThread, double weightMilliseconds,
      IntPtr context, int tick, bool isWaitSnapshot, int waitId,
      ulong cycleDelta
  ) {
    if (_sampleCount >= _samples.Length ||
        threadId == GetCurrentNativeThreadId())
      return;

    var handle = OpenThread(
        ThreadGetContext | ThreadQueryInformation | ThreadSuspendResume,
        false, unchecked((uint)threadId)
    );
    if (handle == IntPtr.Zero)
      return;

    var sampleStarted = Stopwatch.GetTimestamp();
    var suspensionStarted = 0L;
    var suspended = false;
    try {
      if (SuspendThread(handle) == uint.MaxValue)
        return;
      suspended = true;
      suspensionStarted = Stopwatch.GetTimestamp();
      Marshal.WriteInt32(
          context, ContextFlagsOffset,
          _includeWorkers ? ContextAmd64Full : ContextAmd64Control
      );
      if (!GetThreadContext(handle, context))
        return;
      var instructionPointer = Marshal.ReadInt64(
          context, InstructionPointerOffset
      );
      var stackOffset = _stackAddressCount;
      var stackDepth = _includeWorkers
          ? CaptureStack(context)
          : CaptureLeaf(instructionPointer);
      _samples[_sampleCount++] = new CpuSample {
        Timestamp = Stopwatch.GetTimestamp(),
        ThreadId = threadId,
        Tick = tick,
        InstructionPointer = instructionPointer,
        StackOffset = stackOffset,
        StackDepth = unchecked((byte)stackDepth),
        WeightMilliseconds = weightMilliseconds,
        IsMainThread = isMainThread,
        IsWaitSnapshot = isWaitSnapshot,
        WaitId = waitId,
        CycleDelta = cycleDelta,
        PlayerLoopPhase = PerformanceCapture.CurrentPlayerLoopPhase,
      };
    } finally {
      if (suspended) {
        var suspendedFor = Stopwatch.GetTimestamp() - suspensionStarted;
        _totalSuspensionTicks += suspendedFor;
        if (suspendedFor > _maximumSuspensionTicks)
          _maximumSuspensionTicks = suspendedFor;
        ResumeThread(handle);
      }
      CloseHandle(handle);
      _totalSamplingTicks += Stopwatch.GetTimestamp() - sampleStarted;
    }
  }

  private int CaptureLeaf(long instructionPointer) {
    if (_stackAddressCount >= _stackAddresses.Length)
      return 0;
    _stackAddresses[_stackAddressCount++] = instructionPointer;
    return 1;
  }

  private int CaptureStack(IntPtr context) {
    if (_stackAddressCount + MaximumStackDepth > _stackAddresses.Length)
      return 0;
    var start = _stackAddressCount;
    var previousInstruction = 0L;
    for (var depth = 0; depth < MaximumStackDepth; depth++) {
      var instruction = Marshal.ReadInt64(context, InstructionPointerOffset);
      if (instruction == 0 || instruction == previousInstruction)
        break;
      _stackAddresses[_stackAddressCount++] = instruction;
      previousInstruction = instruction;

      var functionEntry = RtlLookupFunctionEntry(
          unchecked((ulong)instruction), out var imageBase, IntPtr.Zero
      );
      if (functionEntry != IntPtr.Zero) {
        RtlVirtualUnwind(
            0, imageBase, unchecked((ulong)instruction), functionEntry,
            context, out _, out _, IntPtr.Zero
        );
      } else {
        var stackPointer = Marshal.ReadInt64(context, StackPointerOffset);
        if (stackPointer < 0x10000 ||
            !ReadProcessMemory(
                new IntPtr(-1), new IntPtr(stackPointer),
                out var returnAddress, new IntPtr(8), out var bytesRead
            ) || bytesRead.ToInt64() != 8)
          break;
        Marshal.WriteInt64(
            context, StackPointerOffset, stackPointer + 8
        );
        Marshal.WriteInt64(
            context, InstructionPointerOffset,
            unchecked((long)returnAddress)
        );
      }
    }
    return _stackAddressCount - start;
  }

  [DllImport("kernel32.dll", EntryPoint = "GetCurrentThreadId")]
  private static extern uint NativeGetCurrentThreadId();

  [DllImport("kernel32.dll", SetLastError = true)]
  private static extern IntPtr OpenThread(
      uint desiredAccess, bool inheritHandle, uint threadId
  );

  [DllImport("kernel32.dll")]
  private static extern uint SuspendThread(IntPtr thread);

  [DllImport("kernel32.dll")]
  private static extern uint ResumeThread(IntPtr thread);

  [DllImport("kernel32.dll", SetLastError = true)]
  private static extern bool GetThreadContext(IntPtr thread, IntPtr context);

  [DllImport("kernel32.dll", SetLastError = true)]
  private static extern bool ReadProcessMemory(
      IntPtr process, IntPtr address, out ulong buffer, IntPtr size,
      out IntPtr bytesRead
  );

  [DllImport("ntdll.dll")]
  private static extern IntPtr RtlLookupFunctionEntry(
      ulong controlPc, out ulong imageBase, IntPtr historyTable
  );

  [DllImport("ntdll.dll")]
  private static extern IntPtr RtlVirtualUnwind(
      uint handlerType, ulong imageBase, ulong controlPc,
      IntPtr functionEntry, IntPtr contextRecord, out IntPtr handlerData,
      out ulong establisherFrame, IntPtr contextPointers
  );

  [DllImport("kernel32.dll")]
  private static extern bool CloseHandle(IntPtr handle);

  [DllImport("kernel32.dll")]
  private static extern bool QueryThreadCycleTime(
      IntPtr thread, out ulong cycleTime
  );

  [DllImport("kernel32.dll")]
  private static extern int GetThreadDescription(
      IntPtr thread, out IntPtr description
  );

  [DllImport("kernel32.dll")]
  private static extern IntPtr LocalFree(IntPtr memory);

  [DllImport("kernel32.dll", SetLastError = true)]
  private static extern IntPtr CreateToolhelp32Snapshot(
      uint flags, uint processId
  );

  [DllImport("kernel32.dll", SetLastError = true)]
  private static extern bool Thread32First(
      IntPtr snapshot, ref ThreadEntry32 entry
  );

  [DllImport("kernel32.dll", SetLastError = true)]
  private static extern bool Thread32Next(
      IntPtr snapshot, ref ThreadEntry32 entry
  );

  [StructLayout(LayoutKind.Sequential)]
  private struct ThreadEntry32 {
    public uint Size;
    public uint Usage;
    public uint ThreadId;
    public uint OwnerProcessId;
    public int BasePriority;
    public int PriorityDelta;
    public uint Flags;
  }
}
#endif

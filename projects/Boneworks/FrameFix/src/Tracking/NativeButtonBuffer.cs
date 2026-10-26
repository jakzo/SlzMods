using System;
using System.IO.MemoryMappedFiles;
using System.Threading;

namespace Sst.BoneworksFrameFix;

// One writer in the isolated OpenVR client, one reader in the game. Timestamps
// are native Windows QPC, converted to the game's clock by NativeInputClock.
internal sealed class NativeButtonBuffer : IDisposable
{
  public const int Capacity = 4096;
  private const int SlotSize = 40;
  private const int Size = 8 + Capacity * SlotSize;
  private readonly MemoryMappedFile _mapping;
  private readonly MemoryMappedViewAccessor _view;
  private long _read;

  public NativeButtonBuffer(string name, bool create)
  {
    _mapping = create ? MemoryMappedFile.CreateNew(name, Size)
        : MemoryMappedFile.OpenExisting(name);
    _view = _mapping.CreateViewAccessor(0, Size);
  }

  public void SkipToLatest() => _read = _view.ReadInt64(0);

  public void Publish(long timestamp, ulong left, ulong right, int valid)
  {
    var sequence = _view.ReadInt64(0) + 1;
    var offset = 8 + (sequence - 1) % Capacity * SlotSize;
    _view.Write(offset, -sequence);
    Thread.MemoryBarrier();
    _view.Write(offset + 8, timestamp);
    _view.Write(offset + 16, left);
    _view.Write(offset + 24, right);
    _view.Write(offset + 32, valid);
    Thread.MemoryBarrier();
    _view.Write(offset, sequence);
    Thread.MemoryBarrier();
    _view.Write(0, sequence);
  }

  public bool TryRead(out long timestamp, out ulong left, out ulong right,
      out int valid, out bool lost)
  {
    timestamp = 0;
    left = right = 0;
    valid = 0;
    var latest = _view.ReadInt64(0);
    lost = latest - _read > Capacity;
    var sequence = lost ? latest - Capacity + 1 : _read + 1;
    if (sequence > latest)
      return false;
    var offset = 8 + (sequence - 1) % Capacity * SlotSize;
    if (_view.ReadInt64(offset) != sequence)
      return false;
    Thread.MemoryBarrier();
    timestamp = _view.ReadInt64(offset + 8);
    left = _view.ReadUInt64(offset + 16);
    right = _view.ReadUInt64(offset + 24);
    valid = _view.ReadInt32(offset + 32);
    Thread.MemoryBarrier();
    if (_view.ReadInt64(offset) != sequence)
      return false;
    _read = sequence;
    return true;
  }

  public void Dispose()
  {
    _view.Dispose();
    _mapping.Dispose();
  }
}

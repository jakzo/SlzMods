using System;
using System.Numerics;

namespace Sst.HandTracking;

public class RollingFft {
  public readonly int BufferSize;

  private readonly int _bitSize;
  private readonly Complex[] _result;
  private readonly Complex[] _buffer;
  private readonly Complex[] _twiddleFactors;
  private int _bufferIndex = 0;

  public RollingFft(int bufferSize) {
    BufferSize = bufferSize;
    _bitSize = (int)Math.Log(BufferSize, 2);
    _result = new Complex[BufferSize];
    _buffer = new Complex[BufferSize];
    _twiddleFactors = new Complex[BufferSize];
    for (var i = 0; i < _twiddleFactors.Length; i++) {
      var angle = -2.0 * Math.PI * i / _twiddleFactors.Length;
      _twiddleFactors[i] = new Complex(Math.Cos(angle), Math.Sin(angle));
    }
  }

  public void Reset() {
    Array.Clear(_buffer, 0, _buffer.Length);
    _bufferIndex = 0;
  }

  public Complex[] Add(float newValue) {
    _buffer[_bufferIndex] = new Complex(newValue, 0);
    _bufferIndex = (_bufferIndex + 1) % _buffer.Length;
    for (int i = 0; i < _buffer.Length; i++) {
      _result[i] = _buffer[(_bufferIndex + i) % _buffer.Length];
    }
    FFT();
    return _result;
  }

  private void FFT() {
    for (var i = 0; i < BufferSize; i++) {
      var j = BitReverse(i, _bitSize);
      if (j > i) {
        var temp = _result[i];
        _result[i] = _result[j];
        _result[j] = temp;
      }
    }

    // Danielson-Lanczos
    for (var s = 1; s <= _bitSize; s++) {
      var m1 = 1 << s;
      var m2 = m1 >> 1;
      var wm = _twiddleFactors[BufferSize / m1];

      for (var k = 0; k < BufferSize; k += m1) {
        var w = Complex.One;
        for (var j = 0; j < m2; j++) {
          var t = w * _result[k + j + m2];
          var u = _result[k + j];
          _result[k + j] = u + t;
          _result[k + j + m2] = u - t;
          w *= wm;
        }
      }
    }
  }

  private int BitReverse(int value, int bits) {
    var result = 0;
    for (var i = 0; i < bits; i++) {
      result = (result << 1) | (value & 1);
      value >>= 1;
    }
    return result;
  }
}

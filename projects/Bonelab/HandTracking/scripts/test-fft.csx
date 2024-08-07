#r "System"
#r "System.Numerics"
using System;
using System.Diagnostics;
using System.Numerics;
using System.Linq;

public class RollingFft {
  private readonly int _bufferSize;
  private readonly int _bitSize;
  private readonly Complex[] _result;
  private readonly Complex[] _buffer;
  private readonly Complex[] _twiddleFactors;
  private int _bufferIndex = 0;

  public RollingFft(int bufferSize) {
    _bufferSize = bufferSize;
    _bitSize = (int)Math.Log(_bufferSize, 2);
    _result = new Complex[_bufferSize];
    _buffer = new Complex[_bufferSize];
    _twiddleFactors = new Complex[_bufferSize];
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
    for (var i = 0; i < _bufferSize; i++) {
      var j = BitReverse(i, _bitSize);
      if (j > i) {
        var temp = _result[i];
        _result[i] = _result[j];
        _result[j] = temp;
      }
    }

    // Danielson-Lanczos section
    for (var s = 1; s <= _bitSize; s++) {
      var m1 = 1 << s;
      var m2 = m1 >> 1;
      var wm = _twiddleFactors[_bufferSize / m1];

      for (var k = 0; k < _bufferSize; k += m1) {
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

static class SimpleFft {
  public static Complex[] Compute(float[] input) {
    return Compute(input.Select(n => new Complex(n, 0)).ToArray());
  }

  public static Complex[] Compute(Complex[] input) {
    var n = input.Length;
    if (n <= 1)
      return input;

    if ((n & (n - 1)) != 0)
      throw new ArgumentException("Input array length must be a power of 2.");

    var even = new Complex[n / 2];
    var odd = new Complex[n / 2];
    for (var i = 0; i < n / 2; i++) {
      even[i] = input[i * 2];
      odd[i] = input[i * 2 + 1];
    }

    var fftEven = Compute(even);
    var fftOdd = Compute(odd);

    var output = new Complex[n];
    for (var k = 0; k < n / 2; k++) {
      Complex t =
          Complex.FromPolarCoordinates(1.0, -2.0 * Math.PI * k / n) * fftOdd[k];
      output[k] = fftEven[k] + t;
      output[k + n / 2] = fftEven[k] - t;
    }

    return output;
  }
}

void Benchmark(string name, Action action) {
  const int warmupIterations = 10;
  var targetDurationTicks = TimeSpan.FromMilliseconds(500).Ticks;
  var elapsedTimes = new long[1000];
  var stopwatch = new Stopwatch();

  for (int i = 0; i < warmupIterations; i++) {
    action();
  }

  int iterations = 0;
  stopwatch.Start();
  while (stopwatch.ElapsedTicks < targetDurationTicks &&
         iterations < elapsedTimes.Length) {
    var start = stopwatch.ElapsedTicks;
    action();
    var end = stopwatch.ElapsedTicks;
    elapsedTimes[iterations++] = end - start;
  }
  stopwatch.Stop();

  if (iterations == 0) {
    Console.WriteLine(
        $"{name}: Action did not complete within the target duration."
    );
    return;
  }

  Array.Resize(ref elapsedTimes, iterations);

  Array.Sort(elapsedTimes);

  string ToMs(double percentile) =>
      (elapsedTimes[(int)(percentile * iterations)] / 10_000.0).ToString("N2");
  Console.WriteLine(
      $"{name}: P1={ToMs(0.01)}ms, P50={ToMs(0.50)}ms, P99={ToMs(0.99)}ms"
  );
}

var FFT_BUFFER_SIZE = 64;
var FFT_REFRESH_RATE = 90f;
var FFT_SAMPLE_DURATION = 1f / FFT_REFRESH_RATE;
var FFT_HALF_SAMPLE_DURATION = FFT_SAMPLE_DURATION / 2f;
var FFT_WINDOW_DURATION = FFT_SAMPLE_DURATION * FFT_BUFFER_SIZE; // 0.71 seconds
var HEAD_BOB_MIN_DURATION = 0.1f;
var HEAD_BOB_MAX_DURATION = 0.6f;
var HEAD_BOB_MIN_FFT_BIN =
    (int)Math.Round(1f / (HEAD_BOB_MAX_DURATION / FFT_WINDOW_DURATION));
var HEAD_BOB_MAX_FFT_BIN =
    (int)Math.Round(1f / (HEAD_BOB_MIN_DURATION / FFT_WINDOW_DURATION));
private float? CalculateAddedRunningScoreFft(int i) {
  if (i >= HEAD_BOB_MIN_FFT_BIN && i <= HEAD_BOB_MAX_FFT_BIN) {
    var binDuration = FFT_WINDOW_DURATION / i;
    var binAmplitude = (float)res[i].Magnitude / FFT_WINDOW_DURATION;
    var binThreshold = (float)Math.Pow(binDuration * 2f, 2f) + 0.5f;
    // Console.WriteLine(
    //     "bin " + i + " threshold = " + binThreshold.ToString("N2") +
    //     " amp = " + binAmplitude
    // );
    if (binAmplitude > binThreshold)
      return (binAmplitude - binThreshold) * binDuration * 8f;
    return 0f;
  }

  // TODO: Sigmoid activation
  return null;
}
// Console.WriteLine(HEAD_BOB_MIN_FFT_BIN);
// Console.WriteLine(HEAD_BOB_MAX_FFT_BIN);

var samples = File.ReadAllLines("hmd-y-recording.csv")
                  .Where(line => line.Length > 0)
                  .Select(line => {
                    var arr = line.Split(",").Select(float.Parse).ToArray();
                    return (arr[0], arr[1]);
                  })
                  .ToArray();
var refreshRate = FFT_REFRESH_RATE;
var targetStart = 23f;
// var targetSize = 1f;
// var bufferSize = 1 << (int)Math.Ceiling(Math.Log(targetSize * refreshRate,
// 2));
var bufferSize = FFT_BUFFER_SIZE;
var buffer = samples.SkipWhile(x => x.Item1 < targetStart)
                 .Take(bufferSize)
                 .Select(x => x.Item2)
                 .ToArray();
for (var i = 0; i < buffer.Length; i++) {
  buffer[i] = buffer[i & ~1];
}
var avg = buffer.Sum() / buffer.Length;
var bufferFixed = buffer.Select(x => x - avg).ToArray();

Complex[] res;

// Test that FFT implementations work
var fft2 = new RollingFft(buffer.Length);
var a = SimpleFft.Compute(bufferFixed);
foreach (var x in bufferFixed) {
  res = fft2.Add(x);
}
const double EPSILON = 1e-9;
for (var i = 0; i < res.Length; i++) {
  if (Math.Abs(res[i].Magnitude - a[i].Magnitude) > EPSILON) {
    throw new Exception("FFT results are not equal");
  }
}

// Print frequencies
for (var i = 0; i <= res.Length / 2; i++) {
  var index = i.ToString().PadLeft(res.Length.ToString().Length, ' ');
  var duration = res.Length / (double)i / refreshRate;
  var frequency = (1f / duration).ToString("N1");
  var value = res[i].Magnitude.ToString("N2");
  var resStr = CalculateAddedRunningScoreFft(i)?.ToString("N2") ?? "";
  Console.WriteLine(
      $"{index}: {value} ({duration.ToString("N3")}s, {frequency}hz) {resStr}"
  );
}

// Curve
// public static float Sigmoid(float x, float midpoint, float steepness) {
//   return 1.0f / (1.0f + (float)Math.Exp(-steepness * (x - midpoint)));
// }

// public static float
// BellCurve(float value, float x, float y, float steepness = 10.0f) {
//   float lowerBound = x - (y - x) / 2;
//   float upperBound = y + (y - x) / 2;

//   float left = Sigmoid(value, lowerBound, steepness);
//   float right = Sigmoid(value, upperBound, -steepness);

//   return left * right;
// }
// Console.WriteLine(BellCurve(0.1f, 0.2f, 0.6f, 0.001f));

// Benchmarks
// Benchmark("simple", () => {
//   var simpleFftResult = SimpleFft.Compute(bufferFixed);
// });
// var fft = new RollingFft(buffer.Length);
// Benchmark("rolling 1", () => { res = fft.Add(bufferFixed[0]); });
// Benchmark("rolling 256", () => {
//   foreach (var x in bufferFixed) {
//     res = fft.Add(x);
//   }
// });

// Show bit reverse step
// private int BitReverse(int value, int bits) {
//   var result = 0;
//   for (var i = 0; i < bits; i++) {
//     result = (result << 1) | (value & 1);
//     value >>= 1;
//   }
//   return result;
// }
// var test = new int[16];
// var bitLen = (int)Math.Log(test.Length, 2);
// for (var i = 0; i < test.Length; i++)
//   test[i] = i;
// for (var i = 0; i < test.Length; i++) {
//   var j = BitReverse(i, bitLen);
//   if (j > i) {
//     var temp = test[i];
//     test[i] = test[j];
//     test[j] = temp;
//   }
// }
// int i = 0;
// foreach (var c in test) {
//   Console.WriteLine(i++ + ": " + c);
// }

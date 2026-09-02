#if DEBUG
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using MelonLoader;
using UnhollowerBaseLib;

namespace Sst.BoneworksPerformance;

internal sealed class NativeMethodSymbol {
  public long Address;
  public string AssemblyName;
  public string TypeName;
  public string MethodName;

  public string DisplayName => TypeName + "." + MethodName;
}

internal sealed class NativeModuleRange {
  public long Start;
  public long End;
  public string Name;
}

internal sealed class ResolvedCpuSample {
  public string Module;
  public string Assembly;
  public string Type;
  public string Method;
  public long NativeOffset;

  public string DisplayName => Method == null
      ? Module + (NativeOffset == 0
          ? " (native or unresolved)"
          : "+0x" + (NativeOffset & ~0xFFL).ToString("X"))
      : Type + "." + Method;

  public string ExactDisplayName => Method == null
      ? Module + (NativeOffset == 0
          ? " (native or unresolved)"
          : "+0x" + NativeOffset.ToString("X"))
      : Type + "." + Method;
}

internal static class NativeMethodCatalog {
  private static readonly object Sync = new object();
  private static volatile bool _building;
  private static volatile bool _complete;
  private static NativeMethodSymbol[] _symbols = new NativeMethodSymbol[0];
  private static NativeModuleRange[] _modules = new NativeModuleRange[0];
  private static int _dumperSymbolCount;
  private static readonly Dictionary<long, ResolvedCpuSample> ResolutionCache =
      new Dictionary<long, ResolvedCpuSample>();

  public static int SymbolCount => _symbols.Length;
  public static int DumperSymbolCount => _dumperSymbolCount;
  public static bool IsComplete => _complete;

  public static void BeginBuild(string il2CppDumperScriptPath) {
    lock (Sync) {
      if (_building || _complete)
        return;
      _building = true;
    }
    Build(il2CppDumperScriptPath);
  }

  public static ResolvedCpuSample Resolve(long instructionPointer) {
    var module = FindModule(instructionPointer);
    lock (Sync) {
      if (ResolutionCache.TryGetValue(instructionPointer, out var cached))
        return cached;
    }

    ResolvedCpuSample result;
    if (module == null) {
      result = new ResolvedCpuSample {
        Module = "unknown",
      };
    } else if (!string.Equals(
                   module.Name, "GameAssembly.dll",
                   StringComparison.OrdinalIgnoreCase
               )) {
      if (NativePdbSymbolResolver.TryResolve(
              instructionPointer, module, out var symbolName
          )) {
        result = new ResolvedCpuSample {
          Module = module.Name,
          Assembly = "Native PDB",
          Type = Path.GetFileNameWithoutExtension(module.Name),
          Method = symbolName,
        };
      } else {
        result = new ResolvedCpuSample {
        Module = module == null ? "unknown" : module.Name,
        NativeOffset = module == null ? 0 : instructionPointer - module.Start,
      };
      }
    } else {
      result = ResolveGameAssembly(instructionPointer, module);
    }

    lock (Sync)
      ResolutionCache[instructionPointer] = result;
    return result;
  }

  private static ResolvedCpuSample ResolveGameAssembly(
      long instructionPointer, NativeModuleRange module
  ) {

    var symbols = _symbols;
    var low = 0;
    var high = symbols.Length - 1;
    var found = -1;
    while (low <= high) {
      var middle = low + ((high - low) >> 1);
      if (symbols[middle].Address <= instructionPointer) {
        found = middle;
        low = middle + 1;
      } else {
        high = middle - 1;
      }
    }
    if (found < 0)
      return new ResolvedCpuSample {
        Module = module.Name,
        NativeOffset = instructionPointer - module.Start,
      };

    var symbol = symbols[found];
    var nextAddress = found + 1 < symbols.Length
        ? symbols[found + 1].Address
        : module.End;
    if (instructionPointer >= nextAddress ||
        instructionPointer - symbol.Address > 65536)
      return new ResolvedCpuSample {
        Module = module.Name,
        NativeOffset = instructionPointer - module.Start,
      };
    return new ResolvedCpuSample {
      Module = module.Name,
      Assembly = symbol.AssemblyName,
      Type = symbol.TypeName,
      Method = symbol.MethodName,
    };
  }

  private static void Build(string il2CppDumperScriptPath) {
    var stopwatch = Stopwatch.StartNew();
    var byAddress = new Dictionary<long, NativeMethodSymbol>();
    try {
      foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies()) {
        Type[] types;
        try {
          types = assembly.GetTypes();
        } catch (ReflectionTypeLoadException exception) {
          types = exception.Types;
        } catch {
          continue;
        }
        if (types == null)
          continue;

        foreach (var type in types) {
          if (type == null)
            continue;
          MethodBase[] methods;
          try {
            var flags = BindingFlags.DeclaredOnly | BindingFlags.Instance |
                        BindingFlags.Static | BindingFlags.Public |
                        BindingFlags.NonPublic;
            var ordinary = type.GetMethods(flags);
            var constructors = type.GetConstructors(flags);
            methods = new MethodBase[ordinary.Length + constructors.Length];
            Array.Copy(ordinary, methods, ordinary.Length);
            Array.Copy(
                constructors, 0, methods, ordinary.Length,
                constructors.Length
            );
          } catch {
            continue;
          }

          foreach (var method in methods) {
            try {
              var field = UnhollowerUtils
                  .GetIl2CppMethodInfoPointerFieldForGeneratedMethod(method);
              if (field == null)
                continue;
              var methodInfo = (IntPtr)field.GetValue(null);
              if (methodInfo == IntPtr.Zero)
                continue;
              var nativeAddress = Marshal.ReadIntPtr(methodInfo).ToInt64();
              if (nativeAddress == 0 || byAddress.ContainsKey(nativeAddress))
                continue;
              byAddress.Add(nativeAddress, new NativeMethodSymbol {
                Address = nativeAddress,
                AssemblyName = assembly.GetName().Name,
                TypeName = type.FullName ?? type.Name,
                MethodName = FormatMethod(method),
              });
            } catch {
              // Ordinary managed methods do not have IL2CPP metadata fields.
            }
          }
        }
      }

      _modules = ReadModules();
      var gameAssembly = FindModule("GameAssembly.dll");
      var dumpCount = gameAssembly == null
          ? 0
          : AddIl2CppDumperSymbols(
              byAddress, gameAssembly.Start, il2CppDumperScriptPath
          );
      _dumperSymbolCount = dumpCount;

      var symbols = new NativeMethodSymbol[byAddress.Count];
      byAddress.Values.CopyTo(symbols, 0);
      Array.Sort(symbols, (left, right) => left.Address.CompareTo(right.Address));
      _symbols = symbols;
      NativePdbSymbolResolver.Initialize(_modules);
      _complete = true;
      MelonLogger.Msg(
          $"Catalogued {symbols.Length} native IL2CPP method addresses " +
          $"({dumpCount} from IL2CppDumper) in " +
          $"{stopwatch.Elapsed.TotalSeconds:0.0} seconds."
      );
    } catch (Exception exception) {
      MelonLogger.Warning("Could not build IL2CPP method catalog: " + exception);
    } finally {
      _building = false;
    }
  }

  private static int AddIl2CppDumperSymbols(
      Dictionary<long, NativeMethodSymbol> byAddress, long moduleBase,
      string scriptPath
  ) {
    if (string.IsNullOrWhiteSpace(scriptPath) || !File.Exists(scriptPath)) {
      MelonLogger.Warning(
          "IL2CppDumper script.json was not found; runtime wrapper symbols " +
          "will still be used."
      );
      return 0;
    }

    var count = 0;
    long relativeAddress = -1;
    try {
      using (var reader = new StreamReader(scriptPath)) {
        string line;
        while ((line = reader.ReadLine()) != null) {
          var trimmed = line.Trim();
          if (trimmed.StartsWith("\"Address\":", StringComparison.Ordinal)) {
            var number = trimmed.Substring(10).Trim().TrimEnd(',');
            long.TryParse(
                number, NumberStyles.Integer, CultureInfo.InvariantCulture,
                out relativeAddress
            );
            continue;
          }
          if (relativeAddress < 0 || !trimmed.StartsWith(
                  "\"Name\":", StringComparison.Ordinal
              ))
            continue;

          var name = ReadJsonString(trimmed.Substring(7).Trim());
          var address = moduleBase + relativeAddress;
          relativeAddress = -1;
          if (name.Length == 0 || byAddress.ContainsKey(address))
            continue;
          var separator = name.LastIndexOf("$$", StringComparison.Ordinal);
          byAddress.Add(address, new NativeMethodSymbol {
            Address = address,
            AssemblyName = "IL2CPP dump",
            TypeName = separator > 0 ? name.Substring(0, separator) : "IL2CPP",
            MethodName = separator > 0 ? name.Substring(separator + 2) : name,
          });
          count++;
        }
      }
    } catch (Exception exception) {
      MelonLogger.Warning(
          "Could not read IL2CppDumper symbols: " + exception.Message
      );
    }
    return count;
  }

  private static string ReadJsonString(string value) {
    if (value.EndsWith(",", StringComparison.Ordinal))
      value = value.Substring(0, value.Length - 1).TrimEnd();
    if (value.Length < 2 || value[0] != '"' ||
        value[value.Length - 1] != '"')
      return "";
    var output = new System.Text.StringBuilder(value.Length - 2);
    for (var i = 1; i < value.Length - 1; i++) {
      var character = value[i];
      if (character != '\\' || i + 1 >= value.Length - 1) {
        output.Append(character);
        continue;
      }
      var escaped = value[++i];
      if (escaped == 'u' && i + 4 < value.Length) {
        if (ushort.TryParse(
                value.Substring(i + 1, 4), NumberStyles.HexNumber,
                CultureInfo.InvariantCulture, out var codePoint
            )) {
          output.Append((char)codePoint);
          i += 4;
        }
      } else {
        switch (escaped) {
          case 'n': output.Append('\n'); break;
          case 'r': output.Append('\r'); break;
          case 't': output.Append('\t'); break;
          default: output.Append(escaped); break;
        }
      }
    }
    return output.ToString();
  }

  private static string FormatMethod(MethodBase method) {
    var parameters = method.GetParameters();
    var text = method.Name + "(";
    for (var i = 0; i < parameters.Length; i++) {
      if (i > 0)
        text += ",";
      text += parameters[i].ParameterType.Name;
    }
    return text + ")";
  }

  private static NativeModuleRange[] ReadModules() {
    var ranges = new List<NativeModuleRange>();
    try {
      using (var process = Process.GetCurrentProcess()) {
        foreach (ProcessModule module in process.Modules) {
          ranges.Add(new NativeModuleRange {
            Start = module.BaseAddress.ToInt64(),
            End = module.BaseAddress.ToInt64() + module.ModuleMemorySize,
            Name = module.ModuleName,
          });
          module.Dispose();
        }
      }
    } catch {
      // A module can unload while the snapshot is being read.
    }
    return ranges.ToArray();
  }

  private static NativeModuleRange FindModule(long address) {
    var modules = _modules;
    for (var i = 0; i < modules.Length; i++) {
      if (address >= modules[i].Start && address < modules[i].End)
        return modules[i];
    }
    return null;
  }

  private static NativeModuleRange FindModule(string name) {
    var modules = _modules;
    for (var i = 0; i < modules.Length; i++) {
      if (string.Equals(modules[i].Name, name, StringComparison.OrdinalIgnoreCase))
        return modules[i];
    }
    return null;
  }
}
#endif

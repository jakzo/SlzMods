using System;
using System.IO;
using System.Reflection;
using Sst;

namespace Jakzo.Testing;

public static class HotReload {
  // private static string _assemblyPath;
  // private static AssemblyLoadContext _loadContext;

  // public static void Init() {
  //   var assemblyPath = Assembly.GetExecutingAssembly().Location;

  //   var watcher = new FileSystemWatcher() {
  //     Path = Path.GetDirectoryName(assemblyPath),
  //     Filter = Path.GetFileName(assemblyPath),
  //     NotifyFilter =
  //         NotifyFilters.LastWrite | NotifyFilters.FileName |
  //         NotifyFilters.Size
  //   };

  //   watcher.Changed += OnChanged;
  //   watcher.Created += OnChanged;
  //   watcher.EnableRaisingEvents = true;
  // }

  // private static void OnChanged(object source, FileSystemEventArgs e) {
  //   Dbg.Log($"Mod assembly file changed: {e.FullPath} {e.ChangeType}");

  //   if (_loadContext != null) {
  //     CallDeinit();

  //     _loadContext.Unload();
  //     _loadContext = null;

  //     GC.Collect();
  //     GC.WaitForPendingFinalizers();
  //   }

  //   _loadContext = new AssemblyLoadContext("FeatureLoadContext", true);
  //   Assembly assembly = _loadContext.LoadFromAssemblyPath(_assemblyPath);

  //   // Create and initialize the new feature instance
  //   CreateAndInitNewFeatureInstance(assembly);
  // }

  // private void CallDeinit() {
  //   if (_featureType != null && _featureType.GetField("Instance") != null) {
  //     var instance =
  //         _featureType
  //             .GetField("Instance", BindingFlags.Public |
  //             BindingFlags.Static)
  //             ?.GetValue(null);
  //     _featureType.GetMethod("Deinit")?.Invoke(instance, null);
  //   }
  // }

  // private void CreateAndInitNewFeatureInstance(Assembly assembly) {
  //   var newFeatureType = assembly.GetType(_featureType.FullName);

  //   if (newFeatureType != null) {
  //     // Create a new instance
  //     var newInstance = Activator.CreateInstance(newFeatureType);

  //     // Set the static Instance field
  //     newFeatureType
  //         .GetField("Instance", BindingFlags.Public | BindingFlags.Static)
  //         ?.SetValue(null, newInstance);

  //     // Call Init on the new instance
  //     newFeatureType.GetMethod("Init")?.Invoke(newInstance, null);
  //   }
  // }
}

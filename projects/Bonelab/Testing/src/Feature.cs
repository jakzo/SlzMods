using System;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using MelonLoader;
using Sst;

namespace Jakzo.Testing;

public abstract class Feature<T>
    where T : Feature<T> {
  public static T Instance;
  public abstract void Init();
  public abstract void Deinit();
}

public static class Features {
  private static string STATIC_CLASS_NAME = "J";

  public static Type Init() {
    var assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(
        new AssemblyName("TestingFeaturesAssembly"), AssemblyBuilderAccess.Run
    );
    var moduleBuilder = assemblyBuilder.DefineDynamicModule("MainModule");

    var typeBuilder = moduleBuilder.DefineType(
        STATIC_CLASS_NAME,
        TypeAttributes.Public | TypeAttributes.Class | TypeAttributes.Abstract |
            TypeAttributes.Sealed
    );

    var featureTypes =
        Assembly.GetExecutingAssembly()
            .GetTypes()
            .Where(
                t => IsSubclassOfGeneric(t, typeof(Feature<>)) && !t.IsAbstract
            )
            .ToArray();

    foreach (var featureType in featureTypes) {
      typeBuilder.DefineField(
          featureType.Name, featureType,
          FieldAttributes.Public | FieldAttributes.Static
      );
    }

    var type = typeBuilder.CreateType();

    foreach (var featureType in featureTypes) {
      try {
        Dbg.Log($"Init: {featureType.Name}");
        var instance = Activator.CreateInstance(featureType);
        type.GetField(featureType.Name).SetValue(null, instance);
        featureType.BaseType.GetField("Instance").SetValue(null, instance);
        featureType.GetMethod("Init").Invoke(instance, null);
      } catch (Exception ex) {
        MelonLogger.Error($"Failed to initialize feature {featureType.Name}:");
        MelonLogger.Error(ex);
      }
    }

    return type;
  }

  public static bool IsSubclassOfGeneric(Type type, Type genericBaseType) {
    while (type != null && type != typeof(object)) {
      var cur = type.IsGenericType ? type.GetGenericTypeDefinition() : type;
      if (genericBaseType == cur)
        return true;
      type = type.BaseType;
    }
    return false;
  }
}

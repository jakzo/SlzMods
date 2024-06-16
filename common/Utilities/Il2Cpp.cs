using System;
using System.Collections.Generic;
using System.Reflection;
using UnhollowerBaseLib;
using UnhollowerRuntimeLib;

namespace Sst.Utilities {
public class Il2Cpp {
  private static readonly MethodInfo TryCastMethodInfo =
      typeof(Il2CppObjectBase).GetMethod("TryCast");

  private static readonly Dictionary<Type, MethodInfo> CachedTryCastMethods =
      new Dictionary<Type, MethodInfo>();

  public static T CastToUnderlyingType<T>(T obj) {
    // var castTo = Type.GetType(obj.GetIl2CppType().AssemblyQualifiedName);
    var castTo =
        Type.GetType(Il2CppType.From(obj.GetType()).AssemblyQualifiedName);

    if (!typeof(Il2CppSystem.Object).IsAssignableFrom(castTo))
      return obj;

    if (!CachedTryCastMethods.ContainsKey(castTo)) {
      CachedTryCastMethods.Add(castTo,
                               TryCastMethodInfo.MakeGenericMethod(castTo));
    }

    return (T)CachedTryCastMethods[castTo].Invoke(obj, null);
  }
}
}

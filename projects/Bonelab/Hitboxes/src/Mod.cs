using System.Collections;
using System.Collections.Generic;
using MelonLoader;
using UnityEngine;

namespace Sst.Hitboxes {
public class Mod : MelonMod {
  private LinkedList<(Collider, GameObject)> _visualizations =
      new LinkedList<(Collider, GameObject)>();

  public override void OnInitializeMelon() { Dbg.Init(BuildInfo.NAME); }

  public override void OnUpdate() {
    // System.AppDomain.CurrentDomain.GetAssemblies() [0].GetName();

    // var colliders = GameObject.FindObjectsOfType<Collider>();
    // var item = _visualizations.First;
    // foreach (var collider in colliders) {
    //   var boxCollider = collider.TryCast(collider.GetIl2CppType());

    //   // The type of object the extension method extends
    //   Type extendedType = collider;

    //   // The method's name
    //   string methodName = "TryCast";

    //   // Search for the extension method in all loaded assemblies
    //   var extensionMethod =
    //       AppDomain.CurrentDomain.GetAssemblies()
    //           .SelectMany(assembly => assembly.GetTypes())
    //           .Where(type =>
    //                      type.IsSealed && !type.IsGenericType &&
    //                      !type.IsNested)
    //           .SelectMany(type => type.GetMethods(BindingFlags.Static |
    //                                               BindingFlags.Public |
    //                                               BindingFlags.NonPublic))
    //           .Where(method => method.Name == methodName)
    //           .FirstOrDefault(method => {
    //             var parameters = method.GetParameters();
    //             return parameters.Length > 0 &&
    //                    parameters[0].ParameterType == extendedType;
    //           });

    //   if (extensionMethod != null) {
    //     Console.WriteLine(
    //         $"Extension method {methodName} is defined in
    //         {extensionMethod.DeclaringType.FullName}");
    //   } else {
    //     Console.WriteLine($"Extension method {methodName} not found.");
    //   }
    // }
  }
}
}

using MelonLoader;
using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SpeedrunTools.Utilities {
public class Geometry {
  public static GameObject CreatePrefabCube(string name, Color color,
                                            float left, float right, float top,
                                            float bottom, float front,
                                            float back) {
    var gameObject = CreatePrefab(name);
    var meshRenderer =
        AddCubeMesh(ref gameObject, left, right, top, bottom, front, back);
    meshRenderer.material.color = color;
    return gameObject;
  }

  public static GameObject
  CreatePrefabUnclosedCylinder(string name, Color color, float radius,
                               int segments, float top, float bottom) {
    var gameObject = CreatePrefab(name);
    var meshRenderer =
        AddUnclosedCylinderMesh(ref gameObject, radius, segments, top, bottom);
    meshRenderer.material.color = color;
    return gameObject;
  }

  public static GameObject CreatePrefabSphere(string name, Color color,
                                              float radius, int subdivisions) {
    var gameObject = CreatePrefab(name);
    var meshRenderer = AddSphereMesh(ref gameObject, radius, subdivisions);
    meshRenderer.material.color = color;
    return gameObject;
  }

  public static GameObject CreatePrefab(string name) {
    var gameObject = new GameObject($"SpeedrunTools_Prefab_{name}") {
      // https://gamedev.stackexchange.com/questions/71713/how-to-create-a-new-gameobject-without-adding-it-to-the-scene
      // hideFlags = HideFlags.HideInHierarchy,
      active = false,
    };
    GameObject.DontDestroyOnLoad(gameObject);
    return gameObject;
  }

  public static MeshRenderer AddCubeMesh(ref GameObject gameObject, float left,
                                         float right, float bottom, float top,
                                         float back, float front) {
    var meshFilter = gameObject.AddComponent<MeshFilter>();
    meshFilter.mesh.Clear();
    meshFilter.mesh.vertices = new[] {
      new Vector3(right, top, back),     new Vector3(left, top, back),
      new Vector3(left, bottom, back),   new Vector3(right, bottom, back),
      new Vector3(right, bottom, front), new Vector3(left, bottom, front),
      new Vector3(left, top, front),     new Vector3(right, top, front),
    };
    meshFilter.mesh.triangles = new[] {
      0, 2, 1, 0, 3, 2, // front
      2, 3, 4, 2, 4, 5, // top
      1, 2, 5, 1, 5, 6, // right
      0, 7, 4, 0, 4, 3, // left
      5, 4, 7, 5, 7, 6, // back
      0, 6, 7, 0, 1, 6, // bottom
    };
    meshFilter.mesh.Optimize();
    meshFilter.mesh.RecalculateBounds();
    meshFilter.mesh.RecalculateTangents();
    meshFilter.mesh.RecalculateNormals();
    return gameObject.AddComponent<MeshRenderer>();
  }

  public static MeshRenderer AddSphereMesh(ref GameObject gameObject,
                                           float radius, int subdivisions) {
    var meshFilter = gameObject.AddComponent<MeshFilter>();
    meshFilter.mesh.Clear();
    var (vertices, triangles, normals) = IcoSphere.Create(radius, subdivisions);
    meshFilter.mesh.vertices = vertices;
    meshFilter.mesh.triangles = triangles;
    meshFilter.mesh.normals = normals;
    meshFilter.mesh.Optimize();
    meshFilter.mesh.RecalculateBounds();
    meshFilter.mesh.RecalculateTangents();
    meshFilter.mesh.RecalculateNormals();
    return gameObject.AddComponent<MeshRenderer>();
  }

  public static MeshRenderer AddUnclosedCylinderMesh(ref GameObject gameObject,
                                                     float radius, int segments,
                                                     float top, float bottom) {
    var meshFilter = gameObject.AddComponent<MeshFilter>();
    meshFilter.mesh.Clear();
    var circleCoords = Enumerable.Range(0, segments).Select(i => {
      var angle = (float)i / (float)segments * (float)System.Math.PI * 2f;
      return new Vector2(Mathf.Sin(angle) * radius, Mathf.Cos(angle) * radius);
    });
    meshFilter.mesh.vertices =
        new[] { bottom, top }
            .SelectMany(y => circleCoords.Select(
                            coord => new Vector3(coord.x, y, coord.y)))
            .ToArray();
    meshFilter.mesh.triangles = Enumerable.Range(0, segments)
                                    .SelectMany(idx => {
                                      var nextIdx = (idx + 1) % segments;
                                      return new[] {
                                        idx,
                                        nextIdx,
                                        segments + idx,
                                        nextIdx,
                                        segments + nextIdx,
                                        segments + idx,
                                      };
                                    })
                                    .ToArray();
    meshFilter.mesh.Optimize();
    meshFilter.mesh.RecalculateBounds();
    meshFilter.mesh.RecalculateTangents();
    meshFilter.mesh.RecalculateNormals();
    return gameObject.AddComponent<MeshRenderer>();
  }

  public static class IcoSphere {
    private static int getMiddlePoint(int p1, int p2,
                                      ref List<Vector3> vertices,
                                      ref Dictionary<long, int> cache,
                                      float radius) {
      var firstIsSmaller = p1 < p2;
      var pMin = firstIsSmaller ? p1 : p2;
      var pMax = firstIsSmaller ? p2 : p1;
      var key = ((long)pMin << 32) + pMax;
      int cached;
      if (cache.TryGetValue(key, out cached)) {
        return cached;
      }

      var index = vertices.Count;
      var point1 = vertices[p1];
      var point2 = vertices[p2];
      var middle =
          new Vector3((point1.x + point2.x) / 2f, (point1.y + point2.y) / 2f,
                      (point1.z + point2.z) / 2f);
      vertices.Add(middle.normalized * radius);
      cache.Add(key, index);
      return index;
    }

    public static (Vector3[], int[], Vector3[])
        Create(float radius, int subdivisions) {
      // Create initial icosahedron vertices
      var t = (1f + Mathf.Sqrt(5f)) / 2f;
      var vertices = new List<Vector3>() {
        new Vector3(-1f, t, 0f).normalized *radius,
        new Vector3(1f, t, 0f).normalized *radius,
        new Vector3(-1f, -t, 0f).normalized *radius,
        new Vector3(1f, -t, 0f).normalized *radius,

        new Vector3(0f, -1f, t).normalized *radius,
        new Vector3(0f, 1f, t).normalized *radius,
        new Vector3(0f, -1f, -t).normalized *radius,
        new Vector3(0f, 1f, -t).normalized *radius,

        new Vector3(t, 0f, -1f).normalized *radius,
        new Vector3(t, 0f, 1f).normalized *radius,
        new Vector3(-t, 0f, -1f).normalized *radius,
        new Vector3(-t, 0f, 1f).normalized *radius,
      };

      // Create initial icosahedron triangles
      var triangles = new int[] {
        // 5 faces around point 0
        0, 11, 5,  // 0
        0, 5, 1,   // 1
        0, 1, 7,   // 2
        0, 7, 10,  // 3
        0, 10, 11, // 4

        // 5 adjacent faces
        1, 5, 9,   // 0
        5, 11, 4,  // 1
        11, 10, 2, // 2
        10, 7, 6,  // 3
        7, 1, 8,   // 4

        // 5 faces around point 3
        3, 9, 4, // 0
        3, 4, 2, // 1
        3, 2, 6, // 2
        3, 6, 8, // 3
        3, 8, 9, // 4

        // 5 adjacent faces
        4, 9, 5,  // 0
        2, 4, 11, // 1
        6, 2, 10, // 2
        8, 6, 7,  // 3
        9, 8, 1,  // 4
      };

      // Subdivide triangles
      var middlePointIndexCache = new Dictionary<long, int>();
      for (int i = 0; i < subdivisions; i++) {
        var newTriangles = new int[triangles.Length * 4];
        for (var j = 0; j < triangles.Length; j += 3) {
          var i0 = triangles[j];
          var i1 = triangles[j + 1];
          var i2 = triangles[j + 2];

          // Replace triangle with 4 sub-triangles
          int a = getMiddlePoint(i0, i1, ref vertices,
                                 ref middlePointIndexCache, radius);
          int b = getMiddlePoint(i1, i2, ref vertices,
                                 ref middlePointIndexCache, radius);
          int c = getMiddlePoint(i2, i0, ref vertices,
                                 ref middlePointIndexCache, radius);
          new int[] {
            i0, a, c, // 0
            i1, b, a, // 1
            i2, c, b, // 2
            a,  b, c, // 3
          }
              .CopyTo(newTriangles, j * 4);
        }
        triangles = newTriangles;
      }

      var normals = vertices.Select(vertex => vertex.normalized).ToArray();

      return (vertices.ToArray(), triangles, normals);
    }
  }
}
}

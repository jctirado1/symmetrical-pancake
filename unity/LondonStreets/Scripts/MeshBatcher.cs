using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace LondonStreets
{
    /// <summary>
    /// Collects thousands of boxes and turns them into one mesh per material,
    /// which keeps the game fast. Texture coordinates are worked out from the
    /// world position, so a window texture looks the same size on every
    /// building however big it is.
    /// </summary>
    public class MeshBatcher
    {
        class Batch
        {
            public readonly List<Vector3> Vertices = new List<Vector3>();
            public readonly List<Vector3> Normals = new List<Vector3>();
            public readonly List<Vector2> Uvs = new List<Vector2>();
            public readonly List<int> Triangles = new List<int>();
        }

        // Each face: outward normal, then two edge directions a and b with a x b = normal.
        static readonly Vector3[,] Faces =
        {
            { Vector3.up, Vector3.forward, Vector3.right },
            { Vector3.down, Vector3.right, Vector3.forward },
            { Vector3.right, Vector3.up, Vector3.forward },
            { Vector3.left, Vector3.forward, Vector3.up },
            { Vector3.forward, Vector3.right, Vector3.up },
            { Vector3.back, Vector3.up, Vector3.right },
        };

        readonly Dictionary<Material, Batch> _batches = new Dictionary<Material, Batch>();
        readonly Dictionary<Material, float> _tileSizes = new Dictionary<Material, float>();

        /// <summary>The texture on <paramref name="material"/> repeats every <paramref name="metres"/>.</summary>
        public void SetTileSize(Material material, float metres) => _tileSizes[material] = metres;

        public void Clear() => _batches.Clear();

        public void AddBox(Material material, Vector3 centre, Vector3 size, Quaternion rotation)
        {
            if (!_batches.TryGetValue(material, out var batch))
            {
                batch = new Batch();
                _batches[material] = batch;
            }
            float tile = _tileSizes.TryGetValue(material, out var t) ? t : 1f;
            var half = size * 0.5f;
            for (int f = 0; f < 6; f++)
            {
                var n = Faces[f, 0];
                var a = Faces[f, 1];
                var b = Faces[f, 2];
                var worldNormal = rotation * n;
                int first = batch.Vertices.Count;
                Vector3 Corner(float sa, float sb) => centre + rotation * Vector3.Scale(half, n + a * sa + b * sb);
                var corners = new[] { Corner(-1, -1), Corner(-1, 1), Corner(1, 1), Corner(1, -1) };
                foreach (var corner in corners)
                {
                    batch.Vertices.Add(corner);
                    batch.Normals.Add(worldNormal);
                    batch.Uvs.Add(WorldUv(corner, worldNormal) / tile);
                }
                batch.Triangles.Add(first);
                batch.Triangles.Add(first + 2);
                batch.Triangles.Add(first + 1);
                batch.Triangles.Add(first);
                batch.Triangles.Add(first + 3);
                batch.Triangles.Add(first + 2);
            }
        }

        static Vector2 WorldUv(Vector3 p, Vector3 normal)
        {
            if (Mathf.Abs(normal.y) > 0.5f)
                return new Vector2(p.x, p.z);
            if (Mathf.Abs(normal.x) > 0.5f)
                return new Vector2(p.z, p.y);
            return new Vector2(p.x, p.y);
        }

        /// <summary>
        /// Builds one child object per material under <paramref name="parent"/>.
        /// Returns the meshes it made, so they can be cleaned up later.
        /// </summary>
        public List<Mesh> Flush(Transform parent)
        {
            var meshes = new List<Mesh>();
            foreach (var pair in _batches)
            {
                var batch = pair.Value;
                var mesh = new Mesh { name = pair.Key.name + " (batched)", indexFormat = IndexFormat.UInt32 };
                mesh.hideFlags = HideFlags.DontSave;
                mesh.SetVertices(batch.Vertices);
                mesh.SetNormals(batch.Normals);
                mesh.SetUVs(0, batch.Uvs);
                mesh.SetTriangles(batch.Triangles, 0);
                mesh.RecalculateBounds();
                mesh.RecalculateTangents();
                meshes.Add(mesh);

                var go = new GameObject(pair.Key.name);
                go.transform.SetParent(parent, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = pair.Key;
                renderer.shadowCastingMode = ShadowCastingMode.On;
            }
            _batches.Clear();
            return meshes;
        }
    }
}

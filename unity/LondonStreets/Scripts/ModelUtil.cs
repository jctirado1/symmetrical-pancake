using System.Collections.Generic;
using UnityEngine;

namespace LondonStreets
{
    /// <summary>
    /// Helpers that turn any imported model (.fbx, .obj, .glb or a prefab) into
    /// something we can place in the world: measure it, resize it, stand it on
    /// the ground and turn it the right way.
    ///
    /// A spawned model is a "wrapper" object holding a copy of the model. The
    /// fitting functions move the copy inside the wrapper so the bottom-centre
    /// of the model is at the wrapper's origin, then scale and turn the wrapper.
    /// </summary>
    public static class ModelUtil
    {
        /// <summary>
        /// Makes a copy of <paramref name="model"/> inside a new wrapper object.
        /// Colliders, rigidbodies, cameras and the like that came with the model
        /// are removed so they don't fight with the game's own.
        /// </summary>
        public static GameObject Spawn(GameObject model, Texture texture = null)
        {
            var wrapper = new GameObject(model.name);
            var copy = Object.Instantiate(model, wrapper.transform, false);
            copy.name = model.name;
            Strip(copy);
            if (texture != null)
                ApplyMaterial(copy, LondonMaterials.Textured(texture));
            return wrapper;
        }

        static void Strip(GameObject copy)
        {
            foreach (var c in copy.GetComponentsInChildren<Joint>(true)) Object.DestroyImmediate(c);
            foreach (var c in copy.GetComponentsInChildren<CharacterController>(true)) Object.DestroyImmediate(c);
            foreach (var c in copy.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
            foreach (var c in copy.GetComponentsInChildren<Rigidbody>(true)) Object.DestroyImmediate(c);
            foreach (var c in copy.GetComponentsInChildren<AudioListener>(true)) Object.DestroyImmediate(c);
            foreach (var c in copy.GetComponentsInChildren<Camera>(true)) Object.DestroyImmediate(c);
        }

        public static void ApplyMaterial(GameObject root, Material material)
        {
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                var materials = new Material[Mathf.Max(1, renderer.sharedMaterials.Length)];
                for (int i = 0; i < materials.Length; i++)
                    materials[i] = material;
                renderer.sharedMaterials = materials;
            }
        }

        /// <summary>
        /// The box the model fits in, measured in <paramref name="wrapper"/>'s own
        /// space. Size is zero if the model has nothing visible.
        /// </summary>
        public static Bounds MeasureLocal(GameObject wrapper)
        {
            var toWrapper = wrapper.transform.worldToLocalMatrix;
            bool any = false;
            var result = new Bounds();
            foreach (var renderer in wrapper.GetComponentsInChildren<Renderer>(true))
            {
                Bounds local;
                Matrix4x4 toWorld;
                if (renderer is SkinnedMeshRenderer skinned)
                {
                    local = skinned.localBounds;
                    toWorld = (skinned.rootBone != null ? skinned.rootBone : skinned.transform).localToWorldMatrix;
                }
                else if (renderer is MeshRenderer)
                {
                    var filter = renderer.GetComponent<MeshFilter>();
                    if (filter == null || filter.sharedMesh == null)
                        continue;
                    local = filter.sharedMesh.bounds;
                    toWorld = renderer.transform.localToWorldMatrix;
                }
                else
                {
                    continue;
                }
                var matrix = toWrapper * toWorld;
                var c = local.center;
                var e = local.extents;
                for (int i = 0; i < 8; i++)
                {
                    var corner = c + Vector3.Scale(e, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    var p = matrix.MultiplyPoint3x4(corner);
                    if (!any)
                    {
                        result = new Bounds(p, Vector3.zero);
                        any = true;
                    }
                    else
                    {
                        result.Encapsulate(p);
                    }
                }
            }
            return result;
        }

        /// <summary>Moves the model inside its wrapper so its bottom-centre sits on the wrapper's origin.</summary>
        static void CentreOnFeet(GameObject wrapper, Bounds bounds)
        {
            var bottomCentre = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            foreach (Transform child in wrapper.transform)
                child.localPosition -= bottomCentre;
        }

        /// <summary>
        /// Places the model on its feet with the given turn and scale, as a
        /// child of <paramref name="parent"/> at <paramref name="localPosition"/>.
        /// Returns the model's size before scaling.
        /// </summary>
        public static Vector3 Place(GameObject wrapper, Transform parent, Vector3 localPosition, Quaternion localRotation, float scale)
        {
            var bounds = MeasureLocal(wrapper);
            CentreOnFeet(wrapper, bounds);
            wrapper.transform.SetParent(parent, false);
            wrapper.transform.localPosition = localPosition;
            wrapper.transform.localRotation = localRotation;
            wrapper.transform.localScale = Vector3.one * scale;
            return bounds.size;
        }

        /// <summary>
        /// Sizes and places a car: turned to point along Z (whichever way round it
        /// was made), scaled to <paramref name="length"/> metres long and stood on
        /// the ground. Returns its size (width, height, length) in metres.
        /// </summary>
        public static Vector3 FitVehicle(GameObject wrapper, Transform parent, float length, bool flip)
        {
            var bounds = MeasureLocal(wrapper);
            var size = bounds.size;
            bool alongX = size.x > size.z;
            float modelLength = Mathf.Max(Mathf.Max(size.x, size.z), 0.001f);
            float yaw = (alongX ? 90f : 0f) + (flip ? 180f : 0f);
            float scale = length / modelLength;
            Place(wrapper, parent, Vector3.zero, Quaternion.Euler(0f, yaw, 0f), scale);
            size *= scale;
            return alongX ? new Vector3(size.z, size.y, size.x) : size;
        }

        /// <summary>
        /// Sizes and places a character: scaled to <paramref name="height"/> metres
        /// tall with its feet on the ground, facing forward (+Z).
        /// </summary>
        public static void FitCharacter(GameObject wrapper, Transform parent, float height, bool flip)
        {
            var bounds = MeasureLocal(wrapper);
            float scale = height / Mathf.Max(bounds.size.y, 0.001f);
            Place(wrapper, parent, Vector3.zero, Quaternion.Euler(0f, flip ? 180f : 0f, 0f), scale);
        }

        /// <summary>Makes every mesh in <paramref name="root"/> solid with its exact shape.</summary>
        public static void AddMeshColliders(GameObject root)
        {
            foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh != null)
                    filter.gameObject.AddComponent<MeshCollider>().sharedMesh = filter.sharedMesh;
            }
            foreach (var skinned in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (skinned.sharedMesh != null)
                    skinned.gameObject.AddComponent<MeshCollider>().sharedMesh = skinned.sharedMesh;
            }
        }

        /// <summary>A box seen axis-aligned after turning it by <paramref name="rotation"/> (a quarter turn or so).</summary>
        public static Vector3 RotatedSize(Vector3 size, Quaternion rotation)
        {
            var r = rotation * size;
            return new Vector3(Mathf.Abs(r.x), Mathf.Abs(r.y), Mathf.Abs(r.z));
        }

        /// <summary>A plain shape (cube, cylinder...) with no collider, for building simple looks.</summary>
        public static GameObject Primitive(PrimitiveType type, Transform parent, Vector3 position, Vector3 scale, Material material)
        {
            var go = GameObject.CreatePrimitive(type);
            var collider = go.GetComponent<Collider>();
            if (collider != null)
                Object.DestroyImmediate(collider);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale;
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
            return go;
        }

        public static void SetHideFlagsRecursively(GameObject root, HideFlags flags)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                t.gameObject.hideFlags = flags;
        }

        /// <summary>Destroy that works both while playing and in the editor.</summary>
        public static void SafeDestroy(Object obj)
        {
            if (obj == null)
                return;
            if (Application.isPlaying)
                Object.Destroy(obj);
            else
                Object.DestroyImmediate(obj);
        }

        /// <summary>Picks a random item, or the default if the list is empty.</summary>
        public static T PickRandom<T>(IList<T> list) => list == null || list.Count == 0 ? default : list[Random.Range(0, list.Count)];
    }
}

using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace LondonStreets
{
    /// <summary>
    /// A slot for one of your own models (a building, a bus stop, a statue...).
    /// Drag a model onto "Model" in the Inspector and it appears straight away,
    /// and is solid when you play.
    ///
    /// (You can also just drag models from the Project window into the scene,
    /// as with any Unity game; add a Mesh Collider to make them solid.)
    /// </summary>
    [ExecuteAlways]
    public class ModelProp : MonoBehaviour
    {
        [Tooltip("The model to show: a .fbx, .obj or .glb file, or a prefab.")]
        public GameObject model;
        [Tooltip("Optional texture to paint onto the model (useful for .obj files that show up white).")]
        public Texture2D texture;
        [Tooltip("Make the model solid so the player bumps into it instead of walking through.")]
        public bool solid = true;

        GameObject _instance;
#if UNITY_EDITOR
        bool _rebuildQueued;
#endif

        void OnEnable() => Rebuild();
        void OnDisable() => Clear();

        void OnValidate()
        {
#if UNITY_EDITOR
            if (_rebuildQueued)
                return;
            _rebuildQueued = true;
            EditorApplication.delayCall += () =>
            {
                _rebuildQueued = false;
                if (this != null && isActiveAndEnabled)
                    Rebuild();
            };
#endif
        }

        void Clear()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i);
                if (child.name == "Model")
                    ModelUtil.SafeDestroy(child.gameObject);
            }
            _instance = null;
        }

        void Rebuild()
        {
            Clear();
            if (model == null)
                return;
            _instance = ModelUtil.Spawn(model, texture);
            _instance.name = "Model";
            _instance.transform.SetParent(transform, false);
            if (Application.isPlaying)
            {
                if (solid)
                    ModelUtil.AddMeshColliders(_instance);
            }
            else
            {
                // A preview only: don't save it into the scene.
                ModelUtil.SetHideFlagsRecursively(_instance, HideFlags.DontSave | HideFlags.NotEditable);
            }
        }

        void OnDrawGizmos()
        {
            if (model != null)
                return;
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireCube(transform.position + Vector3.up * 0.5f, Vector3.one);
        }
    }
}

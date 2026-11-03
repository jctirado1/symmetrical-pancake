using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace LondonStreets
{
    /// <summary>
    /// Finds models for the game to use: what's in a folder, and the characters
    /// and cars found automatically anywhere in the project.
    /// </summary>
    public static class AssetLibrary
    {
        public const string Root = "Assets/LondonStreets";
        public const string CharactersFolder = Root + "/Characters";
        public const string CarsFolder = Root + "/Cars";

        static readonly string[] ModelExtensions = { ".fbx", ".obj", ".glb", ".gltf", ".dae", ".blend", ".3ds", ".max", ".prefab" };

        static AssetCatalog _catalog;

        public static AssetCatalog Catalog
        {
            get
            {
                if (_catalog == null)
                    _catalog = Resources.Load<AssetCatalog>(AssetCatalog.ResourceName);
                return _catalog;
            }
        }

        /// <summary>Forget the cached catalog (the editor calls this after rewriting it).</summary>
        public static void Reload() => _catalog = null;

        public static bool IsModelPath(string path)
        {
            var extension = Path.GetExtension(path).ToLowerInvariant();
            return ModelExtensions.Contains(extension);
        }

        /// <summary>
        /// Every model in <paramref name="folder"/> and its sub-folders, in
        /// alphabetical order. If a prefab and a model file share a name, only
        /// the prefab is listed (it usually has the materials set up).
        /// </summary>
        public static List<GameObject> ModelsInFolder(string folder)
        {
            var result = new List<GameObject>();
            if (string.IsNullOrEmpty(folder))
                return result;
#if UNITY_EDITOR
            folder = folder.TrimEnd('/');
            if (!AssetDatabase.IsValidFolder(folder))
                return result;
            var paths = AssetDatabase.FindAssets("t:GameObject", new[] { folder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(IsModelPath)
                .Distinct()
                .OrderBy(p => p, System.StringComparer.OrdinalIgnoreCase)
                .ToList();
            foreach (var path in PreferPrefabs(paths))
            {
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (model != null)
                    result.Add(model);
            }
#else
            // In a built game, folders can't be searched, so use what the editor
            // remembered about the two drop folders.
            var catalog = Catalog;
            if (catalog != null)
            {
                if (folder.TrimEnd('/') == CharactersFolder) result.AddRange(catalog.droppedCharacters);
                if (folder.TrimEnd('/') == CarsFolder) result.AddRange(catalog.droppedCars);
            }
#endif
            result.RemoveAll(m => m == null);
            return result;
        }

        /// <summary>Drops "Car.fbx" when "Car.prefab" is in the list too.</summary>
        public static List<string> PreferPrefabs(List<string> paths)
        {
            var prefabNames = new HashSet<string>(paths
                .Where(p => p.EndsWith(".prefab", System.StringComparison.OrdinalIgnoreCase))
                .Select(p => Path.GetFileNameWithoutExtension(p).ToLowerInvariant()));
            return paths.Where(p => p.EndsWith(".prefab", System.StringComparison.OrdinalIgnoreCase)
                || !prefabNames.Contains(Path.GetFileNameWithoutExtension(p).ToLowerInvariant())).ToList();
        }

        /// <summary>The character found for the player (see found_assets.txt), or null.</summary>
        public static GameObject PlayerCharacter => Catalog != null ? Catalog.player : null;

        /// <summary>Every character found except the player's (or all of them if there's only one).</summary>
        public static List<GameObject> OtherCharacters
        {
            get
            {
                var list = Catalog != null ? Catalog.characters.Where(c => c != null).ToList() : new List<GameObject>();
                if (list.Count > 1)
                    list.Remove(PlayerCharacter);
                return list;
            }
        }

        /// <summary>Every car found in the project.</summary>
        public static List<GameObject> Cars =>
            Catalog != null ? Catalog.cars.Where(c => c != null).ToList() : new List<GameObject>();

        /// <summary>The animations that came with a model.</summary>
        public static List<AnimationClip> ClipsFor(GameObject model)
        {
            var clips = new List<AnimationClip>();
            if (model == null)
                return clips;
#if UNITY_EDITOR
            clips.AddRange(ClipsInAsset(model));
            if (clips.Count == 0 && PrefabUtility.IsPartOfPrefabAsset(model))
            {
                var source = PrefabUtility.GetCorrespondingObjectFromOriginalSource(model);
                if (source != null && source != model)
                    clips.AddRange(ClipsInAsset(source));
            }
#else
            var catalog = Catalog;
            if (catalog != null)
            {
                var entry = catalog.animations.FirstOrDefault(a => a.model == model);
                if (entry != null && entry.clips != null)
                    clips.AddRange(entry.clips.Where(c => c != null));
            }
#endif
            // An Animator Controller that came with the model knows its clips too.
            var animator = model.GetComponentInChildren<Animator>(true);
            if (animator != null && animator.runtimeAnimatorController != null)
            {
                foreach (var clip in animator.runtimeAnimatorController.animationClips)
                {
                    if (clip != null && !clips.Contains(clip))
                        clips.Add(clip);
                }
            }
            return clips;
        }

#if UNITY_EDITOR
        public static IEnumerable<AnimationClip> ClipsInAsset(Object asset)
        {
            var path = AssetDatabase.GetAssetPath(asset);
            if (string.IsNullOrEmpty(path))
                return Enumerable.Empty<AnimationClip>();
            return AssetDatabase.LoadAllAssetsAtPath(path)
                .OfType<AnimationClip>()
                .Where(c => !c.name.StartsWith("__preview__"));
        }
#endif

        /// <summary>
        /// Returns <paramref name="chosen"/> if set, otherwise the first clip whose
        /// name contains one of <paramref name="keywords"/>.
        /// </summary>
        public static AnimationClip PickClip(List<AnimationClip> clips, AnimationClip chosen, params string[] keywords)
        {
            if (chosen != null)
                return chosen;
            foreach (var keyword in keywords)
            {
                foreach (var clip in clips)
                {
                    if (clip.name.ToLowerInvariant().Contains(keyword))
                        return clip;
                }
            }
            return null;
        }
    }
}

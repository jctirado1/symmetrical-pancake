using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace LondonStreets.EditorTools
{
    /// <summary>
    /// Searches the whole project for character and car models (for example
    /// inside a GAMEPRO folder you copied in), so the game can use them without
    /// you setting anything up.
    ///
    /// How it decides what a model is:
    /// - Cars: the file or folder name has a word like "car", "sedan", "taxi",
    ///   "van" or "truck" in it (but not "wheel", "door" and other car parts).
    /// - Characters: the name has a word like "character", "man", "woman",
    ///   "player" or "npc", or the model has a skeleton (it's rigged for animation).
    ///
    /// It runs every time you press Play (and when models are added), and only
    /// looks inside new or changed files. What it found is written to
    /// Assets/LondonStreets/found_assets.txt.
    /// </summary>
    [InitializeOnLoad]
    public static class AssetFinder
    {
        const string CatalogFolder = AssetLibrary.Root + "/Resources";
        const string CatalogPath = CatalogFolder + "/" + AssetCatalog.ResourceName + ".asset";
        const string ReportPath = AssetLibrary.Root + "/found_assets.txt";
        const string CachePath = "Library/LondonStreetsAssetCache.json";
        const int CacheVersion = 1;

        static readonly string[] CarWords =
        {
            "car", "cars", "vehicle", "vehicles", "sedan", "suv", "taxi", "cab", "van", "truck",
            "lorry", "pickup", "hatchback", "coupe", "jeep", "police", "ambulance", "convertible",
            "limo", "limousine", "wagon", "minivan", "sportscar", "supercar", "racecar",
            "roadster", "automobile", "firetruck",
        };
        /// <summary>Places named after vehicles or people ("Police_Station", "Car_Park").</summary>
        static readonly string[] PlaceWords =
        {
            "station", "building", "buildings", "house", "garage", "shop", "store", "sign",
            "signs", "park", "parking", "showroom", "wash", "dealership", "hole", "manhole",
        };
        static readonly string[] CarPartWords =
        {
            "wheel", "wheels", "tire", "tires", "tyre", "tyres", "rim", "rims", "door", "doors",
            "interior", "seat", "seats", "steering", "engine", "bumper", "hood", "bonnet",
            "spoiler", "mirror", "mirrors", "chassis", "glass",
        };
        static readonly string[] CharacterWords =
        {
            "character", "characters", "human", "humans", "person", "people", "pedestrian",
            "pedestrians", "npc", "npcs", "player", "players", "male", "female", "man", "woman",
            "men", "women", "guy", "girl", "boy", "kid", "child", "hero", "civilian", "citizen",
            "worker", "businessman", "businesswoman", "policeman", "policewoman", "officer",
            "bot", "avatar", "mannequin",
        };
        /// <summary>Things that are definitely not characters, so they're not worth opening.</summary>
        static readonly string[] SceneryWords =
        {
            "building", "buildings", "house", "houses", "shop", "store", "wall", "walls", "road",
            "roads", "street", "tree", "trees", "plant", "plants", "fence", "bench", "lamp",
            "light", "lights", "sign", "signs", "roof", "floor", "tile", "tiles", "sidewalk",
            "pavement", "ground", "terrain", "rock", "rocks", "bush", "grass", "office", "tower",
            "apartment", "apartments", "factory", "warehouse", "texture", "textures", "material",
            "materials", "window", "windows", "pipe", "pipes", "crate", "barrel", "box", "tank",
        };
        static readonly string[] PlayerWords = { "player", "players", "hero", "main", "protagonist" };

        static readonly Regex WordPattern = new Regex("[A-Z]?[a-z]+|[A-Z]+(?![a-z])|[0-9]+");

        [Serializable]
        class CacheEntry
        {
            public string guid;
            public string hash;
            public string kind;
        }

        [Serializable]
        class Cache
        {
            public int version;
            public List<CacheEntry> entries = new List<CacheEntry>();
        }

        static bool _refreshQueued;

        static AssetFinder()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.ExitingEditMode)
                    Refresh();
            };
            QueueRefresh();
        }

        /// <summary>Refreshes a moment from now (several imports in a row only cause one search).</summary>
        public static void QueueRefresh()
        {
            if (_refreshQueued)
                return;
            _refreshQueued = true;
            EditorApplication.delayCall += () =>
            {
                _refreshQueued = false;
                if (!EditorApplication.isPlayingOrWillChangePlaymode)
                    Refresh();
            };
        }

        [MenuItem("London Streets/Find Characters and Cars Again", priority = 20)]
        static void RefreshFromMenu()
        {
            if (File.Exists(CachePath))
                File.Delete(CachePath);
            Refresh();
            var catalog = AssetDatabase.LoadAssetAtPath<AssetCatalog>(CatalogPath);
            EditorUtility.DisplayDialog("London Streets",
                $"Found {catalog.characters.Count} characters and {catalog.cars.Count} cars.\n\nThe full list is in {ReportPath}.", "OK");
            var report = AssetDatabase.LoadAssetAtPath<TextAsset>(ReportPath);
            if (report != null)
                EditorGUIUtility.PingObject(report);
        }

        public static void Refresh()
        {
            if (!AssetDatabase.IsValidFolder(AssetLibrary.Root))
                return; // The LondonStreets folder has been moved or renamed.

            var cache = LoadCache();
            var known = cache.entries.ToDictionary(e => e.guid);
            var fresh = new Cache { version = CacheVersion };

            var paths = AssetDatabase.FindAssets("t:GameObject", new[] { "Assets" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(AssetLibrary.IsModelPath)
                .Distinct()
                .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                .ToList();
            paths = AssetLibrary.PreferPrefabs(paths);

            var characters = new List<string>();
            var cars = new List<string>();
            foreach (var path in paths)
            {
                var guid = AssetDatabase.AssetPathToGUID(path);
                var hash = AssetDatabase.GetAssetDependencyHash(path).ToString();
                string kind;
                if (known.TryGetValue(guid, out var entry) && entry.hash == hash)
                    kind = entry.kind;
                else
                    kind = Classify(path);
                fresh.entries.Add(new CacheEntry { guid = guid, hash = hash, kind = kind });
                if (kind == "character")
                    characters.Add(path);
                else if (kind == "car")
                    cars.Add(path);
            }
            SaveCache(fresh);

            string playerPath = characters.FirstOrDefault(p => HasAny(Words(p), PlayerWords)) ?? characters.FirstOrDefault();
            WriteCatalog(characters, cars, playerPath);
            WriteReport(paths.Count, characters, cars, playerPath);
        }

        /// <summary>Works out whether a model is a "character", a "car" or "other".</summary>
        static string Classify(string path)
        {
            var words = Words(path);
            if (HasAny(words, CarPartWords) || HasAny(words, PlaceWords))
                return "other";
            if (HasAny(words, CarWords))
                return "car";
            if (HasAny(words, CharacterWords))
                return "character";
            if (HasAny(words, SceneryWords))
                return "other";

            // No clues in the name: open it and look for a skeleton.
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (model != null && model.GetComponentsInChildren<SkinnedMeshRenderer>(true).Any(r => r.sharedMesh != null && r.bones.Length > 0))
                return "character";
            return "other";
        }

        /// <summary>
        /// Splits a path like "Assets/GAMEPRO/People/BusinessMan_01.fbx" into lower-case
        /// words: gamepro, people, business, man, 01. Whole compound names
        /// (businessman) are kept too.
        /// </summary>
        static List<string> Words(string path)
        {
            var words = new List<string>();
            var text = path.StartsWith("Assets/") ? path.Substring("Assets/".Length) : path;
            var extension = Path.GetExtension(text);
            if (!string.IsNullOrEmpty(extension))
                text = text.Substring(0, text.Length - extension.Length);
            foreach (var piece in text.Split(new[] { '/', '_', '-', ' ', '.' }, StringSplitOptions.RemoveEmptyEntries))
            {
                words.Add(piece.ToLowerInvariant());
                foreach (Match match in WordPattern.Matches(piece))
                    words.Add(match.Value.ToLowerInvariant());
            }
            return words;
        }

        static bool HasAny(List<string> words, string[] wanted) => words.Any(wanted.Contains);

        static void WriteCatalog(List<string> characters, List<string> cars, string playerPath)
        {
            if (!AssetDatabase.IsValidFolder(CatalogFolder))
                AssetDatabase.CreateFolder(AssetLibrary.Root, "Resources");
            var catalog = AssetDatabase.LoadAssetAtPath<AssetCatalog>(CatalogPath);
            bool isNew = catalog == null;
            if (isNew)
                catalog = ScriptableObject.CreateInstance<AssetCatalog>();

            GameObject Load(string path) => AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var newCharacters = characters.Select(Load).Where(m => m != null).ToList();
            var newCars = cars.Select(Load).Where(m => m != null).ToList();
            var newPlayer = playerPath != null ? Load(playerPath) : null;
            var droppedCharacters = AssetLibrary.ModelsInFolder(AssetLibrary.CharactersFolder);
            var droppedCars = AssetLibrary.ModelsInFolder(AssetLibrary.CarsFolder);
            var animations = newCharacters.Union(droppedCharacters)
                .Select(m => new AssetCatalog.ModelClips { model = m, clips = AssetLibrary.ClipsFor(m).ToArray() })
                .Where(e => e.clips.Length > 0)
                .ToList();

            bool changed = isNew
                || catalog.player != newPlayer
                || !catalog.characters.SequenceEqual(newCharacters)
                || !catalog.cars.SequenceEqual(newCars)
                || !catalog.droppedCharacters.SequenceEqual(droppedCharacters)
                || !catalog.droppedCars.SequenceEqual(droppedCars)
                || catalog.animations.Count != animations.Count
                || catalog.animations.Zip(animations, (a, b) => a.model == b.model && a.clips.SequenceEqual(b.clips)).Any(same => !same);
            if (!changed)
                return;

            catalog.player = newPlayer;
            catalog.characters = newCharacters;
            catalog.cars = newCars;
            catalog.droppedCharacters = droppedCharacters;
            catalog.droppedCars = droppedCars;
            catalog.animations = animations;
            if (isNew)
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            AssetLibrary.Reload();
            Debug.Log($"London Streets: found {newCharacters.Count} characters and {newCars.Count} cars (out of all the models in the project). Details in {ReportPath}");
        }

        static void WriteReport(int total, List<string> characters, List<string> cars, string playerPath)
        {
            var text = new StringBuilder();
            text.AppendLine("Models found by the London Streets AssetFinder (this file is rewritten automatically).");
            text.AppendLine($"Searched {total} models.");
            text.AppendLine();
            text.AppendLine($"CHARACTERS ({characters.Count}):");
            foreach (var path in characters)
                text.AppendLine("  " + path + (path == playerPath ? "   <- player" : "   <- pedestrian"));
            text.AppendLine();
            text.AppendLine($"CARS ({cars.Count}) - used for the parked cars and the traffic:");
            foreach (var path in cars)
                text.AppendLine("  " + path);
            text.AppendLine();
            text.AppendLine("If something is missing or in the wrong list, rename the file or its folder");
            text.AppendLine("(e.g. put cars in a folder called 'cars' and characters in one called 'characters'),");
            text.AppendLine("or use the menu London Streets > Find Characters and Cars Again.");

            var content = text.ToString();
            if (File.Exists(ReportPath) && File.ReadAllText(ReportPath) == content)
                return;
            File.WriteAllText(ReportPath, content);
            AssetDatabase.ImportAsset(ReportPath);
        }

        static Cache LoadCache()
        {
            try
            {
                if (File.Exists(CachePath))
                {
                    var cache = JsonUtility.FromJson<Cache>(File.ReadAllText(CachePath));
                    if (cache != null && cache.version == CacheVersion)
                    {
                        // Ignore duplicates, just in case.
                        cache.entries = cache.entries.GroupBy(e => e.guid).Select(g => g.First()).ToList();
                        return cache;
                    }
                }
            }
            catch (Exception)
            {
                // A broken cache just means everything gets checked again.
            }
            return new Cache { version = CacheVersion };
        }

        static void SaveCache(Cache cache)
        {
            try
            {
                File.WriteAllText(CachePath, JsonUtility.ToJson(cache));
            }
            catch (Exception)
            {
                // Not being able to save the cache only makes the next search slower.
            }
        }
    }

    /// <summary>Looks again whenever models are added, moved or deleted.</summary>
    class ModelImportWatcher : AssetPostprocessor
    {
        static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            if (imported.Concat(deleted).Concat(moved).Concat(movedFrom).Any(AssetLibrary.IsModelPath))
                AssetFinder.QueueRefresh();
        }
    }
}

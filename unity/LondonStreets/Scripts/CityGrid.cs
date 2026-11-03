using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace LondonStreets
{
    public enum Zone { City, Suburban, Industrial, Park }

    /// <summary>
    /// Builds a whole grid of London streets: roads, junctions with zebra
    /// crossings, pavements, street lamps, buildings and parks.
    ///
    /// Everything is generated from the settings in the Inspector, so change a
    /// number (or the Random Seed) and the city rebuilds itself, in the editor
    /// too. Point the "Models Folder" settings at your city packs and those
    /// buildings are used instead of the plain boxes.
    ///
    /// The city is centred on this object, which should stay at (0, 0, 0):
    /// there's always a crossroads there.
    /// </summary>
    [ExecuteAlways]
    public class CityGrid : MonoBehaviour
    {
        public const float PavementHeight = 0.08f;

        /// <summary>The city in the scene (the last one enabled).</summary>
        public static CityGrid Main { get; private set; }

        [Header("Layout")]
        [Tooltip("How many blocks across (west to east).")]
        [Range(1, 20)] public int blocksX = 6;
        [Tooltip("How many blocks deep (south to north).")]
        [Range(1, 20)] public int blocksZ = 6;
        [Tooltip("Size of each block of buildings, in metres.")]
        [Range(30f, 200f)] public float blockSize = 60f;
        [Tooltip("Width of the roads (two lanes), in metres.")]
        [Range(6f, 30f)] public float roadWidth = 12f;
        [Tooltip("Width of the pavement around each block.")]
        [Range(2f, 10f)] public float pavementWidth = 4f;
        [Tooltip("Change this to get a different random city.")]
        public int randomSeed = 1;
        [Tooltip("Chance (0 to 1) that a block in the middle of town becomes a park.")]
        [Range(0f, 1f)] public float parkChance = 0.12f;
        [Tooltip("Put industrial buildings along the east edge of the city.")]
        public bool industrialEdge = true;
        public bool streetLamps = true;

        [Header("Building Models")]
        [Tooltip("Folder of building models for the middle of town (offices, shops, London buildings). Leave empty to use plain boxes.")]
        [FolderPath] public string cityModelsFolder = "";
        [Tooltip("Folder of building models for the outer ring of blocks (houses).")]
        [FolderPath] public string suburbanModelsFolder = "";
        [Tooltip("Folder of building models for the industrial edge (warehouses, factories).")]
        [FolderPath] public string industrialModelsFolder = "";
        [Tooltip("Or drag building models straight into these lists. They're used as well as the folders.")]
        public List<GameObject> cityModels = new List<GameObject>();
        public List<GameObject> suburbanModels = new List<GameObject>();
        public List<GameObject> industrialModels = new List<GameObject>();
        [Tooltip("Resize each model so it fits its plot. Untick to keep the models' own size (then use Model Scale to match them to the world).")]
        public bool fitModelsToPlots = true;
        [Min(0.0001f)] public float modelScale = 1f;
        [Tooltip("Tick this if your building models face away from the road.")]
        public bool flipModels;

        System.Random _rng;
        GameObject _generated;
        GameObject _collision;
        readonly MeshBatcher _batcher = new MeshBatcher();
        readonly List<Object> _owned = new List<Object>();
        readonly Dictionary<Vector2Int, Zone> _blockZones = new Dictionary<Vector2Int, Zone>();
        readonly Dictionary<Zone, List<GameObject>> _models = new Dictionary<Zone, List<GameObject>>();
#if UNITY_EDITOR
        bool _rebuildQueued;
#endif

        Material _matRoad, _matPavement, _matGrass, _matWhite, _matRoof, _matLamp, _matTrunk, _matLeaves;
        readonly Dictionary<Zone, Material[]> _facades = new Dictionary<Zone, Material[]>();

        void OnEnable()
        {
            Main = this;
            Rebuild();
        }

        void OnDisable()
        {
            if (Main == this)
                Main = null;
            Clear();
        }

        void OnValidate() => QueueRebuild();

        /// <summary>Rebuilds once, a moment later, however many settings changed.</summary>
        void QueueRebuild()
        {
#if UNITY_EDITOR
            if (_rebuildQueued || !isActiveAndEnabled)
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

        // --- Measurements other scripts use ------------------------------------------

        /// <summary>Distance from the city centre to the outer edge of the road grid (x, z).</summary>
        public Vector2 HalfExtents
        {
            get
            {
                float pitch = blockSize + roadWidth;
                return new Vector2(blocksX * pitch + roadWidth, blocksZ * pitch + roadWidth) / 2f;
            }
        }

        /// <summary>
        /// How far each zebra crossing is from the middle of its junction. It lines
        /// up with the middle of the pavement, where pedestrians walk.
        /// </summary>
        public float CrossingOffset => roadWidth / 2f + pavementWidth / 2f;

        /// <summary>The square a block covers, seen from above (x = x, y = z, in metres).</summary>
        public Rect BlockRect(int bx, int bz) =>
            new Rect(RoadX(bx) + roadWidth / 2f, RoadZ(bz) + roadWidth / 2f, blockSize, blockSize);

        public Zone BlockZone(int bx, int bz) =>
            _blockZones.TryGetValue(new Vector2Int(bx, bz), out var zone) ? zone : Zone.City;

        /// <summary>Number of junctions across and deep (one more than the number of blocks).</summary>
        public Vector2Int JunctionCount => new Vector2Int(blocksX + 1, blocksZ + 1);

        /// <summary>Centre of junction (i, j) at road level.</summary>
        public Vector3 JunctionPosition(int i, int j) => new Vector3(RoadX(i), 0f, RoadZ(j));

        /// <summary>Centre line of road number <paramref name="index"/> (0 = westmost).</summary>
        float RoadX(int index) => -HalfExtents.x + roadWidth / 2f + index * (blockSize + roadWidth);

        /// <summary>Centre line of road number <paramref name="index"/> (0 = southmost).</summary>
        float RoadZ(int index) => -HalfExtents.y + roadWidth / 2f + index * (blockSize + roadWidth);

        // --- Building --------------------------------------------------------------------

        void Clear()
        {
            // Also catch leftovers, e.g. from before scripts were reloaded.
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i);
                if (child.name == "Generated")
                    ModelUtil.SafeDestroy(child.gameObject);
            }
            _generated = null;
            foreach (var obj in _owned)
                ModelUtil.SafeDestroy(obj);
            _owned.Clear();
            _facades.Clear();
        }

        public void Rebuild()
        {
            Clear();
            _generated = new GameObject("Generated");
            _generated.transform.SetParent(transform, false);
            // Only build collision while playing; the editor doesn't need it.
            if (Application.isPlaying)
            {
                _collision = new GameObject("Collision");
                _collision.transform.SetParent(_generated.transform, false);
                _collision.isStatic = true;
            }
            else
            {
                _collision = null;
            }
            _batcher.Clear();
            _blockZones.Clear();
            _rng = new System.Random(randomSeed);

            MakeMaterials();
            _models[Zone.City] = Gather(cityModelsFolder, cityModels);
            _models[Zone.Suburban] = Gather(suburbanModelsFolder, suburbanModels);
            _models[Zone.Industrial] = Gather(industrialModelsFolder, industrialModels);

            BuildGround();
            for (int bx = 0; bx < blocksX; bx++)
            {
                for (int bz = 0; bz < blocksZ; bz++)
                    BuildBlock(bx, bz);
            }
            BuildRoadMarkings();
            BuildBoundary();
            _owned.AddRange(_batcher.Flush(_generated.transform));

            if (!Application.isPlaying)
            {
                // Don't save thousands of generated objects into the scene file.
                ModelUtil.SetHideFlagsRecursively(_generated, HideFlags.DontSave | HideFlags.NotEditable);
                _generated.hideFlags = HideFlags.DontSave | HideFlags.HideInHierarchy | HideFlags.NotEditable;
            }
        }

        static List<GameObject> Gather(string folder, List<GameObject> extra)
        {
            var result = AssetLibrary.ModelsInFolder(folder);
            if (extra != null)
            {
                foreach (var model in extra)
                {
                    if (model != null && !result.Contains(model))
                        result.Add(model);
                }
            }
            return result;
        }

        float RandRange(float min, float max) => min + (float)_rng.NextDouble() * (max - min);
        float Randf() => (float)_rng.NextDouble();
        int RandInt(int count) => _rng.Next(count);

        // --- Ground and roads -------------------------------------------------------------

        void BuildGround()
        {
            var half = HalfExtents;
            // One big slab of asphalt under the whole grid; blocks sit on top of it.
            AddBox(new Vector3(0, -0.1f, 0), new Vector3(half.x * 2f, 0.2f, half.y * 2f), _matRoad, true);
            // Grass all around the city (kept well below the road so they don't flicker).
            AddBox(new Vector3(0, -0.4f, 0), new Vector3(half.x * 2f + 200f, 0.2f, half.y * 2f + 200f), _matGrass, true);
        }

        void BuildRoadMarkings()
        {
            const float crossingLength = 3f;
            float gap = CrossingOffset + crossingLength / 2f + 1f; // Keep dashes clear of crossings.

            // Roads running east-west (one per row of junctions), then north-south.
            for (int j = 0; j <= blocksZ; j++)
            {
                float z = RoadZ(j);
                for (int i = 0; i < blocksX; i++)
                {
                    float end = RoadX(i + 1) - gap;
                    for (float x = RoadX(i) + gap; x + 3f <= end; x += 6f)
                        AddBox(new Vector3(x + 1.5f, 0f, z), new Vector3(3f, 0.08f, 0.15f), _matWhite);
                }
            }
            for (int i = 0; i <= blocksX; i++)
            {
                float x = RoadX(i);
                for (int j = 0; j < blocksZ; j++)
                {
                    float end = RoadZ(j + 1) - gap;
                    for (float z = RoadZ(j) + gap; z + 3f <= end; z += 6f)
                        AddBox(new Vector3(x, 0f, z + 1.5f), new Vector3(0.15f, 0.08f, 3f), _matWhite);
                }
            }

            // Zebra crossings on every road leading out of every junction.
            for (int i = 0; i <= blocksX; i++)
            {
                for (int j = 0; j <= blocksZ; j++)
                {
                    var centre = JunctionPosition(i, j);
                    float offset = CrossingOffset;
                    if (i < blocksX) AddZebra(centre + new Vector3(offset, 0, 0), true);
                    if (i > 0) AddZebra(centre - new Vector3(offset, 0, 0), true);
                    if (j < blocksZ) AddZebra(centre + new Vector3(0, 0, offset), false);
                    if (j > 0) AddZebra(centre - new Vector3(0, 0, offset), false);
                }
            }
        }

        /// <summary>A zebra crossing: white stripes running the same way as the traffic.</summary>
        void AddZebra(Vector3 centre, bool roadRunsAlongX)
        {
            int stripes = (int)roadWidth;
            for (int s = 0; s < stripes; s++)
            {
                float across = -roadWidth / 2f + 0.5f + s;
                if (roadRunsAlongX)
                    AddBox(centre + new Vector3(0, 0, across), new Vector3(3f, 0.08f, 0.5f), _matWhite);
                else
                    AddBox(centre + new Vector3(across, 0, 0), new Vector3(0.5f, 0.08f, 3f), _matWhite);
            }
        }

        /// <summary>Invisible walls just outside the grass so nobody drives off the world.</summary>
        void BuildBoundary()
        {
            var half = HalfExtents + new Vector2(60f, 60f);
            foreach (float side in new[] { -1f, 1f })
            {
                AddCollisionBox(new Vector3(half.x * side, 5f, 0f), new Vector3(1f, 20f, half.y * 2f));
                AddCollisionBox(new Vector3(0f, 5f, half.y * side), new Vector3(half.x * 2f, 20f, 1f));
            }
        }

        // --- Blocks -------------------------------------------------------------------------

        Zone ZoneFor(int bx, int bz)
        {
            if (industrialEdge && blocksX >= 3 && bx == blocksX - 1)
                return Zone.Industrial;
            bool outer = bx == 0 || bz == 0 || bx == blocksX - 1 || bz == blocksZ - 1;
            if (outer && blocksX >= 3 && blocksZ >= 3)
                return Zone.Suburban;
            if (Randf() < parkChance)
                return Zone.Park;
            return Zone.City;
        }

        void BuildBlock(int bx, int bz)
        {
            var zone = ZoneFor(bx, bz);
            _blockZones[new Vector2Int(bx, bz)] = zone;
            var rect = BlockRect(bx, bz);
            float x0 = rect.xMin; // West edge of the block.
            float z0 = rect.yMin; // South edge of the block.
            var centre = new Vector3(x0 + blockSize / 2f, 0f, z0 + blockSize / 2f);

            // Raised pavement slab covering the whole block.
            AddBox(centre + new Vector3(0, PavementHeight - 0.1f, 0), new Vector3(blockSize, 0.2f, blockSize), _matPavement, true);

            if (streetLamps)
                BuildLamps(x0, z0);

            if (zone == Zone.Park)
            {
                BuildPark(centre);
                return;
            }

            // Buildings stand in rows along all four sides of the block, facing the road.
            float inner = blockSize - pavementWidth * 2f;
            float depth = PlotDepth(zone, inner);
            float lo = pavementWidth; // Distance from the block edge to the building line.
            // South and north rows run the full width of the block...
            BuildRow(zone, new Vector3(x0 + lo, 0, z0 + lo), Vector3.right, Vector3.forward, inner, depth);
            BuildRow(zone, new Vector3(x0 + blockSize - lo, 0, z0 + blockSize - lo), Vector3.left, Vector3.back, inner, depth);
            // ...and the east and west rows fill the gap between them.
            float sideLength = inner - depth * 2f;
            if (sideLength > 6f)
            {
                BuildRow(zone, new Vector3(x0 + lo, 0, z0 + blockSize - lo - depth), Vector3.back, Vector3.right, sideLength, depth);
                BuildRow(zone, new Vector3(x0 + blockSize - lo, 0, z0 + lo + depth), Vector3.forward, Vector3.left, sideLength, depth);
            }
        }

        float PlotDepth(Zone zone, float inner)
        {
            float wanted = zone == Zone.Suburban ? 12f : zone == Zone.Industrial ? 20f : 16f;
            return Mathf.Min(wanted, inner / 2f);
        }

        /// <summary>
        /// Fills one side of a block with buildings. <paramref name="start"/> is the
        /// corner where the row begins (on the building line), <paramref name="along"/>
        /// the direction the row runs and <paramref name="inward"/> points from the
        /// road into the block.
        /// </summary>
        void BuildRow(Zone zone, Vector3 start, Vector3 along, Vector3 inward, float length, float depth)
        {
            float used = 0f;
            while (length - used > 4f)
            {
                float width;
                switch (zone)
                {
                    case Zone.Suburban: width = RandRange(8f, 12f); break;
                    case Zone.Industrial: width = RandRange(16f, 28f); break;
                    default: width = RandRange(9f, 18f); break;
                }
                if (length - used - width < 6f)
                    width = length - used; // Stretch the last plot to the end of the row.

                float gap = zone == Zone.Suburban ? Mathf.Min(2f, width * 0.2f) : 0f; // Space between houses.
                var plotCentre = start + along * (used + width / 2f) + inward * (depth / 2f);
                BuildBuilding(zone, plotCentre, inward, width - gap, depth);
                used += width;
            }
        }

        void BuildBuilding(Zone zone, Vector3 plotCentre, Vector3 inward, float width, float depth)
        {
            // Turn the plot so its forward (+Z) points at the road, the way models usually face.
            var plotRotation = Quaternion.LookRotation(-inward, Vector3.up);
            plotCentre.y = PavementHeight;

            var models = _models[zone];
            if (models.Count > 0)
            {
                PlaceModel(models[RandInt(models.Count)], plotCentre, plotRotation, width, depth);
                return;
            }

            float height;
            switch (zone)
            {
                case Zone.Suburban: height = RandRange(6f, 9f); break;
                case Zone.Industrial: height = RandRange(7f, 12f); break;
                default:
                    var heights = new[] { 9f, 12f, 12f, 15f, 18f, 21f, 27f };
                    height = heights[RandInt(heights.Length)];
                    break;
            }
            var size = new Vector3(width, height, depth);
            var walls = _facades[zone];
            var middle = plotCentre + Vector3.up * height / 2f;
            _batcher.AddBox(walls[RandInt(walls.Length)], middle, size, plotRotation);
            // A flat roof so the window pattern doesn't show on top.
            _batcher.AddBox(_matRoof, plotCentre + Vector3.up * (height + 0.2f), new Vector3(width + 0.3f, 0.4f, depth + 0.3f), plotRotation);
            AddCollisionBox(middle, ModelUtil.RotatedSize(size, plotRotation));
        }

        void PlaceModel(GameObject prefab, Vector3 plotCentre, Quaternion plotRotation, float width, float depth)
        {
            var wrapper = ModelUtil.Spawn(prefab);
            var bounds = ModelUtil.MeasureLocal(wrapper);
            if (bounds.size.x <= 0f || bounds.size.z <= 0f)
            {
                ModelUtil.SafeDestroy(wrapper);
                return;
            }
            float scale = fitModelsToPlots ? Mathf.Min(width / bounds.size.x, depth / bounds.size.z) : modelScale;
            var rotation = plotRotation * Quaternion.Euler(0f, flipModels ? 180f : 0f, 0f);
            // Moves the model so the bottom-centre of its box sits on the plot centre.
            ModelUtil.Place(wrapper, _generated.transform, plotCentre, rotation, scale);
            var size = bounds.size * scale;
            AddCollisionBox(plotCentre + Vector3.up * size.y / 2f, ModelUtil.RotatedSize(size, rotation));
        }

        void BuildPark(Vector3 centre)
        {
            float grassSize = blockSize - pavementWidth * 2f;
            AddBox(centre + new Vector3(0, PavementHeight, 0), new Vector3(grassSize, 0.2f, grassSize), _matGrass);
            int trees = (int)(grassSize * grassSize / 120f);
            for (int t = 0; t < trees; t++)
            {
                var pos = centre + new Vector3(
                    RandRange(-grassSize, grassSize) * 0.42f, PavementHeight,
                    RandRange(-grassSize, grassSize) * 0.42f);
                float treeHeight = RandRange(4f, 7f);
                AddBox(pos + Vector3.up * treeHeight / 2f, new Vector3(0.4f, treeHeight, 0.4f), _matTrunk);
                float crown = RandRange(3f, 5f);
                AddBox(pos + Vector3.up * (treeHeight + crown * 0.3f), new Vector3(crown, crown * 0.8f, crown), _matLeaves);
                AddCollisionBox(pos + Vector3.up * treeHeight / 2f, new Vector3(0.4f, treeHeight, 0.4f));
            }
        }

        void BuildLamps(float x0, float z0)
        {
            const float inset = 0.6f; // Distance from the kerb.
            const float spacing = 20f;
            int count = Mathf.Max(1, (int)(blockSize / spacing));
            for (int k = 0; k < count; k++)
            {
                float t = (k + 0.5f) * blockSize / count;
                AddLamp(new Vector3(x0 + t, 0, z0 + inset));
                AddLamp(new Vector3(x0 + t, 0, z0 + blockSize - inset));
                AddLamp(new Vector3(x0 + inset, 0, z0 + t));
                AddLamp(new Vector3(x0 + blockSize - inset, 0, z0 + t));
            }
        }

        void AddLamp(Vector3 pos)
        {
            pos.y = PavementHeight;
            AddBox(pos + Vector3.up * 2.75f, new Vector3(0.15f, 5.5f, 0.15f), _matLamp);
            AddBox(pos + Vector3.up * 5.6f, new Vector3(0.5f, 0.25f, 0.5f), _matLamp);
        }

        // --- Helpers ---------------------------------------------------------------------------

        /// <summary>
        /// Queues a box to be drawn. Boxes with the same material are drawn
        /// together, which keeps the game fast even with thousands of them.
        /// </summary>
        void AddBox(Vector3 centre, Vector3 size, Material material, bool solid = false)
        {
            _batcher.AddBox(material, centre, size, Quaternion.identity);
            if (solid)
                AddCollisionBox(centre, size);
        }

        void AddCollisionBox(Vector3 centre, Vector3 size)
        {
            if (_collision == null)
                return;
            var box = _collision.AddComponent<BoxCollider>();
            box.center = centre;
            box.size = size;
        }

        Material Own(Material material, string name)
        {
            material.name = name;
            _owned.Add(material);
            return material;
        }

        void MakeMaterials()
        {
            _matRoad = Own(LondonMaterials.Create(new Color(0.18f, 0.18f, 0.2f)), "Road");
            _matPavement = Own(LondonMaterials.Create(new Color(0.42f, 0.42f, 0.4f)), "Pavement");
            _matGrass = Own(LondonMaterials.Create(new Color(0.25f, 0.38f, 0.2f)), "Grass");
            _matWhite = Own(LondonMaterials.Create(new Color(0.92f, 0.92f, 0.88f)), "Road Markings");
            _matRoof = Own(LondonMaterials.Create(new Color(0.22f, 0.22f, 0.24f)), "Roof");
            _matLamp = Own(LondonMaterials.Create(new Color(0.12f, 0.14f, 0.12f), 0.5f), "Lamp Post");
            _matTrunk = Own(LondonMaterials.Create(new Color(0.3f, 0.22f, 0.15f)), "Tree Trunk");
            _matLeaves = Own(LondonMaterials.Create(new Color(0.2f, 0.42f, 0.18f)), "Leaves");

            var glass = new Color(0.22f, 0.27f, 0.33f);
            _facades[Zone.City] = new[]
            {
                Facade("Red Brick", new Color(0.55f, 0.27f, 0.2f), glass, 3.5f),
                Facade("Orange Brick", new Color(0.62f, 0.36f, 0.26f), glass, 3.5f),
                Facade("Portland Stone", new Color(0.82f, 0.78f, 0.68f), glass, 3.8f),
                Facade("Grey Stone", new Color(0.7f, 0.66f, 0.6f), glass, 3.8f),
                Facade("Glass Office", new Color(0.45f, 0.5f, 0.55f), new Color(0.5f, 0.65f, 0.75f), 3.5f, true),
            };
            _facades[Zone.Suburban] = new[]
            {
                Facade("House Brick", new Color(0.6f, 0.32f, 0.24f), glass, 3f),
                Facade("White Render", new Color(0.85f, 0.82f, 0.75f), glass, 3f),
                Facade("Dark Brick", new Color(0.5f, 0.3f, 0.25f), glass, 3f),
            };
            _facades[Zone.Industrial] = new[]
            {
                Corrugated("Grey Metal", new Color(0.5f, 0.52f, 0.5f)),
                Corrugated("Blue Metal", new Color(0.35f, 0.42f, 0.48f)),
                Corrugated("Brown Metal", new Color(0.55f, 0.45f, 0.35f)),
            };
        }

        /// <summary>A wall texture with one window every <paramref name="floorHeight"/> metres.</summary>
        Material Facade(string name, Color wall, Color window, float floorHeight, bool bigWindows = false)
        {
            // Drawn on a 32 x 32 grid, then enlarged 8 times so window edges stay sharp.
            return Textured(name, floorHeight, (x, y) =>
            {
                bool isWindow = bigWindows
                    ? x >= 2 && x < 30 && y >= 4 && y < 28
                    : x >= 9 && x < 23 && y >= 8 && y < 26;
                return isWindow ? window : wall;
            });
        }

        Material Corrugated(string name, Color wall)
        {
            var dark = new Color(wall.r * 0.75f, wall.g * 0.75f, wall.b * 0.75f);
            return Textured(name, 2f, (x, y) => x % 4 == 0 ? dark : wall);
        }

        Material Textured(string name, float tileSize, System.Func<int, int, Color> pixel)
        {
            const int size = 256;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, true)
            {
                name = name,
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Trilinear,
                anisoLevel = 4,
                hideFlags = HideFlags.DontSave,
            };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                    pixels[y * size + x] = pixel(x / 8, y / 8);
            }
            texture.SetPixels32(pixels);
            texture.Apply(true);
            _owned.Add(texture);
            var material = Own(LondonMaterials.Create(Color.white, 0.85f, 0f, texture), name);
            _batcher.SetTileSize(material, tileSize);
            return material;
        }

        void OnDrawGizmosSelected()
        {
            var half = HalfExtents;
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(transform.position, new Vector3(half.x * 2f, 1f, half.y * 2f));
        }
    }
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LondonStreets
{
    /// <summary>
    /// Fills the pavements with people. They walk round the blocks, cross the
    /// roads on the zebra crossings (and traffic stops for them), wait for cars
    /// to pass, step aside for you, and jump out of the way of a car you drive
    /// at them.
    ///
    /// Pedestrians only appear when you play the game, not in the editor.
    /// </summary>
    public class Pedestrians : MonoBehaviour
    {
        public static Pedestrians Instance { get; private set; }

        [Tooltip("The City whose pavements they walk on.")]
        public CityGrid city;
        [Tooltip("How many people are out and about. They stay near you: anyone who ends up far away quietly reappears somewhere closer.")]
        [Range(0, 300)] public int count = 80;
        [Tooltip("People further away than this (metres) are moved closer to you.")]
        public float keepWithin = 160f;
        [Tooltip("Folder of character models for pedestrians. If it's empty, the characters found in the project (except yours) are used (see found_assets.txt), and if there are none, simple coloured people.")]
        [FolderPath] public string charactersFolder = "";
        [Tooltip("Tick this if your pedestrian models walk backwards.")]
        public bool flipModels;
        [Tooltip("Optional: walk and idle animations for every pedestrian. Leave empty to use the ones inside each model.")]
        public AnimationClip walkAnimation;
        public AnimationClip idleAnimation;

        public readonly List<Pedestrian> people = new List<Pedestrian>();
        float _checkTimer;
        List<GameObject> _models = new List<GameObject>();

        /// <summary>
        /// The walking network. Every block has a corner point in the middle of the
        /// pavement at each of its four corners. People walk from corner to corner
        /// round the block, or straight across the road to the facing corner of the
        /// next block (over a zebra). A corner is (block x, block z, corner number 0-3):
        /// 0 = south-west, 1 = south-east, 2 = north-east, 3 = north-west.
        /// </summary>
        static readonly Vector2Int[] CornerSigns = { new Vector2Int(-1, -1), new Vector2Int(1, -1), new Vector2Int(1, 1), new Vector2Int(-1, 1) };

        public struct Walk
        {
            public Vector3Int corner;
            public bool crossesRoad;
        }

        void Awake() => Instance = this;

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        IEnumerator Start()
        {
            if (city == null)
                city = CityGrid.Main;
            if (city == null)
            {
                Debug.LogWarning("Pedestrians: set 'City' in the Inspector.");
                yield break;
            }
            _models = AssetLibrary.ModelsInFolder(charactersFolder);
            if (_models.Count == 0)
                _models = AssetLibrary.OtherCharacters;

            yield return null;
            for (int k = 0; k < count; k++)
            {
                var go = new GameObject("Pedestrian");
                go.transform.SetParent(transform, false);
                var person = go.AddComponent<Pedestrian>();
                person.Init(this,
                    walkSpeed: Random.Range(1.1f, 1.6f),
                    height: Random.Range(1.6f, 1.9f),
                    sideOffset: Random.Range(0.3f, 0.9f),
                    model: ModelUtil.PickRandom(_models),
                    flipModel: flipModels);
                people.Add(person);
                PlaceRandomly(person, 0f, keepWithin * 0.8f);
            }
        }

        /// <summary>Every second, moves anyone who's wandered far away back near the player.</summary>
        void Update()
        {
            _checkTimer -= Time.deltaTime;
            if (_checkTimer > 0f)
                return;
            _checkTimer = 1f;
            var centre = PlayerPosition();
            foreach (var person in people)
            {
                if (Vector3.Distance(person.transform.position, centre) > keepWithin)
                    PlaceRandomly(person, 50f, keepWithin * 0.75f);
            }
        }

        static Vector3 PlayerPosition()
        {
            var player = PlayerController.Instance;
            return player != null ? player.FocusPosition : Vector3.zero;
        }

        // --- The walking network ---------------------------------------------------------------

        public Vector3 CornerPosition(Vector3Int corner)
        {
            var rect = city.BlockRect(corner.x, corner.y);
            float inset = city.pavementWidth / 2f;
            var signs = CornerSigns[corner.z];
            float x = signs.x < 0 ? rect.xMin + inset : rect.xMax - inset;
            float z = signs.y < 0 ? rect.yMin + inset : rect.yMax - inset;
            return new Vector3(x, CityGrid.PavementHeight, z);
        }

        static Vector3Int CornerWithSigns(int bx, int bz, Vector2Int signs) =>
            new Vector3Int(bx, bz, System.Array.IndexOf(CornerSigns, signs));

        /// <summary>Corners you can walk to from <paramref name="corner"/>, and whether that walk crosses a road.</summary>
        public List<Walk> NextCorners(Vector3Int corner)
        {
            var result = new List<Walk>();
            var signs = CornerSigns[corner.z];
            // Along the two sides of this block.
            result.Add(new Walk { corner = CornerWithSigns(corner.x, corner.y, new Vector2Int(-signs.x, signs.y)) });
            result.Add(new Walk { corner = CornerWithSigns(corner.x, corner.y, new Vector2Int(signs.x, -signs.y)) });
            // Across the road to the neighbouring blocks.
            int acrossX = corner.x + signs.x;
            if (acrossX >= 0 && acrossX < city.blocksX)
                result.Add(new Walk { corner = CornerWithSigns(acrossX, corner.y, new Vector2Int(-signs.x, signs.y)), crossesRoad = true });
            int acrossZ = corner.y + signs.y;
            if (acrossZ >= 0 && acrossZ < city.blocksZ)
                result.Add(new Walk { corner = CornerWithSigns(corner.x, acrossZ, new Vector2Int(signs.x, -signs.y)), crossesRoad = true });
            return result;
        }

        /// <summary>
        /// Picks where to walk after reaching <paramref name="corner"/>, not straight
        /// back to <paramref name="cameFrom"/> unless there's no other way.
        /// </summary>
        public Walk ChooseNext(Vector3Int corner, Vector3Int cameFrom)
        {
            var options = NextCorners(corner);
            if (options.Count > 1)
                options.RemoveAll(o => o.corner == cameFrom);
            return options[Random.Range(0, options.Count)];
        }

        /// <summary>
        /// Puts <paramref name="person"/> on a random pavement between
        /// <paramref name="minDistance"/> and <paramref name="maxDistance"/> metres from the player.
        /// </summary>
        void PlaceRandomly(Pedestrian person, float minDistance, float maxDistance)
        {
            var centre = PlayerPosition();
            var corner = Vector3Int.zero;
            for (int attempt = 0; attempt < 30; attempt++)
            {
                corner = new Vector3Int(Random.Range(0, city.blocksX), Random.Range(0, city.blocksZ), Random.Range(0, 4));
                float distance = Vector3.Distance(CornerPosition(corner), centre);
                if (distance >= minDistance && distance <= maxDistance)
                    break;
            }
            var options = NextCorners(corner).FindAll(o => !o.crossesRoad);
            var target = options[Random.Range(0, options.Count)].corner;
            person.StartWalk(corner, target, false, Random.value);
        }

        // --- What people need to know about the world ---------------------------------------------

        /// <summary>
        /// True if it isn't safe to step onto the road between <paramref name="from"/>
        /// and <paramref name="to"/>. After waiting <paramref name="patienceUsed"/>
        /// seconds people get impatient and only wait for cars that are really close
        /// (cars stop for people on zebras).
        /// </summary>
        public bool IsRoadBusy(Vector3 from, Vector3 to, float patienceUsed)
        {
            var middle = (from + to) / 2f;
            var traffic = Traffic.Instance;
            if (traffic != null)
            {
                foreach (var car in traffic.cars)
                {
                    if (!car.Placed)
                        continue;
                    float distance = Vector3.Distance(car.Position, middle);
                    if (patienceUsed > 15f)
                    {
                        if (distance < 9f + car.HalfLength)
                            return true;
                        continue;
                    }
                    // Close, or coming fast, or turning through the junction next to us.
                    if (distance < 10f + car.HalfLength + car.Speed * 1.5f)
                        return true;
                    if (car.InJunction && distance < 25f)
                        return true;
                }
            }
            foreach (var vehicle in Car.All)
            {
                if (vehicle.Velocity.magnitude > 1f && Vector3.Distance(vehicle.transform.position, middle) < 15f)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// True if a stopped traffic car is right in front of <paramref name="position"/>,
        /// in the way of someone walking along <paramref name="direction"/>.
        /// </summary>
        public bool IsBlockedByCar(Vector3 position, Vector3 direction)
        {
            var traffic = Traffic.Instance;
            if (traffic == null)
                return false;
            var step = position + direction * 0.8f;
            foreach (var car in traffic.cars)
            {
                if (!car.Placed || car.Speed > 0.5f)
                    continue;
                var offset = step - car.Position;
                var right = Vector3.Cross(Vector3.up, car.Forward);
                if (Mathf.Abs(Vector3.Dot(offset, car.Forward)) < car.HalfLength + 0.4f && Mathf.Abs(Vector3.Dot(offset, right)) < 1.3f)
                    return true;
            }
            return false;
        }

        /// <summary>A car being driven fast towards <paramref name="position"/>, if there is one.</summary>
        public Car DangerousVehicleNear(Vector3 position)
        {
            foreach (var vehicle in Car.All)
            {
                var velocity = vehicle.Velocity;
                velocity.y = 0f;
                if (velocity.magnitude < 3f)
                    continue;
                var offset = position - vehicle.transform.position;
                offset.y = 0f;
                var heading = velocity.normalized;
                float ahead = Vector3.Dot(offset, heading);
                if (ahead < -1f || ahead > 4f + velocity.magnitude * 0.6f)
                    continue;
                float side = (offset - heading * ahead).magnitude;
                if (side < 2.2f)
                    return vehicle;
            }
            return null;
        }

        /// <summary>The player if they're walking (not driving), otherwise null.</summary>
        public static PlayerController PlayerOnFoot()
        {
            var player = PlayerController.Instance;
            return player != null && !player.IsDriving ? player : null;
        }
    }
}

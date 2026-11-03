using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LondonStreets
{
    /// <summary>
    /// Fills the city's roads with traffic: cars and red buses that drive on the
    /// left, turn at junctions, queue behind each other and stop for you.
    ///
    /// Traffic only appears when you play the game, not in the editor.
    /// </summary>
    public class Traffic : MonoBehaviour
    {
        public static Traffic Instance { get; private set; }

        [Tooltip("The City whose roads the traffic drives on.")]
        public CityGrid city;
        [Tooltip("How many cars to put on the roads.")]
        [Range(0, 200)] public int carCount = 30;
        [Tooltip("How many red buses to put on the roads.")]
        [Range(0, 50)] public int busCount = 3;
        [Tooltip("Folder of car models to use for traffic (one is picked at random for each car). If it's empty, every car found in the project is used instead (see found_assets.txt), and if there are none, simple box cars.")]
        [FolderPath] public string carModelsFolder = AssetLibrary.CarsFolder;
        [Tooltip("Tick this if your car models drive backwards.")]
        public bool flipCarModels;
        public float cruiseSpeedKmh = 40f;

        public readonly List<TrafficCar> cars = new List<TrafficCar>();
        readonly Dictionary<Vector2Int, TrafficCar> _junctionOwner = new Dictionary<Vector2Int, TrafficCar>();
        readonly Dictionary<Vector2Int, List<TrafficCar>> _junctionQueue = new Dictionary<Vector2Int, List<TrafficCar>>();
        List<GameObject> _carModels = new List<GameObject>();

        static readonly Vector2Int[] Steps = { new Vector2Int(1, 0), new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(0, -1) };

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
                Debug.LogWarning("Traffic: set 'City' in the Inspector.");
                yield break;
            }
            _carModels = AssetLibrary.ModelsInFolder(carModelsFolder);
            if (_carModels.Count == 0)
                _carModels = AssetLibrary.Cars;

            // Wait one frame so the city and the player are ready.
            yield return null;
            for (int k = 0; k < carCount + busCount; k++)
            {
                bool isBus = k < busCount;
                var go = new GameObject(isBus ? "Bus" : "Traffic Car");
                go.transform.SetParent(transform, false);
                var car = go.AddComponent<TrafficCar>();
                float speed = cruiseSpeedKmh / 3.6f * (isBus ? 0.8f : Random.Range(0.85f, 1.15f));
                var model = isBus ? null : ModelUtil.PickRandom(_carModels);
                car.Init(this, isBus, speed, model, flipCarModels);
                cars.Add(car);
                Respawn(car, 12f);
            }
        }

        // --- Routes ------------------------------------------------------------------------

        public float LaneOffset => city.roadWidth / 4f;
        public float JunctionHalfSize => city.roadWidth / 2f;
        /// <summary>How far from the middle of a junction cars stop: just before the zebra.</summary>
        public float StopLineDistance => city.CrossingOffset + 2f;

        public Vector3 JunctionPosition(Vector2Int junction) => city.JunctionPosition(junction.x, junction.y);

        /// <summary>The direction 90 degrees to the left of <paramref name="direction"/>.</summary>
        public static Vector3 LeftOf(Vector3 direction) => Vector3.Cross(direction, Vector3.up);

        /// <summary>Junctions you can drive to from <paramref name="junction"/>.</summary>
        public List<Vector2Int> Neighbours(Vector2Int junction)
        {
            var size = city.JunctionCount;
            var result = new List<Vector2Int>();
            foreach (var step in Steps)
            {
                var next = junction + step;
                if (next.x >= 0 && next.y >= 0 && next.x < size.x && next.y < size.y)
                    result.Add(next);
            }
            return result;
        }

        /// <summary>Picks where to go after <paramref name="to"/>, never turning back unless it must.</summary>
        public Vector2Int ChooseNext(Vector2Int from, Vector2Int to)
        {
            var options = Neighbours(to);
            if (options.Count > 1)
                options.Remove(from);
            return options[Random.Range(0, options.Count)];
        }

        /// <summary>
        /// Where the middle of a car <paramref name="halfLength"/> metres long is once
        /// it has fully left junction <paramref name="from"/> towards <paramref name="to"/>,
        /// in the left-hand lane.
        /// </summary>
        public Vector3 LaneStart(Vector2Int from, Vector2Int to, float halfLength)
        {
            var a = JunctionPosition(from);
            var direction = (JunctionPosition(to) - a).normalized;
            return a + direction * (JunctionHalfSize + halfLength) + LeftOf(direction) * LaneOffset;
        }

        /// <summary>
        /// Puts <paramref name="car"/> somewhere on the road at least
        /// <paramref name="minPlayerDistance"/> metres from the player and away from other cars.
        /// </summary>
        public bool Respawn(TrafficCar car, float minPlayerDistance)
        {
            var size = city.JunctionCount;
            var playerPos = PlayerPosition();
            for (int attempt = 0; attempt < 50; attempt++)
            {
                var from = new Vector2Int(Random.Range(0, size.x), Random.Range(0, size.y));
                var options = Neighbours(from);
                if (options.Count == 0)
                    continue;
                var to = options[Random.Range(0, options.Count)];
                var start = LaneStart(from, to, car.HalfLength);
                if (Vector3.Distance(start, playerPos) < minPlayerDistance)
                    continue;
                if (IsSpotTaken(start, car))
                    continue;
                ReleaseJunctions(car);
                car.StartRoute(from, to, ChooseNext(from, to));
                return true;
            }
            return false;
        }

        bool IsSpotTaken(Vector3 spot, TrafficCar ignore)
        {
            foreach (var other in cars)
            {
                if (other != ignore && other.Placed && Vector3.Distance(other.Position, spot) < 14f)
                    return true;
            }
            foreach (var vehicle in Car.All)
            {
                if (Vector3.Distance(vehicle.transform.position, spot) < 8f)
                    return true;
            }
            return false;
        }

        // --- Junctions: one car at a time ---------------------------------------------------

        /// <summary>
        /// Asks to cross <paramref name="junction"/>. Cars queue up and go in the
        /// order they asked, so nobody waits for ever.
        /// </summary>
        public bool TryReserveJunction(Vector2Int junction, TrafficCar car)
        {
            if (_junctionOwner.TryGetValue(junction, out var owner) && owner == car)
                return true;
            if (!_junctionQueue.TryGetValue(junction, out var queue))
            {
                queue = new List<TrafficCar>();
                _junctionQueue[junction] = queue;
            }
            if (!queue.Contains(car))
                queue.Add(car);
            if (!_junctionOwner.ContainsKey(junction) && queue[0] == car)
            {
                queue.RemoveAt(0);
                _junctionOwner[junction] = car;
                return true;
            }
            return false;
        }

        public void ReleaseJunction(Vector2Int junction, TrafficCar car)
        {
            if (_junctionOwner.TryGetValue(junction, out var owner) && owner == car)
                _junctionOwner.Remove(junction);
        }

        /// <summary>Forgets every junction <paramref name="car"/> was crossing or waiting for.</summary>
        public void ReleaseJunctions(TrafficCar car)
        {
            var owned = new List<Vector2Int>();
            foreach (var pair in _junctionOwner)
            {
                if (pair.Value == car)
                    owned.Add(pair.Key);
            }
            foreach (var junction in owned)
                _junctionOwner.Remove(junction);
            foreach (var queue in _junctionQueue.Values)
                queue.Remove(car);
        }

        // --- Seeing what's ahead -------------------------------------------------------------

        /// <summary>Where the player is: on foot, or the car they're driving.</summary>
        public static Vector3 PlayerPosition()
        {
            var player = PlayerController.Instance;
            return player != null ? player.FocusPosition : new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
        }

        /// <summary>
        /// How far <paramref name="car"/> can drive before hitting something in its
        /// lane (infinity if the road ahead is clear).
        /// </summary>
        public float FreeDistanceAhead(TrafficCar car)
        {
            var forward = car.Forward;
            float nearest = float.PositiveInfinity;

            // Other traffic, ignoring cars coming the other way and cars waiting at a
            // junction we're already crossing (otherwise we'd wait for each other).
            foreach (var other in cars)
            {
                if (other == car || !other.Placed)
                    continue;
                if (Vector3.Dot(other.Forward, forward) < -0.3f)
                    continue;
                if (car.InJunction && other.WaitingAtJunction)
                    continue;
                nearest = Mathf.Min(nearest, GapTo(car, forward, other.Position, other.HalfLength, 1.9f));
            }

            // Parked or player-driven cars, and the player on foot.
            foreach (var vehicle in Car.All)
                nearest = Mathf.Min(nearest, GapTo(car, forward, vehicle.transform.position, 2.2f, 1.9f));
            var player = PlayerController.Instance;
            if (player != null && !player.IsDriving)
                nearest = Mathf.Min(nearest, GapTo(car, forward, player.transform.position, 0.4f, 1.5f));

            // People on a zebra crossing: like a real driver, stop for anyone on the
            // crossing ahead, not just someone already in our lane.
            var people = Pedestrians.Instance;
            if (people != null)
            {
                float watchWidth = city.roadWidth / 2f + 1f;
                foreach (var person in people.people)
                {
                    if (person.OnRoad)
                        nearest = Mathf.Min(nearest, GapTo(car, forward, person.transform.position, 0.4f, watchWidth));
                }
            }
            return nearest;
        }

        static float GapTo(TrafficCar car, Vector3 forward, Vector3 position, float otherHalfLength, float maxSide)
        {
            var offset = position - car.Position;
            offset.y = 0f;
            float ahead = Vector3.Dot(offset, forward);
            if (ahead <= 0f || ahead > 40f)
                return float.PositiveInfinity;
            float side = (offset - forward * ahead).magnitude;
            if (side > maxSide)
                return float.PositiveInfinity;
            return ahead - car.HalfLength - otherHalfLength;
        }
    }
}

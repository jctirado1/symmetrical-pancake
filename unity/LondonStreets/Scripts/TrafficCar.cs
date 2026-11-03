using System.Collections.Generic;
using UnityEngine;

namespace LondonStreets
{
    /// <summary>
    /// One computer-driven car (or bus). It follows the left-hand lane from
    /// junction to junction, picking a random way to go at each one. The Traffic
    /// object creates these, so you don't place them yourself.
    /// </summary>
    public class TrafficCar : MonoBehaviour
    {
        const float SecondsStuckBeforeRespawn = 8f;
        const float Acceleration = 4f;  // m/s²
        const float Braking = 9f;       // m/s²
        const float SafeGap = 2.5f;     // metres to leave behind whatever is in front
        const float TurnSpeed = 5f;     // m/s through corners

        public bool IsBus { get; private set; }
        public float HalfLength { get; private set; } = 2.2f;
        public float Speed { get; private set; }
        public bool WaitingAtJunction { get; private set; }
        public bool InJunction { get; private set; }
        /// <summary>False until the car has been put on a road.</summary>
        public bool Placed { get; private set; }
        public Vector3 Position => _position;
        public Vector3 Forward => _rotation * Vector3.forward;

        Traffic _manager;
        Rigidbody _body;
        float _cruiseSpeed = 11f;
        Vector2Int _from, _to, _next;
        readonly List<Vector3> _path = new List<Vector3>();
        int _target;        // Index of the path point we're driving towards.
        bool _turning;
        float _stuckTime;
        Vector3 _position;
        Quaternion _rotation = Quaternion.identity;

        static readonly Color[] Paint =
        {
            new Color(0.05f, 0.05f, 0.06f), // black cab
            new Color(0.05f, 0.05f, 0.06f),
            new Color(0.75f, 0.75f, 0.78f), // silver
            new Color(0.9f, 0.9f, 0.9f),    // white
            new Color(0.12f, 0.2f, 0.45f),  // blue
            new Color(0.5f, 0.08f, 0.08f),  // dark red
            new Color(0.15f, 0.3f, 0.2f),   // green
            new Color(0.3f, 0.3f, 0.32f),   // grey
        };

        static readonly Dictionary<string, Material> SharedMaterials = new Dictionary<string, Material>();

        public void Init(Traffic manager, bool isBus, float cruiseSpeed, GameObject model, bool flipModel)
        {
            _manager = manager;
            IsBus = isBus;
            _cruiseSpeed = cruiseSpeed;
            // We move the car ourselves; physics just needs to know where it is so you bump into it.
            _body = gameObject.AddComponent<Rigidbody>();
            _body.isKinematic = true;
            _body.interpolation = RigidbodyInterpolation.Interpolate;
            BuildBody(model, flipModel);
        }

        // --- Driving ------------------------------------------------------------------------

        /// <summary>
        /// Starts driving from junction <paramref name="from"/> towards <paramref name="to"/>,
        /// then on to <paramref name="next"/>.
        /// </summary>
        public void StartRoute(Vector2Int from, Vector2Int to, Vector2Int next)
        {
            _from = from;
            _to = to;
            _next = next;
            BuildPath();
            _position = _path[0];
            _rotation = Facing(_path[1] - _path[0]);
            // Jump straight there (no smooth movement).
            _body.position = _position;
            _body.rotation = _rotation;
            transform.SetPositionAndRotation(_position, _rotation);
            _target = 1;
            Speed = _cruiseSpeed * 0.5f;
            _stuckTime = 0f;
            InJunction = false;
            WaitingAtJunction = false;
            Placed = true;
        }

        /// <summary>
        /// The route for one stretch of road: the lane up to the junction, then a
        /// curve (or straight line) across it into the next road.
        /// </summary>
        void BuildPath()
        {
            // Positions are for the middle of the car, so allow for its length: it
            // stops with its front bumper before the zebra crossing and leaves the
            // junction once its tail is out.
            var start = _manager.LaneStart(_from, _to, HalfLength);
            var direction = (_manager.JunctionPosition(_to) - _manager.JunctionPosition(_from)).normalized;
            var stopLine = _manager.JunctionPosition(_to) - direction * (_manager.StopLineDistance + HalfLength)
                + Traffic.LeftOf(direction) * _manager.LaneOffset;
            var exit = _manager.LaneStart(_to, _next, HalfLength);

            _path.Clear();
            _path.Add(start);
            _path.Add(stopLine);
            var nextDirection = (_manager.JunctionPosition(_next) - _manager.JunctionPosition(_to)).normalized;
            _turning = Vector3.Dot(nextDirection, direction) < 0.9f;
            if (_turning)
            {
                // A smooth curve whose corner is where the two lanes would meet.
                var corner = stopLine + direction * Vector3.Dot(exit - stopLine, direction);
                for (int k = 1; k <= 8; k++)
                {
                    float t = k / 8f;
                    _path.Add(Vector3.Lerp(Vector3.Lerp(stopLine, corner, t), Vector3.Lerp(corner, exit, t), t));
                }
            }
            else
            {
                _path.Add(exit);
            }
        }

        void FixedUpdate()
        {
            if (!Placed)
                return;
            float dt = Time.fixedDeltaTime;

            // How fast do we want to go?
            float wanted = _cruiseSpeed;
            if (_target >= 2 && _turning)
                wanted = TurnSpeed;

            // Slow down for whatever is in front.
            float gap = _manager.FreeDistanceAhead(this) - SafeGap;
            wanted = Mathf.Min(wanted, SpeedToStopWithin(gap));

            // Approaching the junction: we need it to ourselves before we cross.
            WaitingAtJunction = false;
            if (_target == 1)
            {
                float toLine = Vector3.Distance(_position, _path[1]);
                // Only go if there's room on the far side, so we never block the junction.
                if (toLine < 12f && (gap < 10f || !_manager.TryReserveJunction(_to, this)))
                {
                    wanted = Mathf.Min(wanted, SpeedToStopWithin(toLine - 0.5f));
                    WaitingAtJunction = toLine < 3f;
                }
            }

            Speed = wanted < Speed ? Mathf.Max(wanted, Speed - Braking * dt) : Mathf.Min(wanted, Speed + Acceleration * dt);
            Drive(Speed * dt, dt);
            if (!Placed)
                return;
            _body.MovePosition(_position);
            _body.MoveRotation(_rotation);

            // Stuck for ages somewhere the player can't see? Start again elsewhere.
            _stuckTime = Speed < 0.3f ? _stuckTime + dt : 0f;
            if (_stuckTime > SecondsStuckBeforeRespawn)
            {
                _stuckTime = 0f;
                if (Vector3.Distance(_position, Traffic.PlayerPosition()) > 70f)
                    _manager.Respawn(this, 70f);
            }
        }

        static float SpeedToStopWithin(float distance)
        {
            if (distance <= 0f)
                return 0f;
            return Mathf.Sqrt(2f * Braking * 0.6f * distance);
        }

        /// <summary>Moves <paramref name="distance"/> metres along the path, turning to face where we go.</summary>
        void Drive(float distance, float dt)
        {
            var pos = _position;
            while (distance > 0f)
            {
                var target = _path[_target];
                float toTarget = Vector3.Distance(pos, target);
                if (toTarget > distance)
                {
                    pos += (target - pos) / toTarget * distance;
                    break;
                }
                pos = target;
                distance -= toTarget;
                _target++;
                if (_target == 2)
                    InJunction = true;
                if (_target >= _path.Count)
                {
                    FinishStretch();
                    return;
                }
            }

            var heading = _path[_target] - pos;
            heading.y = 0f;
            if (heading.sqrMagnitude > 0.0001f)
                _rotation = Quaternion.Slerp(_rotation, Facing(heading), Mathf.Clamp01(10f * dt));
            _position = pos;
        }

        /// <summary>Reached the far side of a junction: release it and head for the next one.</summary>
        void FinishStretch()
        {
            _manager.ReleaseJunction(_to, this);
            var keepRotation = _rotation;
            var keepSpeed = Speed;
            var from = _to;
            var to = _next;
            // Carry on from where we are, without the jump StartRoute does.
            _from = from;
            _to = to;
            _next = _manager.ChooseNext(from, to);
            BuildPath();
            _position = _path[0];
            _target = 1;
            InJunction = false;
            WaitingAtJunction = false;
            _rotation = keepRotation;
            Speed = keepSpeed;
        }

        /// <summary>A rotation that points the car's front (+Z) along <paramref name="direction"/>.</summary>
        static Quaternion Facing(Vector3 direction)
        {
            direction.y = 0f;
            return direction.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(direction.normalized, Vector3.up) : Quaternion.identity;
        }

        // --- Looks -------------------------------------------------------------------------------

        void BuildBody(GameObject model, bool flipModel)
        {
            var size = IsBus ? new Vector3(2.5f, 4.4f, 10f) : new Vector3(1.8f, 1.4f, 4.2f);
            HalfLength = size.z / 2f;

            if (model != null && !IsBus)
            {
                var wrapper = ModelUtil.Spawn(model);
                size = ModelUtil.FitVehicle(wrapper, transform, 4.3f, flipModel);
                HalfLength = size.z / 2f;
                AddCollision(size);
                return;
            }

            if (IsBus)
            {
                var glass = Material(new Color(0.15f, 0.2f, 0.25f), 0.1f);
                AddPart(new Vector3(2.5f, 4.1f, 10f), new Vector3(0, 2.35f, 0), Material(new Color(0.78f, 0.06f, 0.14f), 0.5f));
                AddPart(new Vector3(2.56f, 0.9f, 9f), new Vector3(0, 2f, 0.2f), glass);
                AddPart(new Vector3(2.56f, 0.9f, 9.4f), new Vector3(0, 3.6f, 0), glass);
            }
            else
            {
                var paint = Paint[Random.Range(0, Paint.Length)];
                AddPart(new Vector3(1.8f, 0.6f, 4.2f), new Vector3(0, 0.65f, 0), Material(paint, 0.35f));
                AddPart(new Vector3(1.6f, 0.55f, 2.2f), new Vector3(0, 1.22f, -0.25f), Material(new Color(0.15f, 0.2f, 0.25f), 0.1f));
            }
            // Wheels.
            float wheelX = size.x / 2f - 0.1f;
            float wheelZ = size.z / 2f - (IsBus ? 1.8f : 1f);
            var tyre = Material(new Color(0.05f, 0.05f, 0.05f), 0.9f);
            foreach (float x in new[] { -wheelX, wheelX })
            {
                foreach (float z in new[] { -wheelZ, wheelZ })
                {
                    var wheel = ModelUtil.Primitive(PrimitiveType.Cylinder, transform, new Vector3(x, 0.38f, z), new Vector3(0.76f, 0.125f, 0.76f), tyre);
                    wheel.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                }
            }
            AddCollision(size);
        }

        void AddCollision(Vector3 size)
        {
            var box = gameObject.AddComponent<BoxCollider>();
            box.size = new Vector3(size.x, size.y - 0.3f, size.z);
            box.center = new Vector3(0f, 0.3f + box.size.y / 2f, 0f);
        }

        void AddPart(Vector3 size, Vector3 position, Material material) =>
            ModelUtil.Primitive(PrimitiveType.Cube, transform, position, size, material);

        static Material Material(Color color, float roughness)
        {
            string key = $"{color} {roughness}";
            if (!SharedMaterials.TryGetValue(key, out var material) || material == null)
            {
                material = LondonMaterials.Create(color, roughness, 0.3f);
                SharedMaterials[key] = material;
            }
            return material;
        }
    }
}

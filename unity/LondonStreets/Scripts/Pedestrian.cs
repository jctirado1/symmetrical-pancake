using System.Collections.Generic;
using UnityEngine;

namespace LondonStreets
{
    /// <summary>
    /// One person walking around the city. The Pedestrians object creates these,
    /// so you don't place them yourself.
    /// </summary>
    public class Pedestrian : MonoBehaviour
    {
        /// <summary>True while crossing a road, so traffic knows to stop.</summary>
        public bool OnRoad { get; private set; }

        Pedestrians _manager;
        float _walkSpeed = 1.4f;
        float _height = 1.75f;
        float _sideOffset = 0.6f; // How far left of the pavement's middle line they keep.

        Vector3Int _from, _to;
        Vector3 _start, _end;
        float _progress;          // Metres walked along the current stretch.
        bool _waiting;
        float _waited;
        float _blockedTime;
        Vector3 _dodge;
        float _dodgeTime;
        float _shakenTime;        // Standing still for a moment after jumping clear.
        Transform _body;
        CharacterAnimator _animator;
        float _bobTime;

        static readonly Color[] Colours =
        {
            new Color(0.15f, 0.15f, 0.2f), new Color(0.5f, 0.1f, 0.1f), new Color(0.2f, 0.3f, 0.5f), new Color(0.6f, 0.55f, 0.45f),
            new Color(0.25f, 0.35f, 0.25f), new Color(0.7f, 0.7f, 0.7f), new Color(0.4f, 0.25f, 0.15f), new Color(0.8f, 0.6f, 0.2f),
        };
        static readonly Color[] Skin =
        {
            new Color(0.95f, 0.8f, 0.65f), new Color(0.8f, 0.6f, 0.45f), new Color(0.55f, 0.38f, 0.25f), new Color(0.35f, 0.24f, 0.16f),
        };
        static readonly Dictionary<Color, Material> SharedMaterials = new Dictionary<Color, Material>();

        public void Init(Pedestrians manager, float walkSpeed, float height, float sideOffset, GameObject model, bool flipModel)
        {
            _manager = manager;
            _walkSpeed = walkSpeed;
            _height = height;
            _sideOffset = sideOffset;
            BuildBody(model, flipModel);

            // You bump into people on foot, but cars pass through them (people
            // jump out of the way instead): see GameLayers.
            var body = gameObject.AddComponent<Rigidbody>();
            body.isKinematic = true;
            var capsule = gameObject.AddComponent<CapsuleCollider>();
            capsule.radius = 0.3f;
            capsule.height = height;
            capsule.center = new Vector3(0f, height / 2f, 0f);
            GameLayers.SetLayerRecursively(gameObject, GameLayers.Pedestrians);
        }

        void OnDestroy() => _animator?.Destroy();

        /// <summary>
        /// Starts walking from corner <paramref name="from"/> to <paramref name="to"/>.
        /// <paramref name="fraction"/> (0 to 1) says how far along the way to start.
        /// </summary>
        public void StartWalk(Vector3Int from, Vector3Int to, bool crossesRoad, float fraction = 0f)
        {
            _from = from;
            _to = to;
            OnRoad = false;
            _waiting = crossesRoad; // Look both ways before crossing.
            var a = _manager.CornerPosition(from);
            var b = _manager.CornerPosition(to);
            // Keep to the left of the pavement's middle, so people coming the other
            // way pass each other.
            var left = Traffic.LeftOf((b - a).normalized);
            _start = a + left * _sideOffset;
            _end = b + left * _sideOffset;
            _progress = Vector3.Distance(_start, _end) * fraction;
            transform.position = Vector3.Lerp(_start, _end, fraction);
        }

        void Update()
        {
            if (_manager == null)
                return;
            float dt = Time.deltaTime;
            float length = Vector3.Distance(_start, _end);
            var direction = (_end - _start) / Mathf.Max(length, 0.001f);
            var pos = transform.position;
            float speed = 0f;

            // A car coming at us? Jump out of the way!
            var danger = _manager.DangerousVehicleNear(pos);
            if (danger != null && _dodgeTime <= 0f)
            {
                var carDirection = danger.Velocity.normalized;
                var away = Vector3.Cross(Vector3.up, carDirection);
                away.y = 0f;
                if (Vector3.Dot(pos - danger.transform.position, away) < 0f)
                    away = -away;
                _dodge = away.normalized * 5f;
                _dodgeTime = 0.5f;
                _shakenTime = 1.5f;
            }
            if (_dodgeTime > 0f)
            {
                _dodgeTime -= dt;
                transform.position = pos + _dodge * dt;
                Face(_dodge, dt);
                Animate(true, dt);
                return;
            }
            if (_shakenTime > 0f)
            {
                _shakenTime -= dt;
                Animate(false, dt);
                return;
            }

            if (_waiting)
            {
                _waited += dt;
                if (!_manager.IsRoadBusy(_start, _end, _waited))
                {
                    _waiting = false;
                    _waited = 0f;
                    OnRoad = true;
                }
            }
            else
            {
                speed = _walkSpeed;
                // Don't walk into the player.
                var player = Pedestrians.PlayerOnFoot();
                if (player != null)
                {
                    var offset = player.transform.position - pos;
                    offset.y = 0f;
                    if (offset.magnitude < 1.5f && Vector3.Dot(offset, direction) > 0.2f)
                        speed = 0f;
                }
                // Don't walk through a car that's stopped on the crossing (but give up
                // after a few seconds, in case it's waiting for us).
                if (OnRoad && _manager.IsBlockedByCar(pos, direction))
                {
                    _blockedTime += dt;
                    if (_blockedTime < 4f)
                        speed = 0f;
                }
                else
                {
                    _blockedTime = 0f;
                }
            }

            _progress += speed * dt;
            var target = _start + direction * Mathf.Min(_progress, length);
            // Drift back to the path after dodging.
            pos = Vector3.Distance(pos, target) > 0.05f ? Vector3.MoveTowards(pos, target, (speed + 2f) * dt) : target;
            transform.position = pos;
            Face(direction, dt);
            Animate(speed > 0f, dt);

            if (_progress >= length)
            {
                var next = _manager.ChooseNext(_to, _from);
                StartWalk(_to, next.corner, next.crossesRoad);
            }
        }

        void Face(Vector3 direction, float dt)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f)
                return;
            var wanted = Quaternion.LookRotation(direction.normalized, Vector3.up);
            // Turn about a fifth of the way each 1/60 s.
            transform.rotation = Quaternion.Slerp(transform.rotation, wanted, 1f - Mathf.Pow(0.8f, dt * 60f));
        }

        // --- Looks and animation ----------------------------------------------------------------------

        void Animate(bool moving, float dt)
        {
            if (_animator != null)
            {
                _animator.Update(moving ? CharacterAnimator.State.Walk : CharacterAnimator.State.Idle, dt);
                return;
            }
            // No animations: a simple bob while walking.
            if (moving)
            {
                _bobTime += dt * _walkSpeed * 6f;
                _body.localPosition = new Vector3(0f, Mathf.Abs(Mathf.Sin(_bobTime)) * 0.06f, 0f);
            }
            else
            {
                _body.localPosition = Vector3.zero;
            }
        }

        void BuildBody(GameObject model, bool flipModel)
        {
            _body = new GameObject("Body").transform;
            _body.SetParent(transform, false);
            if (model != null)
            {
                var wrapper = ModelUtil.Spawn(model);
                ModelUtil.FitCharacter(wrapper, _body, _height, flipModel);
                var clips = AssetLibrary.ClipsFor(model);
                var walk = AssetLibrary.PickClip(clips, _manager.walkAnimation, "walk");
                var idle = AssetLibrary.PickClip(clips, _manager.idleAnimation, "idle", "stand", "breath");
                // Can't walk? Then use the bob instead.
                _animator = CharacterAnimator.Create(wrapper, idle, walk, null);
                return;
            }
            BuildSimplePerson();
        }

        /// <summary>A simple person made of a capsule body and a ball for a head.</summary>
        void BuildSimplePerson()
        {
            var clothes = Shared(Colours[Random.Range(0, Colours.Length)]);
            var skin = Shared(Skin[Random.Range(0, Skin.Length)]);
            ModelUtil.Primitive(PrimitiveType.Capsule, _body, new Vector3(0f, _height * 0.4f, 0f), new Vector3(0.5f, _height * 0.4f, 0.5f), clothes);
            ModelUtil.Primitive(PrimitiveType.Sphere, _body, new Vector3(0f, _height * 0.8f + 0.12f, 0f), Vector3.one * 0.26f, skin);
        }

        static Material Shared(Color color)
        {
            if (!SharedMaterials.TryGetValue(color, out var material) || material == null)
            {
                material = LondonMaterials.Create(color);
                SharedMaterials[color] = material;
            }
            return material;
        }
    }
}

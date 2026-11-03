using UnityEngine;

namespace LondonStreets
{
    /// <summary>
    /// The player character: walks, runs and jumps, with a third-person camera
    /// that you steer with the mouse. Walk up to a car and press E to drive it.
    ///
    /// Every setting below shows up in the Inspector when you select the
    /// Player, so you can tweak it without touching code.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        public static PlayerController Instance { get; private set; }

        [Header("Character Model")]
        [Tooltip("Your character: drag a model (.fbx, .obj, .glb) or prefab here. If empty, the first model in Character Folder is used. If that's empty too, the first character found anywhere in the project (see found_assets.txt), and failing that, the blue capsule.")]
        public GameObject characterModel;
        [Tooltip("Folder the game looks in for a character when Character Model is empty.")]
        [FolderPath] public string characterFolder = AssetLibrary.CharactersFolder;
        [Tooltip("Optional texture to paint onto the character (for models that show up white).")]
        public Texture2D characterTexture;
        [Tooltip("Resize the character automatically so it's 1.8 m tall and standing on the ground. Untick to use Model Scale only.")]
        public bool fitToHeight = true;
        [Tooltip("Make the model bigger or smaller (applied after Fit To Height).")]
        [Min(0.0001f)] public float modelScale = 1f;
        [Tooltip("Tick this if your character walks backwards.")]
        public bool flipModelDirection;
        [Tooltip("Animations to play. Leave empty and the game picks the model's animations with 'idle', 'walk' and 'run' in their names.")]
        public AnimationClip idleAnimation;
        public AnimationClip walkAnimation;
        public AnimationClip runAnimation;

        [Header("Movement")]
        [Tooltip("Metres per second when walking.")]
        public float walkSpeed = 4f;
        [Tooltip("Metres per second while holding Shift.")]
        public float runSpeed = 8f;
        [Tooltip("How hard the player jumps.")]
        public float jumpVelocity = 4.5f;
        [Tooltip("How quickly the body turns to face where it walks.")]
        public float turnSpeed = 10f;
        [Tooltip("Degrees the camera turns per pixel the mouse moves.")]
        public float mouseSensitivity = 0.17f;
        [Tooltip("How close you need to be to a car to get in (metres).")]
        public float enterVehicleDistance = 4f;

        [Header("Camera")]
        [Tooltip("Lowest the camera may tilt, in degrees (negative = looking down).")]
        public float cameraMinPitch = -70f;
        [Tooltip("Highest the camera may tilt, in degrees.")]
        public float cameraMaxPitch = 20f;
        public float cameraDistance = 4.5f;

        public Camera Camera { get; private set; }
        public Transform Body { get; private set; }
        /// <summary>The car the player is driving, or null when on foot.</summary>
        public Car Vehicle { get; private set; }
        public bool IsDriving => Vehicle != null;

        /// <summary>Where the player is: on foot, or the car they're driving.</summary>
        public Vector3 FocusPosition => Vehicle != null ? Vehicle.transform.position : transform.position;

        CharacterController _controller;
        Transform _pivot;
        GameObject _placeholder;
        CharacterAnimator _animator;
        Vector3 _velocity;
        Vector3 _spawnPosition;
        float _yaw;
        float _pitch = 17f; // Unity pitch: positive looks down.
        Car _nearbyVehicle;
        int _ignoreInteractFrame = -1;

        void Awake()
        {
            Instance = this;
            GameLayers.Setup();
            _controller = GetComponent<CharacterController>();
            _controller.height = 1.8f;
            _controller.radius = 0.35f;
            _controller.center = new Vector3(0f, 0.9f, 0f);
            _controller.stepOffset = 0.3f;
            _controller.skinWidth = 0.04f;

            _yaw = transform.eulerAngles.y;
            transform.rotation = Quaternion.identity;

            Body = new GameObject("Body").transform;
            Body.SetParent(transform, false);
            Body.localRotation = Quaternion.Euler(0f, _yaw, 0f);
            BuildModel();

            _pivot = new GameObject("CameraPivot").transform;
            _pivot.SetParent(transform, false);
            _pivot.localPosition = new Vector3(0f, 1.6f, 0f);
            var cameraObject = new GameObject("Player Camera");
            cameraObject.tag = "MainCamera";
            Camera = cameraObject.AddComponent<Camera>();
            Camera.nearClipPlane = 0.1f;
            Camera.farClipPlane = 1500f;
            cameraObject.AddComponent<AudioListener>();
            cameraObject.transform.SetParent(_pivot, false);
        }

        void Start()
        {
            _spawnPosition = transform.position;
            GameCursor.Capture();
        }

        void OnDestroy()
        {
            _animator?.Destroy();
            if (Instance == this)
                Instance = null;
        }

        void Update()
        {
            GameCursor.Tick();

            if (GameInput.InteractPressed && _nearbyVehicle != null && Time.frameCount != _ignoreInteractFrame)
            {
                EnterVehicle(_nearbyVehicle);
                return;
            }

            // Moving the mouse swings the camera around the player.
            if (GameCursor.Captured)
            {
                var mouse = GameInput.MouseDelta;
                _yaw += mouse.x * mouseSensitivity;
                _pitch = Mathf.Clamp(_pitch - mouse.y * mouseSensitivity, -cameraMaxPitch, -cameraMinPitch);
            }

            Move(Time.deltaTime);
            UpdateAnimation();
            LookForVehicle();

            // Fell off the edge of the world? Put the player back at the start.
            if (transform.position.y < -20f)
            {
                Teleport(_spawnPosition);
                _velocity = Vector3.zero;
            }
        }

        void LateUpdate()
        {
            _pivot.rotation = Quaternion.Euler(_pitch, _yaw, 0f);
            CameraArm.Place(Camera.transform, _pivot.position, _pivot.rotation, cameraDistance, 0.2f, transform);
        }

        void Move(float dt)
        {
            bool grounded = _controller.isGrounded;
            // Gravity pulls the player down whenever they are in the air.
            if (grounded && _velocity.y < 0f)
                _velocity.y = -2f; // Keeps the player pressed onto slopes and kerbs.
            else
                _velocity.y += Physics.gravity.y * dt;

            if (GameInput.JumpPressed && grounded)
                _velocity.y = jumpVelocity;

            // WASD relative to the camera, so "forward" always means "away from the camera".
            var input = GameInput.Move;
            var direction = Quaternion.Euler(0f, _yaw, 0f) * new Vector3(input.x, 0f, input.y);
            float speed = GameInput.Sprint ? runSpeed : walkSpeed;

            if (direction.sqrMagnitude > 0.0001f)
            {
                direction.Normalize();
                _velocity.x = direction.x * speed;
                _velocity.z = direction.z * speed;
                // Smoothly turn the body to face the way we're walking.
                float target = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
                float current = Body.localEulerAngles.y;
                Body.localRotation = Quaternion.Euler(0f, Mathf.LerpAngle(current, target, Mathf.Clamp01(turnSpeed * dt)), 0f);
            }
            else
            {
                _velocity.x = 0f;
                _velocity.z = 0f;
            }

            _controller.Move(_velocity * dt);
        }

        /// <summary>Finds the nearest car within reach, for the "Press E" hint.</summary>
        void LookForVehicle()
        {
            _nearbyVehicle = null;
            float best = enterVehicleDistance;
            foreach (var vehicle in Car.All)
            {
                float distance = Vector3.Distance(transform.position, vehicle.transform.position);
                if (distance < best)
                {
                    best = distance;
                    _nearbyVehicle = vehicle;
                }
            }
        }

        void EnterVehicle(Car vehicle)
        {
            // Hide the player and switch them off while they're driving.
            Vehicle = vehicle;
            _velocity = Vector3.zero;
            _nearbyVehicle = null;
            gameObject.SetActive(false);
            vehicle.Enter(this);
        }

        /// <summary>Called by the car when the player gets out.</summary>
        public void LeaveVehicle(Vector3 exitPosition)
        {
            Vehicle = null;
            Teleport(exitPosition);
            _ignoreInteractFrame = Time.frameCount;
            gameObject.SetActive(true);
        }

        void Teleport(Vector3 position)
        {
            // A CharacterController ignores position changes unless it's switched off first.
            _controller.enabled = false;
            transform.position = position;
            _controller.enabled = true;
        }

        void OnGUI()
        {
            if (_nearbyVehicle != null && !IsDriving)
                Hud.Label(new Rect(0, Screen.height - Screen.height * 0.12f, Screen.width, 50), "Press E to get in", 26, TextAnchor.MiddleCenter);
        }

        // --- Looks ---------------------------------------------------------------------------

        /// <summary>Uses your own character model if there is one, otherwise a blue capsule.</summary>
        void BuildModel()
        {
            var model = characterModel;
            if (model == null)
            {
                var found = AssetLibrary.ModelsInFolder(characterFolder);
                model = found.Count > 0 ? found[0] : AssetLibrary.PlayerCharacter;
                if (model != null)
                    Debug.Log("Using character: " + model.name);
            }
            if (model == null)
            {
                BuildPlaceholder();
                return;
            }

            var wrapper = ModelUtil.Spawn(model, characterTexture);
            if (fitToHeight)
            {
                ModelUtil.FitCharacter(wrapper, Body, 1.8f, flipModelDirection);
                wrapper.transform.localScale *= modelScale;
            }
            else
            {
                ModelUtil.Place(wrapper, Body, Vector3.zero, Quaternion.Euler(0f, flipModelDirection ? 180f : 0f, 0f), modelScale);
            }

            var clips = AssetLibrary.ClipsFor(model);
            var idle = AssetLibrary.PickClip(clips, idleAnimation, "idle", "stand", "breath");
            var walk = AssetLibrary.PickClip(clips, walkAnimation, "walk");
            var run = AssetLibrary.PickClip(clips, runAnimation, "run", "sprint", "jog");
            if (clips.Count > 0)
                Debug.Log("Character animations found: " + string.Join(", ", clips.ConvertAll(c => c.name)));
            Debug.Log($"Using: idle = {Name(idle)}, walk = {Name(walk)}, run = {Name(run)}");
            _animator = CharacterAnimator.Create(wrapper, idle, walk, run);
        }

        static string Name(AnimationClip clip) => clip != null ? clip.name : "(none)";

        void BuildPlaceholder()
        {
            _placeholder = new GameObject("Capsule");
            _placeholder.transform.SetParent(Body, false);
            var blue = LondonMaterials.Create(new Color(0.15f, 0.35f, 0.8f));
            var skin = LondonMaterials.Create(new Color(0.95f, 0.8f, 0.65f));
            ModelUtil.Primitive(PrimitiveType.Capsule, _placeholder.transform, new Vector3(0f, 0.9f, 0f), new Vector3(0.7f, 0.9f, 0.7f), blue);
            // A "face" so you can see which way the capsule is looking.
            ModelUtil.Primitive(PrimitiveType.Cube, _placeholder.transform, new Vector3(0f, 1.5f, 0.3f), new Vector3(0.4f, 0.15f, 0.2f), skin);
        }

        /// <summary>Plays idle, walk or run depending on how fast the player is moving.</summary>
        void UpdateAnimation()
        {
            if (_animator == null)
                return;
            float speed = new Vector2(_velocity.x, _velocity.z).magnitude;
            var state = speed > walkSpeed + 0.5f ? CharacterAnimator.State.Run
                : speed > 0.1f ? CharacterAnimator.State.Walk
                : CharacterAnimator.State.Idle;
            _animator.Update(state, Time.deltaTime);
        }

        void OnDrawGizmos()
        {
            if (Application.isPlaying)
                return;
            Gizmos.color = new Color(0.15f, 0.35f, 0.8f);
            Gizmos.DrawWireCube(transform.position + Vector3.up * 0.9f, new Vector3(0.7f, 1.8f, 0.7f));
            Gizmos.DrawLine(transform.position + Vector3.up * 1.5f, transform.position + Vector3.up * 1.5f + transform.forward);
        }
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace LondonStreets
{
    /// <summary>
    /// A car you can get into and drive.
    ///
    /// Walk up to it and press E (or F) to get in, E again to get out.
    /// W / S to drive and reverse, A / D to steer, Space for the handbrake.
    /// The car drives towards its blue (Z) arrow.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class Car : MonoBehaviour
    {
        /// <summary>Every drivable car in the game.</summary>
        public static readonly List<Car> All = new List<Car>();

        /// <summary>The car the player is driving, or null.</summary>
        public static Car Driven => PlayerController.Instance != null ? PlayerController.Instance.Vehicle : null;

        [Header("Car Model")]
        [Tooltip("Your car: drag a model or prefab here. If empty, a car found in the project is used (see found_assets.txt); if there are none, you get the simple box car.")]
        public GameObject carModel;
        [Tooltip("Optional texture to paint onto the car (for models that show up white).")]
        public Texture2D carTexture;
        [Tooltip("Resize the model automatically to the length of a real car (4.3 m) and turn it to face along the car. Untick to use Model Scale instead.")]
        public bool fitToCarSize = true;
        [Tooltip("Make the model bigger or smaller (only when Fit To Car Size is off).")]
        [Min(0.0001f)] public float modelScale = 1f;
        [Tooltip("Tick this if the car model faces the wrong way.")]
        public bool flipModelDirection;
        [Tooltip("Move the model up or down if it floats above or sinks into the road.")]
        public float modelHeightOffset;
        [Tooltip("Show the simple black wheels as well as your model (untick if your model already has wheels).")]
        public bool showBoxWheels;

        [Header("Driving")]
        [Tooltip("How hard each back wheel pushes the car (newton-metres).")]
        public float maxMotorTorque = 950f;
        [Tooltip("How hard the brakes are (newton-metres per wheel).")]
        public float maxBrakeTorque = 3000f;
        [Tooltip("How far the front wheels turn.")]
        public float maxSteerDegrees = 30f;
        public float topSpeedKmh = 120f;
        public float reverseSpeedKmh = 30f;
        [Tooltip("How high above the car the camera looks.")]
        public float cameraHeight = 1.5f;
        public float cameraDistance = 7f;

        /// <summary>The player while they're in the car.</summary>
        public PlayerController Driver { get; private set; }
        /// <summary>The chase camera, switched on while someone's driving.</summary>
        public Camera Camera => _camera;
        public Vector3 Velocity => _body != null ? _body.GetVelocity() : Vector3.zero;

        Rigidbody _body;
        readonly WheelCollider[] _wheels = new WheelCollider[4];
        readonly Transform[] _wheelMeshes = new Transform[4];
        Transform _cameraPivot;
        Camera _camera;
        AudioListener _listener;
        float _steer;
        float _cameraYaw;
        int _enteredFrame = -1;

        static readonly Vector3[] WheelPositions =
        {
            new Vector3(-0.85f, 0.45f, 1.35f), new Vector3(0.85f, 0.45f, 1.35f),   // front left, front right
            new Vector3(-0.85f, 0.45f, -1.35f), new Vector3(0.85f, 0.45f, -1.35f), // rear left, rear right
        };

        void Awake()
        {
            GameLayers.Setup();
            _body = GetComponent<Rigidbody>();
            _body.mass = 1200f;
            _body.centerOfMass = new Vector3(0f, 0.35f, 0f);
            _body.interpolation = RigidbodyInterpolation.Interpolate;
            _body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            var hull = new GameObject("Hull").AddComponent<BoxCollider>();
            hull.transform.SetParent(transform, false);
            hull.center = new Vector3(0f, 0.9f, 0f);
            hull.size = new Vector3(1.8f, 1.1f, 4.2f);

            bool hasModel = BuildModel();
            var tyre = LondonMaterials.Create(new Color(0.05f, 0.05f, 0.05f));
            for (int i = 0; i < 4; i++)
            {
                var wheelObject = new GameObject(i < 2 ? "Front Wheel" : "Rear Wheel");
                wheelObject.transform.SetParent(transform, false);
                wheelObject.transform.localPosition = WheelPositions[i];
                var wheel = wheelObject.AddComponent<WheelCollider>();
                wheel.mass = 20f;
                wheel.radius = 0.38f;
                wheel.suspensionDistance = 0.2f;
                wheel.forceAppPointDistance = 0.2f;
                wheel.suspensionSpring = new JointSpring { spring = 35000f, damper = 4500f, targetPosition = 0.5f };
                var sideways = wheel.sidewaysFriction;
                sideways.stiffness = i < 2 ? 1.3f : 1.15f;
                wheel.sidewaysFriction = sideways;
                _wheels[i] = wheel;

                var mesh = ModelUtil.Primitive(PrimitiveType.Cylinder, transform, WheelPositions[i], new Vector3(0.76f, 0.125f, 0.76f), tyre);
                mesh.name = "Wheel Mesh";
                mesh.SetActive(!hasModel || showBoxWheels);
                _wheelMeshes[i] = mesh.transform;
            }
            _wheels[0].ConfigureVehicleSubsteps(5f, 12, 15);
            GameLayers.SetLayerRecursively(gameObject, GameLayers.Vehicles);

            // The camera follows the car smoothly instead of being bolted to it.
            _cameraPivot = new GameObject(name + " Camera Pivot").transform;
            var cameraObject = new GameObject(name + " Camera");
            cameraObject.transform.SetParent(_cameraPivot, false);
            _camera = cameraObject.AddComponent<Camera>();
            _camera.nearClipPlane = 0.1f;
            _camera.farClipPlane = 1500f;
            _camera.enabled = false;
            _listener = cameraObject.AddComponent<AudioListener>();
            _listener.enabled = false;
        }

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        void OnDestroy()
        {
            if (_cameraPivot != null)
                Destroy(_cameraPivot.gameObject);
        }

        /// <summary>Called by the player when they press E next to this car.</summary>
        public void Enter(PlayerController player)
        {
            Driver = player;
            _enteredFrame = Time.frameCount;
            _cameraPivot.position = transform.position + Vector3.up * cameraHeight;
            _cameraYaw = transform.eulerAngles.y;
            _camera.enabled = true;
            _listener.enabled = true;
        }

        void Exit()
        {
            var player = Driver;
            Driver = null;
            _camera.enabled = false;
            _listener.enabled = false;
            // Put the player down beside the driver's door (on the right, as in a British car).
            player.LeaveVehicle(transform.position + transform.right * 2f + Vector3.up * 0.5f);
        }

        void Update()
        {
            if (Driver != null)
            {
                GameCursor.Tick();
                if (GameInput.InteractPressed && Time.frameCount != _enteredFrame)
                    Exit();
            }

            // Make the wheel meshes follow the wheels.
            for (int i = 0; i < 4; i++)
            {
                _wheels[i].GetWorldPose(out var position, out var rotation);
                _wheelMeshes[i].SetPositionAndRotation(position, rotation * Quaternion.Euler(0f, 0f, 90f));
            }
        }

        void FixedUpdate()
        {
            // The car drives along its +Z axis; forwardSpeed is negative when reversing.
            float forwardSpeed = Vector3.Dot(Velocity, transform.forward);
            float speedKmh = Mathf.Abs(forwardSpeed) * 3.6f;

            float motor = 0f;
            float brake = maxBrakeTorque;
            if (Driver != null)
            {
                float throttle = (GameInput.Forward ? 1f : 0f) - (GameInput.Back ? 1f : 0f);
                float steerInput = (GameInput.Right ? 1f : 0f) - (GameInput.Left ? 1f : 0f);

                brake = 0f;
                if (throttle > 0f)
                {
                    if (forwardSpeed < -0.5f)
                        brake = maxBrakeTorque * throttle;      // rolling backwards: brake first
                    else if (speedKmh < topSpeedKmh)
                        motor = maxMotorTorque * throttle;
                }
                else if (throttle < 0f)
                {
                    if (forwardSpeed > 0.5f)
                        brake = maxBrakeTorque * -throttle;     // moving forwards: brake first
                    else if (speedKmh < reverseSpeedKmh)
                        motor = maxMotorTorque * throttle;
                }
                else
                {
                    brake = maxBrakeTorque * 0.05f;             // gentle roll to a stop
                }
                if (GameInput.JumpHeld)
                    brake = maxBrakeTorque;                     // handbrake

                // Steer less at high speed so the car doesn't flip.
                float maxSteer = maxSteerDegrees * Mathf.Lerp(1f, 0.2f, Mathf.Clamp01(speedKmh / 70f));
                _steer = Mathf.MoveTowards(_steer, steerInput * maxSteer, 86f * Time.fixedDeltaTime);
            }
            else
            {
                _steer = Mathf.MoveTowards(_steer, 0f, 86f * Time.fixedDeltaTime);
            }

            for (int i = 0; i < 4; i++)
            {
                bool front = i < 2;
                _wheels[i].steerAngle = front ? _steer : 0f;
                _wheels[i].motorTorque = front ? 0f : motor;
                _wheels[i].brakeTorque = brake;
            }
        }

        void LateUpdate()
        {
            if (Driver == null)
                return;
            // Smooth chase camera.
            float dt = Time.deltaTime;
            _cameraPivot.position = Vector3.Lerp(_cameraPivot.position, transform.position + Vector3.up * cameraHeight, Mathf.Clamp01(10f * dt));
            _cameraYaw = Mathf.LerpAngle(_cameraYaw, transform.eulerAngles.y, Mathf.Clamp01(4f * dt));
            _cameraPivot.rotation = Quaternion.Euler(14f, _cameraYaw, 0f);
            CameraArm.Place(_camera.transform, _cameraPivot.position, _cameraPivot.rotation, cameraDistance, 0.3f, transform);
        }

        void OnGUI()
        {
            if (Driver == null)
                return;
            float mph = Mathf.Abs(Vector3.Dot(Velocity, transform.forward)) * 3.6f / 1.609f;
            float w = Screen.width, h = Screen.height;
            Hud.Label(new Rect(w - 324, h - h * 0.17f, 300, h * 0.15f), $"{Mathf.RoundToInt(mph)} mph\nE to get out", 30, TextAnchor.LowerRight);
        }

        /// <summary>Uses your own car model if there is one, otherwise a simple box car. Returns true for a model.</summary>
        bool BuildModel()
        {
            var model = carModel;
            if (model == null)
            {
                var found = AssetLibrary.Cars;
                if (found.Count > 0)
                    model = found[transform.GetSiblingIndex() % found.Count]; // Each parked car picks a different one.
            }
            if (model == null)
            {
                var paint = LondonMaterials.Create(new Color(0.1f, 0.25f, 0.55f), 0.35f, 0.4f);
                var glass = LondonMaterials.Create(new Color(0.15f, 0.2f, 0.25f), 0.1f);
                var box = new GameObject("Box Body").transform;
                box.SetParent(transform, false);
                ModelUtil.Primitive(PrimitiveType.Cube, box, new Vector3(0f, 0.75f, 0f), new Vector3(1.8f, 0.6f, 4.2f), paint);
                ModelUtil.Primitive(PrimitiveType.Cube, box, new Vector3(0f, 1.32f, -0.25f), new Vector3(1.6f, 0.55f, 2.2f), glass);
                return false;
            }

            var wrapper = ModelUtil.Spawn(model, carTexture);
            if (fitToCarSize)
                ModelUtil.FitVehicle(wrapper, transform, 4.3f, flipModelDirection);
            else
                ModelUtil.Place(wrapper, transform, Vector3.zero, Quaternion.Euler(0f, flipModelDirection ? 180f : 0f, 0f), modelScale);
            wrapper.transform.localPosition += Vector3.up * modelHeightOffset;
            return true;
        }

        void OnDrawGizmos()
        {
            if (Application.isPlaying)
                return;
            Gizmos.color = new Color(0.3f, 0.6f, 1f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(new Vector3(0f, 0.9f, 0f), new Vector3(1.8f, 1.1f, 4.2f));
            Gizmos.DrawLine(new Vector3(0f, 0.9f, 2.1f), new Vector3(0f, 0.9f, 3.5f));
        }
    }
}

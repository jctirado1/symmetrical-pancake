using UnityEngine;
using UnityEngine.Rendering;

namespace LondonStreets
{
    /// <summary>
    /// A minimap in the corner of the screen. It shows the roads, blocks and
    /// parks of the city, the traffic (white dots, red buses), cars you can drive
    /// (blue dots) and you (the yellow arrow). It turns with your view so "up" is
    /// always the way you're looking, and zooms out while you drive.
    /// </summary>
    public class Minimap : MonoBehaviour
    {
        [Tooltip("The City to draw.")]
        public CityGrid city;
        [Tooltip("How many metres across the map shows when you're on foot.")]
        public float metresOnFoot = 180f;
        [Tooltip("How many metres across the map shows when you're driving.")]
        public float metresDriving = 320f;
        [Tooltip("Untick to keep north at the top instead of turning with the camera.")]
        public bool rotateWithCamera = true;
        [Tooltip("Size of the map on a 720-pixel-high screen, and its distance from the bottom-left corner.")]
        public float size = 220f;
        public float margin = 20f;

        static readonly Color Grass = new Color(0.27f, 0.36f, 0.24f);
        static readonly Color Road = new Color(0.42f, 0.42f, 0.45f);
        static readonly Color Pavement = new Color(0.62f, 0.62f, 0.6f);
        static readonly Color[] ZoneColours =
        {
            new Color(0.32f, 0.3f, 0.33f),  // city
            new Color(0.38f, 0.32f, 0.28f), // suburban
            new Color(0.3f, 0.33f, 0.36f),  // industrial
            new Color(0.3f, 0.5f, 0.26f),   // park
        };

        RenderTexture _texture;
        Material _material;
        float _zoom = 180f;
        int _pixels;
        // Current view: what's in the middle, how it's turned and how zoomed.
        Vector3 _focus;
        float _sin, _cos, _pixelsPerMetre;
        Vector2 _northLabel;

        void Start()
        {
            if (city == null)
                city = CityGrid.Main;
            _material = new Material(Shader.Find("Hidden/Internal-Colored")) { hideFlags = HideFlags.DontSave };
            _material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            _material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            _material.SetInt("_Cull", (int)CullMode.Off);
            _material.SetInt("_ZWrite", 0);
            _material.SetInt("_ZTest", (int)CompareFunction.Always);
        }

        void OnDestroy()
        {
            if (_texture != null)
                _texture.Release();
            Destroy(_texture);
            Destroy(_material);
        }

        void LateUpdate()
        {
            var player = PlayerController.Instance;
            var driven = Car.Driven;
            float wanted = driven != null ? metresDriving : metresOnFoot;
            _zoom = Mathf.Lerp(_zoom, wanted, Mathf.Clamp01(2f * Time.deltaTime));

            int pixels = Mathf.Max(32, Mathf.RoundToInt(size * Screen.height / 720f));
            if (_texture == null || _pixels != pixels)
            {
                if (_texture != null)
                {
                    _texture.Release();
                    Destroy(_texture);
                }
                _pixels = pixels;
                _texture = new RenderTexture(pixels, pixels, 0, RenderTextureFormat.ARGB32) { name = "Minimap", antiAliasing = 4 };
                _texture.Create();
            }

            // Work out how world positions (x, z) map onto the minimap.
            _focus = driven != null ? driven.transform.position : player != null ? player.transform.position : Vector3.zero;
            float angle = 0f; // Which way is up on the map, in degrees clockwise from north.
            var camera = driven != null ? driven.Camera : player != null ? player.Camera : null;
            if (rotateWithCamera && camera != null)
            {
                var look = camera.transform.forward;
                angle = Mathf.Atan2(look.x, look.z) * Mathf.Rad2Deg;
            }
            _sin = Mathf.Sin(angle * Mathf.Deg2Rad);
            _cos = Mathf.Cos(angle * Mathf.Deg2Rad);
            _pixelsPerMetre = pixels / _zoom;

            Draw(player, driven);
        }

        /// <summary>A world position as a pixel on the map (0, 0 = bottom-left).</summary>
        Vector2 ToMap(float x, float z)
        {
            float rx = x - _focus.x, rz = z - _focus.z;
            float mx = rx * _cos - rz * _sin;
            float my = rx * _sin + rz * _cos;
            return new Vector2(_pixels / 2f + mx * _pixelsPerMetre, _pixels / 2f + my * _pixelsPerMetre);
        }

        Vector2 ToMap(Vector3 world) => ToMap(world.x, world.z);

        /// <summary>A direction in the world as a direction on the map.</summary>
        Vector2 DirectionOnMap(Vector3 direction) =>
            new Vector2(direction.x * _cos - direction.z * _sin, direction.x * _sin + direction.z * _cos).normalized;

        void Draw(PlayerController player, Car driven)
        {
            var previous = RenderTexture.active;
            RenderTexture.active = _texture;
            GL.PushMatrix();
            GL.LoadPixelMatrix(0, _pixels, 0, _pixels);
            GL.Clear(true, true, Grass);
            _material.SetPass(0);

            if (city != null)
            {
                // The city.
                GL.Begin(GL.QUADS);
                var half = city.HalfExtents;
                Rect(-half.x, -half.y, half.x, half.y, Road);
                float inset = city.pavementWidth;
                for (int bx = 0; bx < city.blocksX; bx++)
                {
                    for (int bz = 0; bz < city.blocksZ; bz++)
                    {
                        var block = city.BlockRect(bx, bz);
                        Rect(block.xMin, block.yMin, block.xMax, block.yMax, Pavement);
                        Rect(block.xMin + inset, block.yMin + inset, block.xMax - inset, block.yMax - inset, ZoneColours[(int)city.BlockZone(bx, bz)]);
                    }
                }
                GL.End();
            }

            // Markers, drawn at a fixed size in pixels.
            float dot = _pixels / 220f;
            GL.Begin(GL.TRIANGLES);
            var traffic = Traffic.Instance;
            if (traffic != null)
            {
                foreach (var car in traffic.cars)
                {
                    if (car.Placed)
                        Circle(ToMap(car.Position), (car.IsBus ? 4f : 2.5f) * dot, car.IsBus ? new Color(0.85f, 0.15f, 0.15f) : new Color(0.95f, 0.95f, 0.95f));
                }
            }
            foreach (var vehicle in Car.All)
            {
                if (vehicle != driven)
                    Circle(ToMap(vehicle.transform.position), 3.5f * dot, new Color(0.3f, 0.6f, 1f));
            }

            // You: an arrow pointing the way you're facing.
            var facing = driven != null ? driven.transform.forward : player != null ? player.Body.forward : Vector3.forward;
            var centre = ToMap(_focus);
            var direction = DirectionOnMap(facing);
            var side = new Vector2(-direction.y, direction.x);
            var tip = centre + direction * 9f * dot;
            var leftBack = centre - direction * 6f * dot + side * 6f * dot;
            var notch = centre - direction * 3f * dot;
            var rightBack = centre - direction * 6f * dot - side * 6f * dot;
            // Black outline first, then the yellow arrow on top.
            Triangle(tip + direction * 1.5f * dot, leftBack + (leftBack - centre).normalized * 1.5f * dot, notch, Color.black);
            Triangle(tip + direction * 1.5f * dot, notch, rightBack + (rightBack - centre).normalized * 1.5f * dot, Color.black);
            var yellow = new Color(1f, 0.85f, 0.1f);
            Triangle(tip, leftBack, notch, yellow);
            Triangle(tip, notch, rightBack, yellow);

            // A circle on the edge of the map, pointing north (+Z), for the "N".
            var north = DirectionOnMap(Vector3.forward);
            _northLabel = new Vector2(_pixels / 2f, _pixels / 2f) + north * (_pixels / 2f - 12f * dot);
            Circle(_northLabel, 9f * dot, new Color(0f, 0f, 0f, 0.6f));
            GL.End();

            GL.PopMatrix();
            RenderTexture.active = previous;
        }

        void Rect(float x0, float z0, float x1, float z1, Color colour)
        {
            GL.Color(colour);
            GL.Vertex(ToMap(x0, z0));
            GL.Vertex(ToMap(x1, z0));
            GL.Vertex(ToMap(x1, z1));
            GL.Vertex(ToMap(x0, z1));
        }

        static void Triangle(Vector2 a, Vector2 b, Vector2 c, Color colour)
        {
            GL.Color(colour);
            GL.Vertex(a);
            GL.Vertex(b);
            GL.Vertex(c);
        }

        static void Circle(Vector2 centre, float radius, Color colour)
        {
            const int segments = 12;
            GL.Color(colour);
            for (int i = 0; i < segments; i++)
            {
                float a0 = i * Mathf.PI * 2f / segments;
                float a1 = (i + 1) * Mathf.PI * 2f / segments;
                GL.Vertex(centre);
                GL.Vertex(centre + new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)) * radius);
                GL.Vertex(centre + new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * radius);
            }
        }

        static GUIStyle _northStyle;

        void OnGUI()
        {
            if (_texture == null || Event.current.type != EventType.Repaint)
                return;
            float s = _pixels;
            float m = margin * Screen.height / 720f;
            var rect = new Rect(m, Screen.height - m - s, s, s);
            GUI.DrawTexture(rect, _texture, ScaleMode.StretchToFill, false);

            // Black border.
            var old = GUI.color;
            GUI.color = Color.black;
            const float border = 3f;
            GUI.DrawTexture(new Rect(rect.x - border, rect.y - border, s + border * 2, border), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.x - border, rect.yMax, s + border * 2, border), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.x - border, rect.y, border, s), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.xMax, rect.y, border, s), Texture2D.whiteTexture);
            GUI.color = old;

            if (_northStyle == null)
                _northStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            _northStyle.fontSize = Mathf.RoundToInt(14 * Screen.height / 720f);
            _northStyle.normal.textColor = Color.white;
            // The map's y goes up, the screen's goes down.
            var label = new Vector2(rect.x + _northLabel.x, rect.yMax - _northLabel.y);
            GUI.Label(new Rect(label.x - 15, label.y - 15, 30, 30), "N", _northStyle);
        }
    }
}

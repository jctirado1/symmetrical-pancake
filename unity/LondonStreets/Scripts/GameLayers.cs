using UnityEngine;

namespace LondonStreets
{
    /// <summary>
    /// Physics layers. Pedestrians are on their own layer so that you bump into
    /// them on foot but cars pass through them (people jump out of the way
    /// instead). The layers don't need names; change the numbers here if your
    /// project already uses 29 or 30 for something else.
    /// </summary>
    public static class GameLayers
    {
        public const int Vehicles = 29;
        public const int Pedestrians = 30;
        const int IgnoreRaycast = 2;

        static bool _done;

        public static void Setup()
        {
            if (_done)
                return;
            _done = true;
            Physics.IgnoreLayerCollision(Vehicles, Pedestrians, true);
        }

        /// <summary>What the camera arms bump into: everything except people.</summary>
        public static int CameraMask => ~((1 << Pedestrians) | (1 << IgnoreRaycast));

        public static void SetLayerRecursively(GameObject root, int layer)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                t.gameObject.layer = layer;
        }
    }

    public static class RigidbodyExtensions
    {
        public static Vector3 GetVelocity(this Rigidbody body)
        {
#if UNITY_6000_0_OR_NEWER
            return body.linearVelocity;
#else
            return body.velocity;
#endif
        }
    }
}

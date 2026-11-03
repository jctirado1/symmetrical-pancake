using UnityEngine;

namespace LondonStreets
{
    /// <summary>
    /// Puts a camera behind a point, like a selfie stick that shortens when
    /// something gets in the way, so walls never block your view.
    /// </summary>
    public static class CameraArm
    {
        static readonly RaycastHit[] Hits = new RaycastHit[32];

        public static void Place(Transform camera, Vector3 pivot, Quaternion rotation, float length, float margin, Transform ignore)
        {
            var back = rotation * Vector3.back;
            float distance = length;
            int count = Physics.SphereCastNonAlloc(pivot, margin, back, Hits, length, GameLayers.CameraMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var hit = Hits[i];
                if (ignore != null && hit.collider.transform.IsChildOf(ignore))
                    continue;
                // A hit at distance 0 means the sphere started inside something.
                if (hit.distance <= 0f)
                    continue;
                distance = Mathf.Min(distance, hit.distance);
            }
            camera.SetPositionAndRotation(pivot + back * distance, rotation);
        }
    }
}

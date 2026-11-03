using UnityEngine;
using UnityEngine.Rendering;

namespace LondonStreets
{
    /// <summary>
    /// The weather: a soft grey London sky, light haze in the distance and gentle
    /// light from all around. Put this on the Sun and change the settings in the
    /// Inspector.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(Light))]
    public class LondonSky : MonoBehaviour
    {
        [Tooltip("The sky (a material using the Skybox/Procedural shader).")]
        public Material skyMaterial;
        [Header("Haze")]
        public bool fog = true;
        public Color fogColour = new Color(0.7f, 0.73f, 0.78f);
        [Range(0f, 0.02f)] public float fogDensity = 0.003f;
        [Header("Light from the sky, the horizon and the ground")]
        public Color skyLight = new Color(0.56f, 0.62f, 0.72f);
        public Color horizonLight = new Color(0.47f, 0.49f, 0.52f);
        public Color groundLight = new Color(0.26f, 0.28f, 0.26f);

        void OnEnable() => Apply();
        void OnValidate() => Apply();

        void Apply()
        {
            RenderSettings.sun = GetComponent<Light>();
            if (skyMaterial != null)
                RenderSettings.skybox = skyMaterial;
            RenderSettings.fog = fog;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = fogColour;
            RenderSettings.fogDensity = fogDensity;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = skyLight;
            RenderSettings.ambientEquatorColor = horizonLight;
            RenderSettings.ambientGroundColor = groundLight;
            DynamicGI.UpdateEnvironment();
        }
    }
}

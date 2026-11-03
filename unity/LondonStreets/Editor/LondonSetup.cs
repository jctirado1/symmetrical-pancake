using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace LondonStreets.EditorTools
{
    /// <summary>
    /// Builds the London scene for you: the city, traffic, pedestrians, two
    /// parked cars, a phone box and a postbox, the player, the minimap and the
    /// sky. Use the menu London Streets > Build London Scene. The first time
    /// the project opens with this folder in it, it offers to do it for you.
    /// </summary>
    [InitializeOnLoad]
    public static class LondonSetup
    {
        public const string ScenePath = AssetLibrary.Root + "/Scenes/London.unity";
        const string MaterialsFolder = AssetLibrary.Root + "/Materials";
        const string ResourcesFolder = AssetLibrary.Root + "/Resources";
        const string OfferedKey = "LondonStreets.OfferedToBuild";

        static LondonSetup()
        {
            EditorApplication.delayCall += OfferToBuild;
        }

        static void OfferToBuild()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || File.Exists(ScenePath) || !AssetDatabase.IsValidFolder(AssetLibrary.Root))
                return;
            if (SessionState.GetBool(OfferedKey, false))
                return;
            SessionState.SetBool(OfferedKey, true);
            if (EditorUtility.DisplayDialog("London Streets",
                    "Build the London Streets scene now?\n\nYou can also do this later from the menu London Streets > Build London Scene.",
                    "Build it", "Not now"))
                BuildScene();
        }

        [MenuItem("London Streets/Open London Scene", priority = 0)]
        static void OpenScene()
        {
            if (!File.Exists(ScenePath))
            {
                BuildScene();
                return;
            }
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(ScenePath);
        }

        [MenuItem("London Streets/Build London Scene", priority = 1)]
        public static void BuildScene()
        {
            if (EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("London Streets", "Stop playing first (press the Play button again).", "OK");
                return;
            }
            if (File.Exists(ScenePath) && !EditorUtility.DisplayDialog("London Streets",
                    "The London scene already exists. Build it again from scratch? Anything you've changed in it will be lost.",
                    "Rebuild it", "Cancel"))
                return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            CreateFolder(AssetLibrary.Root, "Scenes");
            CreateFolder(AssetLibrary.Root, "Materials");
            CreateFolder(AssetLibrary.Root, "Resources");
            CreateFolder(AssetLibrary.Root, "Characters");
            CreateFolder(AssetLibrary.Root, "Cars");
            EnsureBaseMaterial();
            AssetFinder.Refresh();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // The sun, sky and haze.
            var sun = new GameObject("Sun");
            var light = sun.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            light.color = new Color(1f, 0.96f, 0.9f);
            light.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.LookRotation(new Vector3(-0.321f, -0.766f, 0.557f));
            var sky = sun.AddComponent<LondonSky>();
            sky.skyMaterial = SkyMaterial();

            var city = new GameObject("City").AddComponent<CityGrid>();

            var traffic = new GameObject("Traffic").AddComponent<Traffic>();
            traffic.city = city;
            var people = new GameObject("Pedestrians").AddComponent<Pedestrians>();
            people.city = city;

            // A phone box and a postbox by the starting crossroads.
            var props = new GameObject("Props").transform;
            var red = ColourMaterial("Post Office Red", new Color(0.78f, 0.06f, 0.14f), 0.5f);
            AddBox("PhoneBox", props, new Vector3(9.55f, 1.38f, -14f), new Vector3(1f, 2.6f, 1f), red);
            AddBox("PostBox", props, new Vector3(-9.6f, 0.78f, -20f), new Vector3(0.6f, 1.4f, 0.6f), red);
            var slot = new GameObject("YourModelHere");
            slot.transform.SetParent(props, false);
            slot.transform.localPosition = new Vector3(8.5f, 0.08f, -45f);
            slot.AddComponent<ModelProp>();

            // Two parked cars, in the left-hand lane.
            var vehicles = new GameObject("Vehicles").transform;
            AddCar("Car", vehicles, new Vector3(-5.1f, 0.3f, -30f), 0f);
            AddCar("Car2", vehicles, new Vector3(5.1f, 0.3f, 25f), 180f);

            var player = new GameObject("Player");
            player.transform.position = new Vector3(-8f, 0.2f, -26f);
            player.AddComponent<PlayerController>();

            var minimap = new GameObject("Minimap").AddComponent<Minimap>();
            minimap.city = city;

            EditorSceneManager.SaveScene(scene, ScenePath);
            AddToBuildSettings();
            Selection.activeGameObject = city.gameObject;
            SceneView.lastActiveSceneView?.Frame(new Bounds(new Vector3(0f, 0f, -20f), new Vector3(80f, 20f, 80f)), false);
            Debug.Log("London Streets: scene built at " + ScenePath + ". Press Play to walk around.");
        }

        static void AddBox(string name, Transform parent, Vector3 position, Vector3 size, Material material)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetParent(parent, false);
            box.transform.localPosition = position;
            box.transform.localScale = size;
            box.GetComponent<MeshRenderer>().sharedMaterial = material;
        }

        static void AddCar(string name, Transform parent, Vector3 position, float yaw)
        {
            var car = new GameObject(name);
            car.transform.SetParent(parent, false);
            car.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            car.AddComponent<Car>();
        }

        static void AddToBuildSettings()
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            if (scenes.Any(s => s.path == ScenePath))
                return;
            scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        static void CreateFolder(string parent, string name)
        {
            if (!AssetDatabase.IsValidFolder(parent + "/" + name))
                AssetDatabase.CreateFolder(parent, name);
        }

        /// <summary>
        /// The material everything in the game is copied from. Keeping it in a
        /// Resources folder makes sure its shader is included in a built game.
        /// </summary>
        static void EnsureBaseMaterial()
        {
            var path = ResourcesFolder + "/" + LondonMaterials.TemplateName + ".mat";
            var shader = LondonMaterials.LitShader();
            if (shader == null)
                return;
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                AssetDatabase.CreateAsset(new Material(shader), path);
            }
            else if (material.shader != shader)
            {
                material.shader = shader; // The render pipeline has changed since it was made.
                EditorUtility.SetDirty(material);
            }
        }

        static Material ColourMaterial(string name, Color colour, float roughness)
        {
            var path = MaterialsFolder + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(LondonMaterials.LitShader());
                AssetDatabase.CreateAsset(material, path);
            }
            material.shader = LondonMaterials.LitShader();
            material.color = colour;
            if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", 1f - roughness);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 1f - roughness);
            EditorUtility.SetDirty(material);
            return material;
        }

        static Material SkyMaterial()
        {
            var path = MaterialsFolder + "/London Sky.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            var shader = Shader.Find("Skybox/Procedural");
            if (material == null && shader != null)
            {
                material = new Material(shader);
                material.SetColor("_SkyTint", new Color(0.52f, 0.56f, 0.62f));
                material.SetColor("_GroundColor", new Color(0.32f, 0.36f, 0.32f));
                material.SetFloat("_AtmosphereThickness", 1.15f);
                material.SetFloat("_Exposure", 1.1f);
                material.SetFloat("_SunSize", 0.03f);
                AssetDatabase.CreateAsset(material, path);
            }
            return material;
        }
    }
}

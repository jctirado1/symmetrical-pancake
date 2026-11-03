using UnityEngine;
using UnityEngine.Rendering;

namespace LondonStreets
{
    /// <summary>
    /// Makes plain coloured materials that work with whichever render pipeline
    /// your project uses (Built-in, URP or HDRP), so nothing shows up pink.
    /// </summary>
    public static class LondonMaterials
    {
        /// <summary>Name of the template material the setup puts in a Resources folder.</summary>
        public const string TemplateName = "LondonStreetsMaterial";

        static Material _template;

        /// <summary>The lit shader for the current render pipeline.</summary>
        public static Shader LitShader()
        {
            var pipeline = GraphicsSettings.currentRenderPipeline;
            if (pipeline == null)
                return Shader.Find("Standard");
            var name = pipeline.GetType().Name;
            if (name.Contains("HDRenderPipeline") || name.Contains("HighDefinition"))
                return Shader.Find("HDRP/Lit");
            return Shader.Find("Universal Render Pipeline/Lit");
        }

        static Material Template()
        {
            if (_template != null)
                return _template;
            var lit = LitShader();
            var asset = Resources.Load<Material>(TemplateName);
            if (asset != null && (lit == null || asset.shader == lit))
            {
                _template = asset;
            }
            else
            {
                _template = new Material(lit != null ? lit : Shader.Find("Standard"));
                _template.hideFlags = HideFlags.DontSave;
            }
            return _template;
        }

        /// <param name="roughness">0 = shiny, 1 = matt.</param>
        public static Material Create(Color color, float roughness = 0.9f, float metallic = 0f, Texture texture = null)
        {
            var material = new Material(Template());
            material.hideFlags = HideFlags.DontSave;
            material.color = color;
            if (texture != null)
                material.mainTexture = texture;
            SetFloat(material, "_Glossiness", 1f - roughness);
            SetFloat(material, "_Smoothness", 1f - roughness);
            SetFloat(material, "_Metallic", metallic);
            return material;
        }

        /// <summary>A material showing <paramref name="texture"/>, for models that come in white.</summary>
        public static Material Textured(Texture texture) => Create(Color.white, 0.8f, 0f, texture);

        static void SetFloat(Material material, string property, float value)
        {
            if (material.HasProperty(property))
                material.SetFloat(property, value);
        }
    }
}

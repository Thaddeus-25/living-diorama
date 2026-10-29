using UnityEditor;
using UnityEditor.Rendering.Universal.ShaderGUI;
using UnityEngine;
using UnityEngine.Rendering;

namespace LivingDiorama.EditorTools
{
    /// <summary>Creates the handful of shared URP materials the diorama uses.</summary>
    internal static class MaterialFactory
    {
        private const string LitShader = "Universal Render Pipeline/Lit";
        private const string UnlitShader = "Universal Render Pipeline/Unlit";
        private const string ParticlesSimpleLitShader = "Universal Render Pipeline/Particles/Simple Lit";
        private const string ParticlesUnlitShader = "Universal Render Pipeline/Particles/Unlit";

        /// <summary>Lit, alpha-clipped, double-sided material for billboard sprites. Writes depth, so no sort orders are needed.</summary>
        public static Material CreateSpriteLit(string path)
        {
            var material = new Material(FindShader(LitShader)) { name = "M_SpriteLit" };
            MakeCutout(material);
            MakeMatte(material);
            return Save(material, path);
        }

        /// <summary>Unlit cutout for painted backdrop layers (hills).</summary>
        public static Material CreateBackdrop(string path)
        {
            var material = new Material(FindShader(UnlitShader)) { name = "M_Backdrop" };
            MakeCutout(material);
            return Save(material, path);
        }

        /// <summary>Unlit, alpha-blended and tinted; for soft ground decals like contact shadows.</summary>
        public static Material CreateTransparentTint(string path, Color tint)
        {
            var material = new Material(FindShader(UnlitShader)) { name = System.IO.Path.GetFileNameWithoutExtension(path) };
            material.SetColor("_BaseColor", tint);
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            material.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.SetOverrideTag("RenderType", "Transparent");
            material.renderQueue = (int)RenderQueue.Transparent;
            return Save(material, path);
        }

        /// <summary>Alpha-blended, lit particle material (tinted per particle by vertex colour).</summary>
        public static Material CreateParticleLit(string path, Texture texture)
        {
            // Simple Lit, so particles (e.g. leaves) darken at night with the rest of the scene.
            var material = new Material(FindShader(ParticlesSimpleLitShader)) { name = System.IO.Path.GetFileNameWithoutExtension(path) };
            material.SetTexture("_BaseMap", texture);
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.SetOverrideTag("RenderType", "Transparent");
            material.renderQueue = (int)RenderQueue.Transparent;
            return Save(material, path);
        }

        /// <summary>Additive, unlit particles that glow (embers, sparks).</summary>
        public static Material CreateParticleGlow(string path, Texture texture)
        {
            var material = new Material(FindShader(ParticlesUnlitShader)) { name = System.IO.Path.GetFileNameWithoutExtension(path) };
            material.SetTexture("_BaseMap", texture);
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 2f); // additive
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.One);
            material.SetFloat("_ZWrite", 0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.SetOverrideTag("RenderType", "Transparent");
            material.renderQueue = (int)RenderQueue.Transparent;
            return Save(material, path);
        }

        public static Material CreateUnlitTextured(string path, Texture texture)
        {
            var material = new Material(FindShader(UnlitShader)) { name = System.IO.Path.GetFileNameWithoutExtension(path) };
            material.SetTexture("_BaseMap", texture);
            return Save(material, path);
        }

        /// <summary>Lit ground material; <paramref name="tiling"/> is in texture repeats (1 repeat = 1 metre).</summary>
        public static Material CreateGround(string path, Texture texture, Vector2 tiling)
        {
            var material = new Material(FindShader(LitShader)) { name = System.IO.Path.GetFileNameWithoutExtension(path) };
            material.SetTexture("_BaseMap", texture);
            material.SetTextureScale("_BaseMap", tiling);
            MakeMatte(material);
            return Save(material, path);
        }

        private static void MakeCutout(Material material)
        {
            material.SetFloat("_Surface", 0f);
            material.SetFloat("_AlphaClip", 1f);
            material.SetFloat("_Cutoff", 0.5f);
            material.SetFloat("_Cull", (float)CullMode.Off);
            material.EnableKeyword("_ALPHATEST_ON");
            material.SetOverrideTag("RenderType", "TransparentCutout");
            material.renderQueue = (int)RenderQueue.AlphaTest;
        }

        private static void MakeMatte(Material material)
        {
            material.SetFloat("_Smoothness", 0f);
            material.SetFloat("_SpecularHighlights", 0f);
            material.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            material.SetFloat("_EnvironmentReflections", 0f);
            material.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
        }

        private static Shader FindShader(string shaderName)
        {
            var shader = Shader.Find(shaderName);
            if (shader == null)
                throw new System.InvalidOperationException($"Shader '{shaderName}' not found. Is the URP package installed?");
            return shader;
        }

        /// <summary>Creates the asset, or updates it in place so existing references (GUIDs) survive.</summary>
        private static Material Save(Material material, string path)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing == null)
            {
                ApplyUrpMaterialSetup(material);
                AssetDatabase.CreateAsset(material, path);
                return material;
            }

            existing.shader = material.shader;
            existing.CopyPropertiesFromMaterial(material);
            existing.shaderKeywords = material.shaderKeywords;
            existing.renderQueue = material.renderQueue;
            existing.SetOverrideTag("RenderType", material.GetTag("RenderType", false));
            // Recomputes what CopyPropertiesFromMaterial doesn't carry over (double-sided GI, shader passes).
            ApplyUrpMaterialSetup(existing);
            EditorUtility.SetDirty(existing);
            Object.DestroyImmediate(material);
            return existing;
        }

        /// <summary>
        /// Runs URP's own material setup, the same calls its material postprocessor makes when the
        /// Editor validates a material (keywords, alpha-to-coverage, double-sided GI, motion-vector
        /// pass, legacy _Color). Generated files then match what Unity saves, so opening the project
        /// no longer shows them as changed.
        /// </summary>
        private static void ApplyUrpMaterialSetup(Material material)
        {
            switch (material.shader.name)
            {
                case LitShader:
                    BaseShaderGUI.SetMaterialKeywords(material, LitGUI.SetMaterialKeywords);
                    break;
                case UnlitShader:
                    BaseShaderGUI.SetMaterialKeywords(material);
                    break;
                case ParticlesSimpleLitShader:
                    BaseShaderGUI.SetMaterialKeywords(material, SimpleLitGUI.SetMaterialKeywords, ParticleGUI.SetMaterialKeywords);
                    break;
                case ParticlesUnlitShader:
                    BaseShaderGUI.SetMaterialKeywords(material, null, ParticleGUI.SetMaterialKeywords);
                    break;
                // Custom shaders (e.g. the tilt-shift pass) have no URP setup.
            }
        }
    }
}

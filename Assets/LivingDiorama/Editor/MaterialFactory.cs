using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace LivingDiorama.EditorTools
{
    /// <summary>Creates the handful of shared URP materials the diorama uses.</summary>
    internal static class MaterialFactory
    {
        private const string LitShader = "Universal Render Pipeline/Lit";
        private const string UnlitShader = "Universal Render Pipeline/Unlit";

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
                AssetDatabase.CreateAsset(material, path);
                return material;
            }

            existing.shader = material.shader;
            existing.CopyPropertiesFromMaterial(material);
            existing.shaderKeywords = material.shaderKeywords;
            existing.renderQueue = material.renderQueue;
            existing.SetOverrideTag("RenderType", material.GetTag("RenderType", false));
            EditorUtility.SetDirty(existing);
            Object.DestroyImmediate(material);
            return existing;
        }
    }
}

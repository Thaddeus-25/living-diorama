using System.IO;
using LivingDiorama.Rendering;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace LivingDiorama.EditorTools
{
    /// <summary>
    /// The HD-2D atmosphere: a tilt-shift blur pass on the URP renderers, plus a post-processing
    /// profile (soft bloom, vignette, warm colour grading) used by a global Volume in the scene.
    /// </summary>
    public static class AtmosphereBuilder
    {
        public const string SettingsFolder = ProjectBootstrap.Root + "/Settings";
        public const string ProfilePath = SettingsFolder + "/DioramaVolumeProfile.asset";
        private const string TiltShiftShader = "Hidden/LivingDiorama/TiltShift";

        private static readonly string[] RendererPaths =
        {
            SettingsFolder + "/PC_Renderer.asset",
            SettingsFolder + "/Mobile_Renderer.asset",
        };

        /// <summary>Adds (or updates) the tilt-shift pass on every URP renderer asset.</summary>
        public static void InstallTiltShift()
        {
            var shader = Shader.Find(TiltShiftShader)
                         ?? throw new FileNotFoundException($"Shader '{TiltShiftShader}' not found.");
            string materialPath = ProjectBootstrap.MaterialsFolder + "/M_TiltShift.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                material = new Material(shader) { name = "M_TiltShift" };
                AssetDatabase.CreateAsset(material, materialPath);
            }
            material.shader = shader;

            foreach (var path in RendererPaths)
            {
                var rendererData = AssetDatabase.LoadAssetAtPath<ScriptableRendererData>(path);
                if (rendererData == null)
                {
                    Debug.LogWarning($"[Atmosphere] No renderer asset at {path}; tilt-shift not installed there.");
                    continue;
                }

                var feature = rendererData.rendererFeatures.Find(f => f is TiltShiftFeature) as TiltShiftFeature;
                if (feature == null)
                {
                    feature = ScriptableObject.CreateInstance<TiltShiftFeature>();
                    feature.name = "TiltShift";
                    AssetDatabase.AddObjectToAsset(feature, rendererData);
                    RegisterFeature(rendererData, feature);
                }
                feature.Configuration.material = material;
                feature.Create();
                EditorUtility.SetDirty(feature);
                EditorUtility.SetDirty(rendererData);
            }
            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// Adds the feature the way the renderer's Inspector does: to the feature list and to the
        /// map of local file ids that URP uses to keep that list consistent.
        /// </summary>
        private static void RegisterFeature(ScriptableRendererData rendererData, ScriptableRendererFeature feature)
        {
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out _, out long localId);
            var serialized = new SerializedObject(rendererData);
            var features = serialized.FindProperty("m_RendererFeatures");
            var map = serialized.FindProperty("m_RendererFeatureMap");

            features.arraySize++;
            features.GetArrayElementAtIndex(features.arraySize - 1).objectReferenceValue = feature;
            map.arraySize++;
            map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Rebuilds the post-processing profile from scratch, so tweaks here are the source of truth.</summary>
        public static VolumeProfile CreateProfile()
        {
            AssetDatabase.DeleteAsset(ProfilePath);
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, ProfilePath);

            // Soft glow on the brightest areas (sky, sunlit highlights).
            var bloom = Add<Bloom>(profile);
            bloom.threshold.Override(0.9f);
            bloom.intensity.Override(0.35f);
            bloom.scatter.Override(0.65f);

            // Darkened corners frame the diorama like a photograph of a model.
            var vignette = Add<Vignette>(profile);
            vignette.color.Override(new Color(0.16f, 0.1f, 0.08f));
            vignette.intensity.Override(0.25f);
            vignette.smoothness.Override(0.5f);

            // Warm, slightly richer colours.
            var colour = Add<ColorAdjustments>(profile);
            colour.postExposure.Override(0.1f);
            colour.contrast.Override(6f);
            colour.saturation.Override(12f);
            colour.colorFilter.Override(new Color(1f, 0.97f, 0.92f));

            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            return profile;
        }

        private static T Add<T>(VolumeProfile profile) where T : VolumeComponent
        {
            var component = profile.Add<T>();
            component.name = typeof(T).Name;
            AssetDatabase.AddObjectToAsset(component, profile);
            return component;
        }
    }
}

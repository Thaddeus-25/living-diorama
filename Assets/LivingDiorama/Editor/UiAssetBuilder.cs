using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace LivingDiorama.EditorTools
{
    /// <summary>Creates the UI Toolkit runtime theme and PanelSettings used by the editor overlay.</summary>
    internal static class UiAssetBuilder
    {
        public const string UiFolder = ProjectBootstrap.Root + "/UI";
        public const string ThemePath = UiFolder + "/DioramaRuntimeTheme.tss";
        public const string PanelSettingsPath = UiFolder + "/DioramaPanelSettings.asset";
        public const string HudLayoutPath = UiFolder + "/EditorHud.uxml";

        public static PanelSettings EnsureAssets()
        {
            if (!File.Exists(ThemePath))
            {
                // Same content as the theme Unity creates with a new PanelSettings: the built-in runtime look.
                File.WriteAllText(ThemePath, "@import url(\"unity-theme://default\");\n");
                AssetDatabase.ImportAsset(ThemePath, ImportAssetOptions.ForceSynchronousImport);
            }

            var theme = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(ThemePath);
            if (theme == null) throw new FileNotFoundException($"Theme not found at {ThemePath}.");

            var settings = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<PanelSettings>();
                AssetDatabase.CreateAsset(settings, PanelSettingsPath);
            }

            settings.themeStyleSheet = theme;
            // Lay the UI out for 1600x900 and scale it to the actual window size.
            settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            settings.referenceResolution = new Vector2Int(1600, 900);
            settings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            settings.match = 0.5f;
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            return settings;
        }
    }
}

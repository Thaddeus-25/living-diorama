using System.Collections;
using System.IO;
using System.Linq;
using LivingDiorama.SceneEditing;
using LivingDiorama.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace LivingDiorama.Tests
{
    /// <summary>
    /// Not a real test: renders the Garden scene plus the editor UI (with an object selected) to
    /// Verification/hud.png so the layout can be checked from the command line.
    /// Run with: -runTests -testPlatform PlayMode -testFilter HudScreenshotCapture
    /// </summary>
    [Explicit("Writes a screenshot; run on demand.")]
    public class HudScreenshotCapture
    {
        private const int Width = 1600;
        private const int Height = 900;

        [UnityTest]
        public IEnumerator CaptureHudWithSelection()
        {
            yield return SceneManager.LoadSceneAsync("Garden", LoadSceneMode.Single);
            yield return null;

            var editor = Object.FindAnyObjectByType<SceneEditorController>();
            var world = Object.FindAnyObjectByType<DioramaWorld>();
            editor.Select(world.Objects.First(o => o.Definition.Id == "bench"));

            var document = Object.FindAnyObjectByType<UIDocument>();
            var uiTarget = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var panelSettings = document.panelSettings;
            var previousTarget = panelSettings.targetTexture;
            panelSettings.targetTexture = uiTarget;
            panelSettings.clearColor = true;
            panelSettings.colorClearValue = Color.clear;

            for (int i = 0; i < 5; i++) yield return null;

            var cam = Camera.main;
            var worldTarget = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            cam.targetTexture = worldTarget;
            cam.Render();
            cam.targetTexture = null;

            var worldImage = Read(worldTarget);
            var uiImage = Read(uiTarget);
            panelSettings.targetTexture = previousTarget;
            panelSettings.clearColor = false;

            // Alpha-blend the UI over the world.
            var worldPixels = worldImage.GetPixels32();
            var uiPixels = uiImage.GetPixels32();
            for (int i = 0; i < worldPixels.Length; i++)
            {
                float a = uiPixels[i].a / 255f;
                worldPixels[i] = Color32.Lerp(worldPixels[i], uiPixels[i], a);
                worldPixels[i].a = 255;
            }
            worldImage.SetPixels32(worldPixels);

            string folder = Path.Combine(Directory.GetCurrentDirectory(), "Verification");
            Directory.CreateDirectory(folder);
            File.WriteAllBytes(Path.Combine(folder, "hud.png"), worldImage.EncodeToPNG());
            Debug.Log($"[HudScreenshot] Saved {Path.Combine(folder, "hud.png")}");
        }

        private static Texture2D Read(RenderTexture source)
        {
            var previous = RenderTexture.active;
            RenderTexture.active = source;
            var image = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
            image.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
            image.Apply();
            RenderTexture.active = previous;
            return image;
        }
    }
}

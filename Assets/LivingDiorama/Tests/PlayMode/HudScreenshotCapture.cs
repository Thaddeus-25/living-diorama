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

            yield return CaptureWorldAndUi("hud.png");
        }

        /// <summary>The Load dialog with two saved scenes (in a temporary folder), saved to Verification/load-dialog.png.</summary>
        [UnityTest]
        public IEnumerator CaptureLoadDialog()
        {
            yield return SceneManager.LoadSceneAsync("Garden", LoadSceneMode.Single);
            yield return null;

            var service = Object.FindAnyObjectByType<LivingDiorama.Persistence.SceneSaveService>();
            string temp = Path.Combine(Path.GetTempPath(), "LivingDioramaTests", System.Guid.NewGuid().ToString("N"));
            service.UseStore(new LivingDiorama.Persistence.SaveFileStore(temp));
            service.Save("Autumn Park");
            service.Save("My Garden");

            var hud = Object.FindAnyObjectByType<LivingDiorama.UI.EditorHud>();
            hud.Files.OpenLoadDialog();
            yield return CaptureWorldAndUi("load-dialog.png");
            Directory.Delete(temp, recursive: true);
        }

        /// <summary>Renders the camera and the UI Toolkit overlay separately and blends them into one PNG.</summary>
        private static IEnumerator CaptureWorldAndUi(string fileName)
        {
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
            File.WriteAllBytes(Path.Combine(folder, fileName), worldImage.EncodeToPNG());
            Debug.Log($"[HudScreenshot] Saved {Path.Combine(folder, fileName)}");
        }

        /// <summary>Preview mode with the human on the bench and the cat napping, saved to Verification/interactions.png.</summary>
        [UnityTest]
        public IEnumerator CaptureSittingAndNapping()
        {
            yield return SceneManager.LoadSceneAsync("Garden", LoadSceneMode.Single);
            yield return null;
            Object.FindAnyObjectByType<LivingDiorama.Core.AppModeController>().SetMode(LivingDiorama.Core.AppMode.Preview);
            yield return null;

            var creatures = Object.FindObjectsByType<LivingDiorama.Entities.LivingEntity>(FindObjectsSortMode.None);
            var human = creatures.First(c => c.Definition.Id == "human");
            var cat = creatures.First(c => c.Definition.Id == "cat");
            human.StartBehaviour(LivingDiorama.Data.BehaviourWeight.Interact(LivingDiorama.Data.InteractionType.Sit, 1f, new Vector2(60f, 60f)));
            cat.StartBehaviour(LivingDiorama.Data.BehaviourWeight.Interact(LivingDiorama.Data.InteractionType.Sleep, 1f, new Vector2(60f, 60f)));

            Time.timeScale = 3f;
            for (float t = 0f; t < 20f; t += Time.deltaTime)
            {
                if ((human.CurrentState as LivingDiorama.Entities.InteractState)?.IsHolding == true
                    && (cat.CurrentState as LivingDiorama.Entities.InteractState)?.IsHolding == true) break;
                yield return null;
            }
            Time.timeScale = 1f;

            var cam = Camera.main;
            var target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            cam.targetTexture = target;
            cam.Render();
            cam.targetTexture = null;
            var image = Read(target);

            string folder = Path.Combine(Directory.GetCurrentDirectory(), "Verification");
            Directory.CreateDirectory(folder);
            File.WriteAllBytes(Path.Combine(folder, "interactions.png"), image.EncodeToPNG());
            Debug.Log($"[HudScreenshot] human={human.CurrentState?.Name} cat={cat.CurrentState?.Name}");
        }

        /// <summary>The garden at four times of day, saved to Verification/daynight-HH.png.</summary>
        [UnityTest]
        public IEnumerator CaptureTimesOfDay()
        {
            yield return SceneManager.LoadSceneAsync("Garden", LoadSceneMode.Single);
            yield return null;
            var cycle = Object.FindAnyObjectByType<LivingDiorama.World.DayNightCycle>();
            cycle.SetAutoAdvance(false);
            Object.FindAnyObjectByType<LivingDiorama.UI.EditorHud>().GetComponent<UIDocument>().rootVisualElement.style.display = DisplayStyle.None;

            foreach (float hour in new[] { 6.5f, 12f, 17.6f, 22f })
            {
                cycle.SetTime(hour);
                for (int i = 0; i < 3; i++) yield return null;
                var cam = Camera.main;
                var target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                cam.targetTexture = target;
                cam.Render();
                cam.targetTexture = null;
                string folder = Path.Combine(Directory.GetCurrentDirectory(), "Verification");
                Directory.CreateDirectory(folder);
                File.WriteAllBytes(Path.Combine(folder, $"daynight-{hour:00}.png"), Read(target).EncodeToPNG());
            }
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

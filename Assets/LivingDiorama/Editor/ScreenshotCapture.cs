using System.IO;
using LivingDiorama.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LivingDiorama.EditorTools
{
    /// <summary>
    /// Renders the Garden scene's main camera to Verification/garden.png (git-ignored).
    /// Lets the scene be checked visually from the command line, without opening the editor.
    /// </summary>
    public static class ScreenshotCapture
    {
        private const int Width = 1600;
        private const int Height = 900;

        [MenuItem("Living Diorama/Capture Garden Screenshot")]
        public static void CaptureGarden()
        {
            EditorSceneManager.OpenScene(GardenSceneBuilder.ScenePath, OpenSceneMode.Single);
            var cam = Camera.main;
            if (cam == null) throw new System.InvalidOperationException("Garden scene has no MainCamera.");

            Billboard.AlignAll(cam);

            var renderTexture = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var previousTarget = cam.targetTexture;
            var previousActive = RenderTexture.active;
            // The editor compiles shader variants asynchronously and draws not-yet-compiled ones
            // invisible; a one-off capture must wait for real shaders.
            bool previousAsync = ShaderUtil.allowAsyncCompilation;
            ShaderUtil.allowAsyncCompilation = false;
            try
            {
                cam.targetTexture = renderTexture;
                cam.Render();

                RenderTexture.active = renderTexture;
                var image = new Texture2D(Width, Height, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
                image.Apply();

                string folder = Path.Combine(Directory.GetCurrentDirectory(), "Verification");
                Directory.CreateDirectory(folder);
                string file = Path.Combine(folder, "garden.png");
                File.WriteAllBytes(file, image.EncodeToPNG());
                Object.DestroyImmediate(image);
                Debug.Log($"[ScreenshotCapture] Saved {file}");
            }
            finally
            {
                ShaderUtil.allowAsyncCompilation = previousAsync;
                cam.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                Object.DestroyImmediate(renderTexture);
            }
        }
    }
}

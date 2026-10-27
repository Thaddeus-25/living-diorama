using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

namespace LivingDiorama.Rendering
{
    /// <summary>
    /// Adds the tilt-shift "miniature" blur (see TiltShift.shader). URP's own depth of field assumes
    /// a perspective camera, so with our orthographic camera the blur is driven by screen height,
    /// which for a fixed, angled camera matches depth closely. Tune it on the renderer asset.
    /// </summary>
    public class TiltShiftFeature : ScriptableRendererFeature
    {
        [Serializable]
        public class Settings
        {
            public Material material;
            [Tooltip("Screen height (0 = bottom, 1 = top) of the middle of the sharp band.")]
            [Range(0f, 1f)] public float focusCenter = 0.45f;
            [Tooltip("Half the height of the sharp band.")]
            [Range(0f, 0.5f)] public float focusHalfWidth = 0.2f;
            [Tooltip("How far past the band the blur reaches full strength.")]
            [Range(0.01f, 1f)] public float falloff = 0.28f;
            [Tooltip("Full-strength blur radius in pixels (at 900 px screen height).")]
            [Range(0f, 8f)] public float blurRadius = 3f;
            [Tooltip("Blur strength below the band, relative to above it.")]
            [Range(0f, 1f)] public float nearBlurScale = 0.5f;
        }

        [SerializeField] private Settings settings = new();
        private TiltShiftPass pass;

        public Settings Configuration => settings;

        public override void Create()
        {
            pass = new TiltShiftPass { renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (settings.material == null || settings.blurRadius <= 0f) return;
            // Only the game camera; not scene-view previews or reflection probes.
            if (renderingData.cameraData.cameraType != CameraType.Game) return;
            pass.Setup(settings);
            renderer.EnqueuePass(pass);
        }

        private sealed class TiltShiftPass : ScriptableRenderPass
        {
            private static readonly int FocusCenterId = Shader.PropertyToID("_FocusCenter");
            private static readonly int FocusHalfWidthId = Shader.PropertyToID("_FocusHalfWidth");
            private static readonly int FalloffId = Shader.PropertyToID("_Falloff");
            private static readonly int BlurRadiusId = Shader.PropertyToID("_BlurRadius");
            private static readonly int NearBlurScaleId = Shader.PropertyToID("_NearBlurScale");

            private Settings settings;

            public TiltShiftPass()
            {
                // Reading and replacing the camera colour needs an intermediate texture, not the back buffer.
                requiresIntermediateTexture = true;
            }

            public void Setup(Settings newSettings) => settings = newSettings;

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                var resources = frameData.Get<UniversalResourceData>();
                if (resources.isActiveTargetBackBuffer) return;

                var material = settings.material;
                material.SetFloat(FocusCenterId, settings.focusCenter);
                material.SetFloat(FocusHalfWidthId, settings.focusHalfWidth);
                material.SetFloat(FalloffId, settings.falloff);
                material.SetFloat(BlurRadiusId, settings.blurRadius);
                material.SetFloat(NearBlurScaleId, settings.nearBlurScale);

                var source = resources.activeColorTexture;
                var description = renderGraph.GetTextureDesc(source);
                description.name = "_TiltShiftColor";
                description.clearBuffer = false;
                var destination = renderGraph.CreateTexture(description);

                renderGraph.AddBlitPass(new RenderGraphUtils.BlitMaterialParameters(source, destination, material, 0), "Tilt Shift");
                resources.cameraColor = destination;
            }
        }
    }
}

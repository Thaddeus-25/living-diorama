using LivingDiorama.Data;
using LivingDiorama.World;
using UnityEngine;
using UnityEngine.Rendering;

namespace LivingDiorama.EditorTools
{
    /// <summary>Extras added to prop prefabs: lamp lights, the campfire's flames, light and embers.</summary>
    internal static class PropDecorations
    {
        private static readonly Color WarmLamp = new(1f, 0.84f, 0.62f);
        private static readonly Color FireLight = new(1f, 0.7f, 0.45f);

        /// <summary>
        /// A warm point light that switches on at dusk, placed at <paramref name="height"/> metres up the
        /// sprite and a little towards the camera so it lights the lamp's face and the ground.
        /// </summary>
        public static void AddLamp(SpriteVisual visual, float height, float range, float nightIntensity)
        {
            AddLight(visual, "LampLight", height, range, WarmLamp, nightIntensity, dayIntensity: 0f, flicker: 0f);
        }

        /// <summary>Animated flames, a flickering light (dim by day, bright at night) and rising embers.</summary>
        public static void AddCampfire(SpriteVisual visual, SpriteAnimationSet flames, Material emberMaterial)
        {
            var animator = visual.gameObject.AddComponent<SpriteAnimator>();
            animator.Configure(visual, flames);

            AddLight(visual, "FireLight", 0.7f, 4.5f, FireLight, nightIntensity: 2.4f, dayIntensity: 0.15f, flicker: 0.25f);
            AddEmbers(visual.transform, emberMaterial);
        }

        private static void AddLight(SpriteVisual visual, string name, float height, float range, Color colour,
            float nightIntensity, float dayIntensity, float flicker)
        {
            var lightObject = new GameObject(name);
            lightObject.transform.SetParent(visual.transform, false);
            lightObject.transform.localPosition = new Vector3(0f, height, -0.35f);

            var lamp = lightObject.AddComponent<Light>();
            lamp.type = LightType.Point;
            lamp.color = colour;
            lamp.range = range;
            lamp.intensity = dayIntensity;
            lamp.shadows = LightShadows.None; // many small lights; shadows from the sun are enough

            lightObject.AddComponent<NightLight>().Configure(nightIntensity, dayIntensity, flicker);
        }

        private static void AddEmbers(Transform parent, Material material)
        {
            var emitter = new GameObject("Embers");
            emitter.transform.SetParent(parent, false);
            emitter.transform.localPosition = new Vector3(0f, 0.55f, -0.05f);

            var particles = emitter.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = particles.main;
            main.loop = true;
            main.prewarm = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2.2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 0.9f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.1f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.55f, 0.15f), new Color(1f, 0.85f, 0.35f));
            main.gravityModifier = -0.02f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 12;

            var emission = particles.emission;
            emission.rateOverTime = 4f;

            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 12f;
            shape.radius = 0.2f;
            // Point the cone straight up in world space (the emitter sits on a camera-facing sprite).
            emitter.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);

            var noise = particles.noise;
            noise.enabled = true;
            noise.strength = 0.25f;
            noise.frequency = 1.2f;

            var colour = particles.colorOverLifetime;
            colour.enabled = true;
            var fade = new Gradient();
            fade.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(1f, 0.4f, 0.2f), 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.6f), new GradientAlphaKey(0f, 1f) });
            colour.color = fade;

            var renderer = emitter.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            particles.Play();
        }
    }
}

using LivingDiorama.World;
using UnityEngine;

namespace LivingDiorama.EditorTools
{
    /// <summary>
    /// Builds the falling-leaves particle effect for tree prefabs: an occasional leaf drifts and spins
    /// down from the canopy, lands on the ground and fades. Leaves live in world space, so moving
    /// the tree doesn't drag leaves that are already falling.
    /// </summary>
    internal static class LeafEffectBuilder
    {
        // Leaf tints (the leaf texture is white). Warm autumn colours stand out against the grass.
        private static readonly Color LeafYellow = new(0.98f, 0.8f, 0.32f);
        private static readonly Color LeafOrange = new(0.9f, 0.5f, 0.2f);

        public static void AddFallingLeaves(GameObject treeRoot, SpriteVisual canopyVisual, Material leafMaterial)
        {
            // The emitter sits in the canopy, in the billboarded sprite's own space, so it lines up
            // with the drawn leaves whatever the tree's yaw.
            var emitterObject = new GameObject("FallingLeaves");
            emitterObject.transform.SetParent(canopyVisual.transform, false);
            emitterObject.transform.localPosition = new Vector3(0f, 2.6f, -0.05f);

            // Leaves land on this plane: ground level under the tree (root stands on y = 0).
            var ground = new GameObject("LeafGround");
            ground.transform.SetParent(treeRoot.transform, false);
            ground.transform.localPosition = new Vector3(0f, 0.02f, 0f);

            var particles = emitterObject.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = particles.main;
            main.loop = true;
            main.prewarm = true; // leaves are already falling when the scene opens
            main.duration = 10f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(6f, 8f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.26f, 0.36f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new ParticleSystem.MinMaxGradient(LeafYellow, LeafOrange);
            main.gravityModifier = 0.025f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 8;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;

            var emission = particles.emission;
            emission.rateOverTime = 0.25f;

            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(2f, 1.2f, 0.1f); // roughly the canopy

            // A gentle sideways drift, plus wobble from noise, like a light breeze.
            var velocity = particles.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(-0.12f, 0.2f);
            velocity.y = new ParticleSystem.MinMaxCurve(-0.1f, -0.1f);
            velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);

            var noise = particles.noise;
            noise.enabled = true;
            noise.strength = 0.3f;
            noise.frequency = 0.5f;
            noise.scrollSpeed = 0.3f;
            noise.damping = true;

            var rotation = particles.rotationOverLifetime;
            rotation.enabled = true;
            rotation.z = new ParticleSystem.MinMaxCurve(-2f, 2f); // radians per second

            // Fade in at the canopy, rest on the ground, fade out at the end.
            var colour = particles.colorOverLifetime;
            colour.enabled = true;
            var fade = new Gradient();
            fade.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.08f), new GradientAlphaKey(1f, 0.8f), new GradientAlphaKey(0f, 1f) });
            colour.color = fade;

            // Stop on the ground instead of sinking through it.
            var collision = particles.collision;
            collision.enabled = true;
            collision.type = ParticleSystemCollisionType.Planes;
            collision.SetPlane(0, ground.transform);
            collision.dampen = 1f;
            collision.bounce = 0f;
            collision.lifetimeLoss = 0f;
            collision.radiusScale = 0.5f;

            var renderer = emitterObject.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = leafMaterial;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            particles.Play();
        }
    }
}

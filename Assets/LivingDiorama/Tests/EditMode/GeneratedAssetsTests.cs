using LivingDiorama.Data;
using LivingDiorama.EditorTools;
using LivingDiorama.World;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

namespace LivingDiorama.Tests
{
    /// <summary>Checks that the bootstrap produced a consistent set of assets.</summary>
    public class GeneratedAssetsTests
    {
        private ObjectDatabase database;

        [SetUp]
        public void LoadDatabase()
        {
            database = AssetDatabase.LoadAssetAtPath<ObjectDatabase>(ProjectBootstrap.DatabasePath);
            Assert.IsNotNull(database, "Run Living Diorama > Rebuild Generated Assets first.");
        }

        [TestCase("tree")]
        [TestCase("rock")]
        [TestCase("bench")]
        [TestCase("bush")]
        [TestCase("flowers")]
        [TestCase("mushrooms")]
        [TestCase("fence")]
        [TestCase("streetlight")]
        [TestCase("sign")]
        [TestCase("lantern")]
        [TestCase("campfire")]
        public void Definition_HasPrefabWithPlacedObjectAndVisual(string id)
        {
            var definition = database.Find(id);
            Assert.IsNotNull(definition, $"No definition '{id}'");
            Assert.IsNotNull(definition.Prefab, $"'{id}' has no prefab");
            Assert.IsNotNull(definition.Icon, $"'{id}' has no icon");

            var placed = definition.Prefab.GetComponent<PlacedObject>();
            Assert.IsNotNull(placed);
            Assert.AreSame(definition, placed.Definition);
            Assert.IsNotNull(placed.Visual);
            Assert.IsNotNull(placed.Visual.Sprite);
            Assert.AreEqual(definition.IsObstacle, definition.Prefab.GetComponent<NavMeshObstacle>() != null);
        }

        [Test]
        public void Find_UnknownId_ReturnsNull()
        {
            Assert.IsNull(database.Find("no_such_object"));
            Assert.IsNull(database.Find(null));
        }

        [Test]
        public void Sprites_UsePixelArtImportSettings()
        {
            var sprite = database.Find("tree").Icon;
            Assert.AreEqual(PlaceholderArtGenerator.PixelsPerUnit, sprite.pixelsPerUnit);
            Assert.AreEqual(FilterMode.Point, sprite.texture.filterMode);
            // Pivot at the bottom centre keeps feet on the ground.
            Assert.AreEqual(0f, sprite.bounds.min.y, 0.001f);
        }

        [TestCase("human")]
        [TestCase("cat")]
        public void Creatures_HaveAFlatUnclickableContactShadow(string id)
        {
            var shadow = database.Find(id).Prefab.transform.Find("ContactShadow");
            Assert.IsNotNull(shadow, "Contact shadow missing");
            Assert.IsNull(shadow.GetComponent<Collider>(), "Must not block clicks");
            Assert.IsNull(shadow.GetComponent<Billboard>(), "Lies flat instead of facing the camera");
            Assert.AreEqual(UnityEngine.Rendering.ShadowCastingMode.Off, shadow.GetComponent<MeshRenderer>().shadowCastingMode);
            Assert.AreEqual(90f, shadow.localEulerAngles.x, 0.01f);
        }

        [TestCase("streetlight")]
        [TestCase("lantern")]
        [TestCase("campfire")]
        public void LightSources_HaveANightLight(string id)
        {
            var prefab = database.Find(id).Prefab;
            var nightLight = prefab.GetComponentInChildren<NightLight>(true);
            Assert.IsNotNull(nightLight, $"{id} has no NightLight");
            var light = nightLight.GetComponent<Light>();
            Assert.AreEqual(LightType.Point, light.type);
            Assert.AreEqual(LightShadows.None, light.shadows, "Small lights shouldn't cast shadows");
        }

        [Test]
        public void Campfire_IsAnimated_HasEmbers_AndPlacesToWarmUpAndNap()
        {
            var campfire = database.Find("campfire").Prefab;
            var animator = campfire.GetComponentInChildren<SpriteAnimator>(true);
            Assert.IsNotNull(animator, "Flames should animate");
            Assert.IsNotNull(campfire.GetComponentInChildren<ParticleSystem>(true), "Embers missing");

            var points = campfire.GetComponentsInChildren<LivingDiorama.Interactions.InteractionPoint>(true);
            Assert.AreEqual(2, System.Array.FindAll(points, p => p.Type == InteractionType.Warm).Length);
            Assert.AreEqual(1, System.Array.FindAll(points, p => p.Type == InteractionType.Sleep).Length);
        }

        [TestCase("flowers")]
        [TestCase("mushrooms")]
        [TestCase("lantern")]
        public void SmallDecorations_DontBlockWalking(string id)
        {
            Assert.IsFalse(database.Find(id).IsObstacle);
            Assert.IsNull(database.Find(id).Prefab.GetComponent<NavMeshObstacle>());
        }

        [Test]
        public void Trees_DropLeavesThatLandOnTheGround()
        {
            var tree = database.Find("tree").Prefab;
            var leaves = tree.GetComponentInChildren<ParticleSystem>(true);
            Assert.IsNotNull(leaves, "Falling leaves missing");
            Assert.AreEqual(ParticleSystemSimulationSpace.World, leaves.main.simulationSpace,
                "Leaves already falling must not follow a dragged tree");
            Assert.LessOrEqual(leaves.main.maxParticles, 10, "Keep it cheap");
            Assert.IsTrue(leaves.collision.enabled);
            var ground = leaves.collision.GetPlane(0);
            Assert.IsNotNull(ground, "Leaves need a ground plane to land on");
            Assert.IsTrue(ground.IsChildOf(tree.transform));
            Assert.IsNotNull(leaves.GetComponent<ParticleSystemRenderer>().sharedMaterial);
            Assert.IsNull(database.Find("rock").Prefab.GetComponentInChildren<ParticleSystem>(true), "Only trees drop leaves");
        }

        [TestCase("Assets/LivingDiorama/Settings/PC_Renderer.asset")]
        [TestCase("Assets/LivingDiorama/Settings/Mobile_Renderer.asset")]
        public void Renderers_HaveTiltShiftWithMaterial(string path)
        {
            var renderer = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.Universal.ScriptableRendererData>(path);
            Assert.IsNotNull(renderer, path);
            var tiltShift = renderer.rendererFeatures.Find(f => f is LivingDiorama.Rendering.TiltShiftFeature) as LivingDiorama.Rendering.TiltShiftFeature;
            Assert.IsNotNull(tiltShift, "Tilt-shift pass missing");
            Assert.IsNotNull(tiltShift.Configuration.material);
            Assert.AreEqual(1, renderer.rendererFeatures.FindAll(f => f is LivingDiorama.Rendering.TiltShiftFeature).Count,
                "Re-running the bootstrap must not add duplicates");
        }

        [Test]
        public void AtmosphereProfile_HasBloomVignetteAndColour()
        {
            var profile = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.VolumeProfile>(AtmosphereBuilder.ProfilePath);
            Assert.IsNotNull(profile);
            Assert.IsTrue(profile.Has<UnityEngine.Rendering.Universal.Bloom>());
            Assert.IsTrue(profile.Has<UnityEngine.Rendering.Universal.Vignette>());
            Assert.IsTrue(profile.Has<UnityEngine.Rendering.Universal.ColorAdjustments>());
        }

        [Test]
        public void SpriteMeshCache_FacesCameraAndMatchesSpriteBounds()
        {
            var sprite = database.Find("rock").Icon;
            var mesh = SpriteMeshCache.Get(sprite);
            Assert.AreSame(mesh, SpriteMeshCache.Get(sprite), "Meshes should be cached per sprite");
            Assert.AreEqual(sprite.bounds.size.x, mesh.bounds.size.x, 0.001f);
            foreach (var normal in mesh.normals) Assert.AreEqual(Vector3.back, normal);
        }
    }
}

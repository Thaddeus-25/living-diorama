using LivingDiorama.Data;
using LivingDiorama.EditorTools;
using LivingDiorama.Entities;
using LivingDiorama.SceneEditing;
using LivingDiorama.World;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

namespace LivingDiorama.Tests
{
    public class PondTests
    {
        private GameObject pondObject;
        private WaterArea water;

        [SetUp]
        public void CreatePond()
        {
            pondObject = new GameObject("TestPond");
            pondObject.transform.position = new Vector3(2f, 0f, 1f);
            water = pondObject.AddComponent<WaterArea>();
            water.Configure(new Vector2(2f, 1f));
        }

        [TearDown]
        public void DestroyPond() => Object.DestroyImmediate(pondObject);

        [Test]
        public void Contains_IsAnEllipse_WithOptionalMargin()
        {
            Assert.IsTrue(water.Contains(new Vector3(2f, 0f, 1f)), "Centre");
            Assert.IsTrue(water.Contains(new Vector3(3.9f, 0f, 1f)), "Near the end of the long axis");
            Assert.IsFalse(water.Contains(new Vector3(2f, 0f, 2.1f)), "Past the short axis");
            Assert.IsFalse(water.Contains(new Vector3(3.9f, 0f, 1f), margin: 0.3f), "Too close to the edge with a margin");
        }

        [Test]
        public void ClosestPointInside_PullsOutsidePointsIn_LeavesInsidePointsAlone()
        {
            var inside = new Vector3(2.5f, 0f, 1.2f);
            Assert.AreEqual(inside, water.ClosestPointInside(inside));

            var pulled = water.ClosestPointInside(new Vector3(10f, 0f, 1f), margin: 0.2f);
            Assert.IsTrue(water.Contains(pulled, margin: 0.15f), $"{pulled} should be inside");
            Assert.Greater(pulled.x, 3f, "Pulled in towards the nearest side");
        }

        [Test]
        public void RandomPointsInside_AreAllInside()
        {
            var random = new System.Random(4);
            for (int i = 0; i < 500; i++)
                Assert.IsTrue(water.Contains(water.RandomPointInside(random, margin: 0.3f), margin: 0.29f));
        }

        [Test]
        public void ScalingThePond_ScalesTheWater()
        {
            pondObject.transform.localScale = Vector3.one * 2f;
            Assert.AreEqual(new Vector2(4f, 2f), water.Radii);
        }

        [Test]
        public void GeneratedPrefabs_PondIsFlatWater_FishSwimsWithoutNavMesh()
        {
            var database = AssetDatabase.LoadAssetAtPath<ObjectDatabase>(ProjectBootstrap.DatabasePath);
            var pond = database.Find("pond");
            Assert.AreEqual(ObjectCategory.Environment, pond.Category);
            Assert.IsNotNull(pond.Prefab.GetComponent<WaterArea>());
            Assert.IsNotNull(pond.Prefab.GetComponent<NavMeshObstacle>(), "Creatures walk around ponds");
            Assert.IsNull(pond.Prefab.GetComponentInChildren<Billboard>(true), "A pond lies flat");

            var fish = (EntityDefinition)database.Find("fish");
            Assert.AreEqual(Habitat.Water, fish.Habitat);
            Assert.IsNotNull(fish.Prefab.GetComponent<FishSwimmer>());
            Assert.IsNull(fish.Prefab.GetComponent<NavMeshAgent>(), "Fish do not walk on the NavMesh");
            Assert.IsNull(fish.Prefab.GetComponent<LivingEntity>());
            Assert.IsNull(fish.Prefab.GetComponentInChildren<Billboard>(true));
        }

        [Test]
        public void CanPlaceAt_GroundThingsAnywhere_FishOnlyInWater()
        {
            var database = AssetDatabase.LoadAssetAtPath<ObjectDatabase>(ProjectBootstrap.DatabasePath);
            Assert.IsTrue(SceneEditorController.CanPlaceAt(database.Find("tree"), new Vector3(-5f, 0f, -5f)));
            Assert.IsTrue(SceneEditorController.CanPlaceAt(database.Find("dog"), new Vector3(-5f, 0f, -5f)));
            // No water is registered in this edit-mode test (the test pond is never enabled), so fish fit nowhere.
            Assert.IsFalse(SceneEditorController.CanPlaceAt(database.Find("fish"), new Vector3(2f, 0f, 1f)));
        }
    }
}

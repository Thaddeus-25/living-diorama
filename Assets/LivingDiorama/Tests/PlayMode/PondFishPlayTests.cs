using System.Collections;
using System.Collections.Generic;
using System.Linq;
using LivingDiorama.Core;
using LivingDiorama.Entities;
using LivingDiorama.SceneEditing;
using LivingDiorama.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace LivingDiorama.Tests
{
    public class PondFishPlayTests
    {
        private DioramaWorld world;
        private SceneEditorController editor;
        private PlacedObject pond;
        private WaterArea water;
        private List<FishSwimmer> fish;

        [UnitySetUp]
        public IEnumerator LoadGarden()
        {
            yield return SceneManager.LoadSceneAsync("Garden", LoadSceneMode.Single);
            yield return null;
            world = Object.FindAnyObjectByType<DioramaWorld>();
            editor = Object.FindAnyObjectByType<SceneEditorController>();
            pond = world.Objects.First(o => o.Definition.Id == "pond");
            water = pond.GetComponent<WaterArea>();
            fish = Object.FindObjectsByType<FishSwimmer>(FindObjectsSortMode.None).ToList();
            for (int i = 0; i < fish.Count; i++) fish[i].SetRandomSeed(50 + i);
        }

        [TearDown]
        public void ResetTime() => Time.timeScale = 1f;

        [UnityTest]
        public IEnumerator Fish_SwimAround_AndNeverLeaveTheWater()
        {
            Assert.AreEqual(3, fish.Count, "The garden starts with three fish");
            Assert.IsTrue(fish.All(f => f.Habitat == water), "Every fish belongs to the pond");
            var start = fish.Select(f => f.transform.position).ToList();
            float maxMoved = 0f;
            bool sawSwimming = false;

            Time.timeScale = 3f;
            for (float t = 0f; t < 4f; t += Time.unscaledDeltaTime)
            {
                yield return null;
                for (int i = 0; i < fish.Count; i++)
                {
                    Assert.IsTrue(water.Contains(fish[i].transform.position), $"Fish {i} left the water at {fish[i].transform.position}");
                    maxMoved = Mathf.Max(maxMoved, Vector3.Distance(start[i], fish[i].transform.position));
                    sawSwimming |= fish[i].IsSwimming;
                }
            }
            Assert.IsTrue(sawSwimming, "Some fish should swim");
            Assert.Greater(maxMoved, 0.3f, "Fish should get somewhere");
        }

        [UnityTest]
        public IEnumerator MovingThePond_CarriesItsFish()
        {
            Object.FindAnyObjectByType<AppModeController>().SetCreaturesPausedInEdit(true);
            yield return null;
            var before = fish.Select(f => f.transform.position).ToList();
            var offset = new Vector3(3f, 0f, 1f);

            world.Move(pond, pond.transform.position + offset);
            yield return null;

            for (int i = 0; i < fish.Count; i++)
            {
                Assert.Less(Vector3.Distance(before[i] + offset, fish[i].transform.position), 0.01f, $"Fish {i} did not move with the pond");
                Assert.IsTrue(water.Contains(fish[i].transform.position));
            }
        }

        [UnityTest]
        public IEnumerator FishCanOnlyBePlacedInWater()
        {
            var fishDefinition = world.Database.Find("fish");
            string rejection = null;
            editor.PlacementRejected += reason => rejection = reason;

            Assert.IsNull(editor.TryPlaceAt(fishDefinition, new Vector3(3f, 0f, 3f)), "Dry land");
            StringAssert.Contains("water", rejection);

            var placed = editor.TryPlaceAt(fishDefinition, water.transform.position);
            Assert.IsNotNull(placed, "Inside the pond is fine");
            editor.Select(null);
            yield return null;
            var swimmer = placed.GetComponent<FishSwimmer>();
            Assert.AreSame(water, swimmer.Habitat);
            Assert.IsTrue(swimmer.IsSimulating);
        }

        [UnityTest]
        public IEnumerator FishDraggedOntoLand_SlipsBackIntoThePondWhenReleased()
        {
            var swimmer = fish[0];
            editor.Select(swimmer.GetComponent<PlacedObject>());
            Assert.IsTrue(swimmer.IsHeld);

            var dryLand = new Vector3(4f, 0f, 3f);
            world.Move(swimmer.GetComponent<PlacedObject>(), dryLand);
            yield return null;
            Assert.Less(Vector3.Distance(swimmer.transform.position, dryLand), 0.01f, "Can be dragged freely while held");

            editor.Select(null);
            yield return null;
            Assert.IsTrue(water.Contains(swimmer.transform.position), "Back in the water after letting go");
            Assert.AreSame(water, swimmer.Habitat);
        }

        [UnityTest]
        public IEnumerator DeletingThePond_StopsTheFish_UndoBringsThemBack()
        {
            editor.Select(pond);
            editor.DeleteSelected();
            yield return null;
            yield return null;
            Assert.IsTrue(fish.All(f => !f.IsSimulating),
                "No water, no swimming. " + string.Join("; ", fish.Select(f =>
                    $"{f.name}: sim={f.IsSimulating} habitat={(f.Habitat == null ? "none" : f.Habitat.name)} held={f.IsHeld} pos={f.transform.position}")));

            editor.Undo();
            // Fish look for water again about once a second.
            for (float t = 0f; t < 2.5f && !fish.All(f => f.IsSimulating); t += Time.deltaTime) yield return null;
            Assert.IsTrue(fish.All(f => f.IsSimulating), "Swimming again once the pond is back");
            var restoredWater = world.Objects.First(o => o.Definition.Id == "pond").GetComponent<WaterArea>();
            Assert.IsTrue(fish.All(f => f.Habitat == restoredWater));
        }
    }
}

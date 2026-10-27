using System.Collections;
using System.Collections.Generic;
using System.Linq;
using LivingDiorama.Core;
using LivingDiorama.Data;
using LivingDiorama.Entities;
using LivingDiorama.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace LivingDiorama.Tests
{
    /// <summary>Runs the real Garden scene's human and cat in Preview mode.</summary>
    public class LivingEntityPlayTests
    {
        private AppModeController modes;
        private DioramaWorld world;
        private List<LivingEntity> creatures;

        [UnitySetUp]
        public IEnumerator LoadGarden()
        {
            yield return SceneManager.LoadSceneAsync("Garden", LoadSceneMode.Single);
            yield return null;
            modes = Object.FindAnyObjectByType<AppModeController>();
            world = Object.FindAnyObjectByType<DioramaWorld>();
            creatures = Object.FindObjectsByType<LivingEntity>(FindObjectsSortMode.None).ToList();
            for (int i = 0; i < creatures.Count; i++) creatures[i].SetRandomSeed(1234 + i);
        }

        [TearDown]
        public void ResetTime() => Time.timeScale = 1f;

        [UnityTest]
        public IEnumerator EditMode_CreaturesKeepLivingByDefault()
        {
            Assert.AreEqual(2, creatures.Count, "Garden should start with a human and a cat");
            Assert.AreEqual(AppMode.Edit, modes.Mode);
            Assert.IsFalse(modes.CreaturesPausedInEdit);
            yield return null;
            foreach (var creature in creatures)
            {
                Assert.IsTrue(creature.IsSimulating, $"{creature.name} should live while editing");
                Assert.IsTrue(creature.Agent.isOnNavMesh);
            }
        }

        [UnityTest]
        public IEnumerator PauseToggle_FreezesCreaturesInEdit_ButPreviewStillRunsThem()
        {
            modes.SetCreaturesPausedInEdit(true);
            yield return null;
            var start = creatures.Select(c => c.transform.position).ToList();
            for (int i = 0; i < 30; i++) yield return null;
            for (int i = 0; i < creatures.Count; i++)
            {
                Assert.IsFalse(creatures[i].IsSimulating);
                Assert.IsFalse(creatures[i].Agent.enabled, "Agent must be off so the creature can be dragged");
                Assert.AreEqual(start[i], creatures[i].transform.position);
            }

            modes.SetMode(AppMode.Preview);
            yield return null;
            Assert.IsTrue(creatures.All(c => c.IsSimulating), "Preview always runs creatures");

            modes.SetMode(AppMode.Edit);
            yield return null;
            Assert.IsTrue(creatures.All(c => !c.IsSimulating), "Back in Edit the pause applies again");

            modes.SetCreaturesPausedInEdit(false);
            yield return null;
            Assert.IsTrue(creatures.All(c => c.IsSimulating));
        }

        [UnityTest]
        public IEnumerator SelectedCreature_IsHeld_AndCarriesOnWhenDeselected()
        {
            var editor = Object.FindAnyObjectByType<LivingDiorama.SceneEditing.SceneEditorController>();
            var cat = creatures.First(c => c.Definition.Id == "cat");
            yield return null;
            Assert.IsTrue(cat.IsSimulating);

            editor.Select(cat.Placed);
            Assert.IsTrue(cat.IsHeld);
            Assert.IsFalse(cat.IsSimulating);
            Assert.IsFalse(cat.Agent.enabled, "A held creature can be dragged");
            var heldAt = cat.transform.position;
            for (int i = 0; i < 20; i++) yield return null;
            Assert.AreEqual(heldAt, cat.transform.position, "A held creature stays put");

            // Dragging it somewhere new, then letting go: it lives on from there.
            world.Move(cat.Placed, new Vector3(-6f, 0f, -4f));
            editor.Select(null);
            yield return null;
            Assert.IsFalse(cat.IsHeld);
            Assert.IsTrue(cat.IsSimulating);
            Assert.Less(Vector3.Distance(cat.transform.position, new Vector3(-6f, 0f, -4f)), 0.3f);
        }

        [UnityTest]
        public IEnumerator UndoMovingAWalkingCreature_RelocatesItsAgent()
        {
            var editor = Object.FindAnyObjectByType<LivingDiorama.SceneEditing.SceneEditorController>();
            var human = creatures.First(c => c.Definition.Id == "human");
            var before = world.Capture(human.Placed);
            var after = before.Clone();
            after.position = new Vector3(5f, 0f, -4.5f);
            editor.Commands.Execute(new LivingDiorama.SceneEditing.Commands.ModifyObjectCommand(world, before, after));
            yield return null;

            Assert.IsTrue(human.IsSimulating);
            Assert.Less(Vector3.Distance(human.transform.position, after.position), 0.3f, "Moved, not snapped back by its agent");
            Assert.Less(Vector3.Distance(human.Agent.nextPosition, human.transform.position), 0.05f, "Agent moved with it");
        }

        [UnityTest]
        public IEnumerator PlacingACreatureWhileLiving_StartsWhereItWasPlaced()
        {
            var editor = Object.FindAnyObjectByType<LivingDiorama.SceneEditing.SceneEditorController>();
            var spot = new Vector3(-7f, 0f, -4.5f);
            var placed = editor.PlaceAt(world.Database.Find("cat"), spot).GetComponent<LivingEntity>();
            editor.Select(null);
            yield return null;
            Assert.IsTrue(placed.IsSimulating);
            Assert.Less(Vector3.Distance(placed.transform.position, spot), 0.3f, "Not at the prefab's origin");
        }

        [UnityTest]
        public IEnumerator Preview_ForcedWander_MovesAroundAndStopsWhenPaused()
        {
            modes.SetMode(AppMode.Preview);
            yield return null;
            var cat = creatures.First(c => c.Definition.Id == "cat");
            Assert.IsTrue(cat.IsSimulating);
            Assert.IsTrue(cat.Agent.isOnNavMesh);

            var start = cat.transform.position;
            cat.StartBehaviour(new BehaviourWeight(EntityBehaviour.Wander, 1f, new Vector2(10f, 10f)));
            Assert.AreEqual("Wander", cat.CurrentState.Name);
            Assert.AreEqual(SpriteAnimationSet.Walk, cat.Animator.CurrentClipName);

            float maxDistance = 0f;
            for (float t = 0f; t < 3f; t += Time.deltaTime)
            {
                maxDistance = Mathf.Max(maxDistance, Vector3.Distance(start, cat.transform.position));
                yield return null;
            }
            Assert.Greater(maxDistance, 0.5f, "The cat should have walked somewhere");

            modes.SetMode(AppMode.Edit);
            modes.SetCreaturesPausedInEdit(true);
            yield return null;
            var frozenAt = cat.transform.position;
            for (int i = 0; i < 20; i++) yield return null;
            Assert.IsFalse(cat.IsSimulating);
            Assert.AreEqual(frozenAt, cat.transform.position, "Paused creatures freeze in Edit mode");
        }

        [UnityTest]
        public IEnumerator Preview_LongRun_StaysInGardenAndOutOfObstacles()
        {
            modes.SetMode(AppMode.Preview);
            Time.timeScale = 4f; // ~40 simulated seconds
            var statesSeen = new HashSet<string>();
            var obstacles = Object.FindObjectsByType<NavMeshObstacle>(FindObjectsSortMode.None);
            float halfWidth = world.GardenSize.x * 0.5f, halfDepth = world.GardenSize.y * 0.5f;

            for (float t = 0f; t < 10f; t += Time.unscaledDeltaTime)
            {
                foreach (var creature in creatures)
                {
                    var p = creature.transform.position;
                    Assert.LessOrEqual(Mathf.Abs(p.x), halfWidth, $"{creature.name} left the garden: {p}");
                    Assert.LessOrEqual(Mathf.Abs(p.z), halfDepth, $"{creature.name} left the garden: {p}");
                    // Using an interaction point (sitting on the bench) puts a creature on the object on purpose.
                    if (creature.CurrentState is not InteractState)
                        foreach (var obstacle in obstacles)
                            Assert.IsFalse(IsInsideFootprint(obstacle, p, creature.Agent.radius * 0.5f),
                                $"{creature.name} walked into {obstacle.name} at {p}");
                    if (creature.CurrentState != null) statesSeen.Add($"{creature.Definition.Id}:{creature.CurrentState.Name}");
                }
                yield return null;
            }

            Debug.Log($"[LivingEntityPlayTests] states seen: {string.Join(", ", statesSeen.OrderBy(s => s))}");
            Assert.GreaterOrEqual(statesSeen.Count(s => s.StartsWith("cat:")), 3, "The cat should vary its behaviour");
            Assert.GreaterOrEqual(statesSeen.Count(s => s.StartsWith("human:")), 2, "The human should vary its behaviour");
        }

        [UnityTest]
        public IEnumerator WalkingCreature_DoesNotPushRestingCreature([Values("cat", "human")] string resterId)
        {
            modes.SetMode(AppMode.Preview);
            yield return null;
            var rester = creatures.First(c => c.Definition.Id == resterId);
            var walker = creatures.First(c => c != rester);

            // Open ground in front of the bench, away from the rocks.
            var restSpot = new Vector3(0f, 0f, -4f);
            rester.Agent.Warp(restSpot);
            walker.Agent.Warp(new Vector3(-4f, 0f, -4f));
            rester.StartBehaviour(new BehaviourWeight(EntityBehaviour.Rest, 1f, new Vector2(60f, 60f), "sleep"));
            // Keep the walker's brain idle and steer it by hand straight through the resting creature.
            walker.StartBehaviour(new BehaviourWeight(EntityBehaviour.Idle, 1f, new Vector2(60f, 60f)));
            yield return null;
            // Worst case: the walker starts out as the more important agent (random priorities used to allow this).
            walker.Agent.avoidancePriority = 10;
            rester.Agent.avoidancePriority = 90;
            restSpot = rester.transform.position;
            Assert.IsTrue(walker.Agent.SetDestination(new Vector3(4f, 0f, -4f)));

            float maxPush = 0f;
            for (float t = 0f; t < 8f && (walker.Agent.pathPending || walker.Agent.remainingDistance > 0.2f); t += Time.deltaTime)
            {
                maxPush = Mathf.Max(maxPush, Vector3.Distance(restSpot, rester.transform.position));
                yield return null;
            }

            Assert.Greater(walker.transform.position.x, 3f, "The walker should get past the resting creature");
            Assert.Less(maxPush, 0.05f, $"The resting {resterId} was pushed {maxPush:F2} m");
        }

        /// <summary>True when <paramref name="point"/> is inside the obstacle's box, shrunk by a tolerance.</summary>
        private static bool IsInsideFootprint(NavMeshObstacle obstacle, Vector3 point, float tolerance)
        {
            var local = obstacle.transform.InverseTransformPoint(point) - obstacle.center;
            var half = obstacle.size * 0.5f;
            return Mathf.Abs(local.x) < half.x - tolerance && Mathf.Abs(local.z) < half.z - tolerance;
        }
    }
}

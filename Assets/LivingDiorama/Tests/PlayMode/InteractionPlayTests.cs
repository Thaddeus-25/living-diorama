using System;
using System.Collections;
using System.Linq;
using LivingDiorama.Core;
using LivingDiorama.Data;
using LivingDiorama.Entities;
using LivingDiorama.Interactions;
using LivingDiorama.SceneEditing;
using LivingDiorama.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace LivingDiorama.Tests
{
    /// <summary>Bench sitting (and the cat's nap spot) in the real Garden scene.</summary>
    public class InteractionPlayTests
    {
        private AppModeController modes;
        private DioramaWorld world;
        private SceneEditorController editor;
        private LivingEntity human;
        private LivingEntity cat;
        private PlacedObject bench;

        private static BehaviourWeight Sit(float seconds) =>
            BehaviourWeight.Interact(InteractionType.Sit, 1f, new Vector2(seconds, seconds));

        [UnitySetUp]
        public IEnumerator LoadGarden()
        {
            yield return SceneManager.LoadSceneAsync("Garden", LoadSceneMode.Single);
            yield return null;
            modes = Object.FindAnyObjectByType<AppModeController>();
            world = Object.FindAnyObjectByType<DioramaWorld>();
            editor = Object.FindAnyObjectByType<SceneEditorController>();
            var creatures = Object.FindObjectsByType<LivingEntity>(FindObjectsSortMode.None);
            human = creatures.First(c => c.Definition.Id == "human");
            cat = creatures.First(c => c.Definition.Id == "cat");
            bench = world.Objects.First(o => o.Definition.Id == "bench");
            human.SetRandomSeed(7);
            cat.SetRandomSeed(8);
            Time.timeScale = 2f;
        }

        [TearDown]
        public void ResetTime() => Time.timeScale = 1f;

        private static IEnumerator WaitFor(Func<bool> condition, float gameSeconds)
        {
            for (float t = 0f; t < gameSeconds && !condition(); t += Time.deltaTime) yield return null;
        }

        private static InteractState Using(LivingEntity creature) => creature.CurrentState as InteractState;

        [UnityTest]
        public IEnumerator Human_SitsOnBench_ThenGetsUpAndCarriesOn()
        {
            modes.SetMode(AppMode.Preview);
            yield return null;
            // Keep the cat out of the way.
            cat.StartBehaviour(new BehaviourWeight(EntityBehaviour.Rest, 1f, new Vector2(60f, 60f), "sleep"));
            human.StartBehaviour(Sit(2f));
            Assert.AreEqual("Seek Sit", human.CurrentState.Name, "There is a free bench, so the human should head for it");

            yield return WaitFor(() => Using(human)?.IsHolding == true, 15f);
            var sitting = Using(human);
            Assert.IsNotNull(sitting, $"Human never sat down (state: {human.CurrentState?.Name})");
            Assert.AreEqual("sit", human.Animator.CurrentClipName);
            Assert.Less(Vector3.Distance(human.transform.position, sitting.Point.Position), 0.05f, "Should be on the seat");
            Assert.IsTrue(sitting.Point.transform.IsChildOf(bench.transform));
            Assert.IsTrue(sitting.Point.IsReservedBy(human));
            var seat = sitting.Point;

            yield return WaitFor(() => Using(human) == null, 6f);
            Assert.IsNull(Using(human), "Human should get up after the sit duration");
            // The human may decide to sit again right away, which legitimately reserves a seat anew.
            bool seekingSameSeatAgain = human.CurrentState is SeekInteractionState { Target: var target } && target == seat;
            Assert.IsTrue(!seat.IsReservedBy(human) || seekingSameSeatAgain,
                $"Seat must be released (state now: {human.CurrentState?.Name})");
            Assert.IsTrue(human.Agent.updatePosition);
            Assert.IsTrue(human.Agent.isOnNavMesh, "Back on walkable ground");
            Assert.Greater(Vector3.Distance(human.transform.position, seat.Position), 0.4f, "Stepped off the bench");

            // Life goes on: the brain keeps choosing new states.
            var stateAfter = human.CurrentState;
            yield return WaitFor(() => human.CurrentState != stateAfter, 10f);
            Assert.AreNotSame(stateAfter, human.CurrentState);
        }

        [UnityTest]
        public IEnumerator BenchRemovedWhileWalkingToIt_HumanGivesUpCleanly()
        {
            modes.SetMode(AppMode.Preview);
            yield return null;
            human.StartBehaviour(Sit(5f));
            var seek = (SeekInteractionState)human.CurrentState;
            var seat = seek.Target;
            Assert.IsNotNull(seat);
            yield return null;

            world.Remove(bench.InstanceId);
            yield return null;
            yield return null;

            Assert.IsFalse(human.CurrentState is SeekInteractionState s && s.Target == seat, "Should stop heading for a removed bench");
            Assert.IsNull(Using(human));
            Assert.IsTrue(human.IsSimulating);
            Assert.IsTrue(human.Agent.isOnNavMesh);
        }

        [UnityTest]
        public IEnumerator TwoHumans_TakeDifferentSeats()
        {
            var second = editor.PlaceAt(world.Database.Find("human"), new Vector3(1.5f, 0f, -1f)).GetComponent<LivingEntity>();
            second.SetRandomSeed(9);
            modes.SetMode(AppMode.Preview);
            yield return null;
            cat.StartBehaviour(new BehaviourWeight(EntityBehaviour.Rest, 1f, new Vector2(60f, 60f), "sleep"));
            human.StartBehaviour(Sit(10f));
            second.StartBehaviour(Sit(10f));

            yield return WaitFor(() => Using(human)?.IsHolding == true && Using(second)?.IsHolding == true, 20f);
            Assert.IsNotNull(Using(human), "First human didn't sit");
            Assert.IsNotNull(Using(second), "Second human didn't sit");
            Assert.AreNotSame(Using(human).Point, Using(second).Point, "Each seat holds one human");
        }

        [UnityTest]
        public IEnumerator PausingCreaturesWhileSitting_StepsOffAndReleasesSeat()
        {
            modes.SetMode(AppMode.Preview);
            yield return null;
            human.StartBehaviour(Sit(30f));
            yield return WaitFor(() => Using(human)?.IsHolding == true, 15f);
            var seat = Using(human)?.Point;
            Assert.IsNotNull(seat, "Human didn't sit");

            modes.SetMode(AppMode.Edit);
            yield return null;
            Assert.IsNotNull(Using(human), "Edit mode alone doesn't interrupt a living creature");

            modes.SetCreaturesPausedInEdit(true);
            yield return null;
            Assert.IsFalse(seat.IsReservedBy(human));
            Assert.IsFalse(human.IsSimulating);
            Assert.IsFalse(human.Agent.enabled);
            Assert.Greater(Vector3.Distance(human.transform.position, seat.Position), 0.4f,
                "Pausing should leave the human standing in front of the bench, not inside it");

            // And the seat is usable again next time.
            modes.SetCreaturesPausedInEdit(false);
            yield return null;
            human.StartBehaviour(Sit(2f));
            Assert.AreEqual("Seek Sit", human.CurrentState.Name);
        }

        [UnityTest]
        public IEnumerator SelectingASittingHuman_GetsThemUp()
        {
            human.StartBehaviour(Sit(30f));
            yield return WaitFor(() => Using(human)?.IsHolding == true, 15f);
            var seat = Using(human)?.Point;
            Assert.IsNotNull(seat, "Human didn't sit");

            editor.Select(human.Placed);
            Assert.IsTrue(human.IsHeld);
            Assert.IsFalse(seat.IsReservedBy(human));
            Assert.Greater(Vector3.Distance(human.transform.position, seat.Position), 0.4f, "Stepped off to be dragged");
        }

        [UnityTest]
        public IEnumerator DraggingTheBench_CarriesTheSitterAlong()
        {
            cat.StartBehaviour(new BehaviourWeight(EntityBehaviour.Rest, 1f, new Vector2(60f, 60f), "sleep"));
            human.StartBehaviour(Sit(30f));
            yield return WaitFor(() => Using(human)?.IsHolding == true, 15f);
            var seat = Using(human)?.Point;
            Assert.IsNotNull(seat, "Human didn't sit");

            editor.Select(bench);
            world.Move(bench, bench.transform.position + new Vector3(-3f, 0f, -1f));
            yield return null;
            yield return null;

            Assert.IsNotNull(Using(human), "Still sitting");
            Assert.Less(Vector3.Distance(human.transform.position, seat.Position), 0.05f, "On the seat's new position");
        }

        [UnityTest]
        public IEnumerator SavingWhileSitting_RecordsTheSpotInFrontOfTheBench()
        {
            human.StartBehaviour(Sit(30f));
            yield return WaitFor(() => Using(human)?.IsHolding == true, 15f);
            Assert.IsNotNull(Using(human), "Human didn't sit");

            var saved = world.Capture(human.Placed).position;
            var benchObstacle = bench.GetComponent<NavMeshObstacle>();
            var local = bench.transform.InverseTransformPoint(saved) - benchObstacle.center;
            bool insideBench = Mathf.Abs(local.x) < benchObstacle.size.x * 0.5f && Mathf.Abs(local.z) < benchObstacle.size.z * 0.5f;
            Assert.IsFalse(insideBench, $"Saved position {saved} is inside the bench");
            Assert.IsNotNull(Using(human), "Capturing doesn't disturb the sitter");
        }

        [UnityTest]
        public IEnumerator Cat_NapsAtATree()
        {
            modes.SetMode(AppMode.Preview);
            yield return null;
            human.StartBehaviour(new BehaviourWeight(EntityBehaviour.Idle, 1f, new Vector2(60f, 60f)));
            cat.StartBehaviour(BehaviourWeight.Interact(InteractionType.Sleep, 1f, new Vector2(5f, 5f)));

            yield return WaitFor(() => Using(cat)?.IsHolding == true, 20f);
            var nap = Using(cat);
            Assert.IsNotNull(nap, $"Cat never settled (state: {cat.CurrentState?.Name})");
            Assert.AreEqual("sleep", cat.Animator.CurrentClipName);
            // Trees and the campfire both offer nap spots; whichever is nearest is fine.
            CollectionAssert.Contains(new[] { "tree", "campfire" }, nap.Point.GetComponentInParent<PlacedObject>().Definition.Id);
        }

        [UnityTest]
        public IEnumerator Human_WarmsUpByTheCampfire_FacingIt()
        {
            modes.SetMode(AppMode.Preview);
            yield return null;
            cat.StartBehaviour(new BehaviourWeight(EntityBehaviour.Rest, 1f, new Vector2(60f, 60f), "sleep"));
            human.StartBehaviour(BehaviourWeight.Interact(InteractionType.Warm, 1f, new Vector2(5f, 5f)));
            Assert.AreEqual("Seek Warm", human.CurrentState.Name);

            yield return WaitFor(() => Using(human)?.IsHolding == true, 20f);
            var warming = Using(human);
            Assert.IsNotNull(warming, $"Human never reached the fire (state: {human.CurrentState?.Name})");
            var fire = warming.Point.GetComponentInParent<PlacedObject>();
            Assert.AreEqual("campfire", fire.Definition.Id);
            Assert.AreEqual("warm", human.Animator.CurrentClipName, "Hands held out to the fire");
            bool fireIsToTheLeft = fire.transform.position.x < human.transform.position.x;
            Assert.AreEqual(fireIsToTheLeft, human.Placed.FlipX, "Faces the fire");
        }

        [UnityTest]
        public IEnumerator NoFreeSeat_SeekFinishesImmediately()
        {
            modes.SetMode(AppMode.Preview);
            yield return null;
            // Fill both seats with a stand-in so nothing is free.
            var seats = InteractionRegistry.All.Where(p => p.Type == InteractionType.Sit).ToList();
            Assert.AreEqual(2, seats.Count);
            var standIn = editor.World.Spawn(new LivingDiorama.Persistence.PlacedObjectData
            {
                id = "stand-in", definitionId = "human", position = new Vector3(5f, 0f, -4f), scale = 1f
            }).GetComponent<LivingEntity>();
            foreach (var seat in seats) Assert.IsTrue(seat.TryReserve(standIn));

            human.StartBehaviour(Sit(5f));
            // Checked straight away: a frame later the human may already have picked something else
            // (e.g. warming up at the campfire), which is fine.
            var seek = (SeekInteractionState)human.CurrentState;
            Assert.IsNull(seek.Target, "No seat should be found");
            Assert.IsTrue(seek.IsFinished, "With every seat taken, the human gives up at once");
            yield return null;
        }
    }
}

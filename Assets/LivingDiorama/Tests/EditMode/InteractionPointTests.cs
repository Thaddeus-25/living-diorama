using System.Collections.Generic;
using LivingDiorama.Data;
using LivingDiorama.Entities;
using LivingDiorama.Interactions;
using NUnit.Framework;
using UnityEngine;

namespace LivingDiorama.Tests
{
    public class InteractionPointTests
    {
        private readonly List<GameObject> created = new();

        [TearDown]
        public void Cleanup()
        {
            foreach (var go in created) Object.DestroyImmediate(go);
            created.Clear();
        }

        private InteractionPoint Point(int capacity, params string[] entityIds)
        {
            var go = new GameObject("Point");
            created.Add(go);
            var point = go.AddComponent<InteractionPoint>();
            point.Configure(InteractionType.Sit, entityIds, capacity, Vector3.back, "sit", false);
            return point;
        }

        private LivingEntity Creature()
        {
            // RequireComponent adds PlacedObject and NavMeshAgent.
            var go = new GameObject("Creature");
            created.Add(go);
            return go.AddComponent<LivingEntity>();
        }

        [Test]
        public void Reserve_RespectsCapacityAndRelease()
        {
            var point = Point(capacity: 1);
            var first = Creature();
            var second = Creature();

            Assert.IsTrue(point.TryReserve(first));
            Assert.IsTrue(point.TryReserve(first), "Re-reserving your own spot is fine");
            Assert.IsFalse(point.TryReserve(second), "Full");

            point.Release(first);
            Assert.IsTrue(point.TryReserve(second));
            Assert.IsTrue(point.IsReservedBy(second));
        }

        [Test]
        public void Supports_FiltersByEntityId_EmptyMeansEveryone()
        {
            var humansOnly = Point(1, "human");
            Assert.IsTrue(humansOnly.Supports("human"));
            Assert.IsFalse(humansOnly.Supports("cat"));
            Assert.IsTrue(Point(1).Supports("cat"));
        }

        [Test]
        public void ApproachPosition_FollowsRotation()
        {
            var point = Point(1);
            point.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
            // Local back (0,0,-1) turned 90 degrees points along -X.
            Assert.That(Vector3.Distance(point.ApproachPosition, new Vector3(-1f, 0f, 0f)), Is.LessThan(1e-4f));
        }

        [Test]
        public void AddBehaviourIfMissing_AddsOnceAndTreatsNullAndEmptyAnimationAsEqual()
        {
            var definition = ScriptableObject.CreateInstance<EntityDefinition>();
            definition.ConfigureEntity(1f, 4f, 0.25f, null, new[]
            {
                new BehaviourWeight(EntityBehaviour.Idle, 1f, Vector2.one) { animation = "" },
            });

            Assert.IsFalse(definition.AddBehaviourIfMissing(new BehaviourWeight(EntityBehaviour.Idle, 5f, Vector2.one)));
            Assert.IsTrue(definition.AddBehaviourIfMissing(BehaviourWeight.Interact(InteractionType.Sit, 1f, Vector2.one)));
            Assert.IsFalse(definition.AddBehaviourIfMissing(BehaviourWeight.Interact(InteractionType.Sit, 9f, Vector2.one)));
            Assert.IsTrue(definition.AddBehaviourIfMissing(BehaviourWeight.Interact(InteractionType.Sleep, 1f, Vector2.one)));
            Assert.AreEqual(3, definition.Behaviours.Count);
            Object.DestroyImmediate(definition);
        }

        [Test]
        public void CreateState_InteractBecomesSeek()
        {
            var state = BehaviourPicker.CreateState(BehaviourWeight.Interact(InteractionType.Sit, 1f, new Vector2(5f, 5f)), new System.Random(0));
            Assert.IsInstanceOf<SeekInteractionState>(state);
            Assert.AreEqual("Seek Sit", state.Name);
        }
    }
}

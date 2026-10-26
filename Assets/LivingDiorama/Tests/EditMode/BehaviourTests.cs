using System.Collections.Generic;
using LivingDiorama.Data;
using LivingDiorama.Entities;
using NUnit.Framework;
using UnityEngine;

namespace LivingDiorama.Tests
{
    public class BehaviourTests
    {
        private sealed class RecordingState : IState<List<string>>
        {
            public RecordingState(string name) => Name = name;
            public string Name { get; }
            public bool IsFinished => false;
            public void Enter(List<string> log) => log.Add($"enter {Name}");
            public void Tick(List<string> log, float deltaTime) => log.Add($"tick {Name}");
            public void Exit(List<string> log) => log.Add($"exit {Name}");
        }

        [Test]
        public void StateMachine_CallsExitThenEnterAndTicksCurrentOnly()
        {
            var log = new List<string>();
            var machine = new StateMachine<List<string>>();
            machine.ChangeState(new RecordingState("a"), log);
            machine.Tick(log, 0.1f);
            machine.ChangeState(new RecordingState("b"), log);
            machine.Tick(log, 0.1f);
            machine.Stop(log);

            CollectionAssert.AreEqual(
                new[] { "enter a", "tick a", "exit a", "enter b", "tick b", "exit b" }, log);
            Assert.IsNull(machine.Current);
        }

        [Test]
        public void Pick_NeverReturnsZeroWeightEntries()
        {
            var table = new List<BehaviourWeight>
            {
                new(EntityBehaviour.Idle, 0f, Vector2.one),
                new(EntityBehaviour.Wander, 1f, Vector2.one),
            };
            var random = new System.Random(1);
            for (int i = 0; i < 200; i++)
                Assert.AreEqual(EntityBehaviour.Wander, BehaviourPicker.Pick(table, random).behaviour);
        }

        [Test]
        public void Pick_FollowsWeightsRoughly()
        {
            var table = new List<BehaviourWeight>
            {
                new(EntityBehaviour.Idle, 1f, Vector2.one),
                new(EntityBehaviour.Wander, 3f, Vector2.one),
            };
            var random = new System.Random(42);
            int wanders = 0;
            const int rolls = 4000;
            for (int i = 0; i < rolls; i++)
                if (BehaviourPicker.Pick(table, random).behaviour == EntityBehaviour.Wander) wanders++;
            Assert.AreEqual(0.75, wanders / (double)rolls, 0.03);
        }

        [Test]
        public void Pick_EmptyOrNullTable_ReturnsNull()
        {
            var random = new System.Random(0);
            Assert.IsNull(BehaviourPicker.Pick(null, random));
            Assert.IsNull(BehaviourPicker.Pick(new List<BehaviourWeight>(), random));
        }

        [Test]
        public void CreateState_MapsBehavioursAndUsesRestAnimation()
        {
            var random = new System.Random(3);
            Assert.IsInstanceOf<WanderState>(BehaviourPicker.CreateState(new(EntityBehaviour.Wander, 1f, new Vector2(1, 2)), random));
            Assert.IsInstanceOf<LookAroundState>(BehaviourPicker.CreateState(new(EntityBehaviour.LookAround, 1f, new Vector2(1, 2)), random));

            var rest = BehaviourPicker.CreateState(new(EntityBehaviour.Rest, 1f, new Vector2(1, 2), "sleep"), random);
            Assert.IsInstanceOf<StayState>(rest);
            Assert.AreEqual("sleep", ((StayState)rest).Animation);

            // A null entry (empty table) still gives the creature something sensible to do.
            Assert.IsInstanceOf<StayState>(BehaviourPicker.CreateState(null, random));
        }

        [TestCase(0f, 0)]
        [TestCase(0.26f, 2)]
        [TestCase(0.51f, 0)] // 4 frames at 8 fps loop every 0.5 s
        public void FrameAt_Loops(float time, int expected) =>
            Assert.AreEqual(expected, SpriteAnimationSet.FrameAt(time, 8f, 4, loop: true));

        [Test]
        public void FrameAt_NonLoopingHoldsLastFrame() =>
            Assert.AreEqual(3, SpriteAnimationSet.FrameAt(10f, 8f, 4, loop: false));

        [Test]
        public void AnimationSet_FallsBackToIdleForUnknownClips()
        {
            var set = ScriptableObject.CreateInstance<SpriteAnimationSet>();
            var frame = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), Vector2.zero);
            set.SetClips(new[]
            {
                new SpriteAnimationSet.Clip { name = "walk", frames = new[] { frame } },
                new SpriteAnimationSet.Clip { name = "idle", frames = new[] { frame } },
            });
            Assert.AreEqual("walk", set.Find("walk").name);
            Assert.AreEqual("idle", set.Find("dance").name);
            Object.DestroyImmediate(set);
        }
    }
}

using System.Collections;
using System.Collections.Generic;
using System.Linq;
using LivingDiorama.Core;
using LivingDiorama.Data;
using LivingDiorama.Entities;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace LivingDiorama.Tests
{
    public class AnimationPlayTests
    {
        [UnityTest]
        public IEnumerator WalkingCreature_BobsByOnePixel_StandingOneDoesNot()
        {
            yield return SceneManager.LoadSceneAsync("Garden", LoadSceneMode.Single);
            yield return null;
            Object.FindAnyObjectByType<AppModeController>().SetMode(AppMode.Preview);
            yield return null;

            var cat = Object.FindObjectsByType<LivingEntity>(FindObjectsSortMode.None).First(c => c.Definition.Id == "cat");
            var bob = cat.GetComponentInChildren<WalkBob>();
            Assert.IsNotNull(bob, "Creature visuals should have a WalkBob");

            cat.StartBehaviour(new BehaviourWeight(EntityBehaviour.Wander, 1f, new Vector2(10f, 10f)));
            var lifts = new HashSet<float>();
            for (float t = 0f; t < 1.5f && cat.CurrentState is WanderState; t += Time.deltaTime)
            {
                yield return null;
                lifts.Add(Mathf.Round(bob.CurrentLift * 16f));
            }
            CollectionAssert.AreEquivalent(new[] { 0f, 1f }, lifts, "Walking alternates between 0 and 1 pixel");

            cat.StartBehaviour(new BehaviourWeight(EntityBehaviour.Idle, 1f, new Vector2(5f, 5f)));
            for (int i = 0; i < 5; i++) yield return null;
            Assert.AreEqual(0f, bob.CurrentLift, "No bob while standing");
            Assert.Less(Vector3.Distance(bob.transform.position, cat.transform.position), 1e-4f, "Visual back on the creature's feet");
        }

        [Test]
        public void SlideEasing_StartsAndEndsGently()
        {
            Assert.AreEqual(0f, InteractState.Ease(0f));
            Assert.AreEqual(1f, InteractState.Ease(1f));
            Assert.AreEqual(0.5f, InteractState.Ease(0.5f), 1e-5f);
            Assert.Less(InteractState.Ease(0.1f), 0.1f, "Slower than linear at the start");
            Assert.Greater(InteractState.Ease(0.9f), 0.9f, "Slower than linear at the end");
            Assert.AreEqual(1f, InteractState.Ease(3f), "Clamped");
        }
    }
}

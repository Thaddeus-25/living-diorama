using System.Collections;
using System.Linq;
using LivingDiorama.Data;
using LivingDiorama.Entities;
using LivingDiorama.SceneEditing;
using LivingDiorama.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace LivingDiorama.Tests
{
    /// <summary>Dragging an object must pass through whatever it touches, never shove it aside.</summary>
    public class DragThroughPlayTests
    {
        private DioramaWorld world;
        private SceneEditorController editor;
        private LivingEntity cat;

        [UnitySetUp]
        public IEnumerator LoadGarden()
        {
            yield return SceneManager.LoadSceneAsync("Garden", LoadSceneMode.Single);
            yield return null;
            world = Object.FindAnyObjectByType<DioramaWorld>();
            editor = Object.FindAnyObjectByType<SceneEditorController>();
            var creatures = Object.FindObjectsByType<LivingEntity>(FindObjectsSortMode.None);
            cat = creatures.First(c => c.Definition.Id == "cat");
            // Everyone naps where they are, so only the drag can move them.
            foreach (var creature in creatures)
                creature.StartBehaviour(new BehaviourWeight(EntityBehaviour.Rest, 1f, new Vector2(60f, 60f), "sleep"));
        }

        [UnityTest]
        public IEnumerator DraggingAPropThroughASleepingCat_DoesNotPushIt([Values("tree", "bench", "pond")] string propId)
        {
            // Put the cat on open ground and start the drag well to its left.
            cat.Agent.Warp(new Vector3(4f, 0f, -4.3f));
            yield return null;
            var catSpot = cat.transform.position;
            var prop = world.Objects.First(o => o.Definition.Id == propId);
            world.Move(prop, new Vector3(0.5f, 0f, -4.3f));
            yield return null;

            editor.BeginDrag(prop);
            float maxPush = 0f;
            // Sweep the prop right through the cat and out the other side, over about a second.
            for (float x = 0.5f; x <= 8f; x += 0.12f)
            {
                editor.DragTo(new Vector3(x, 0f, -4.3f));
                yield return null;
                maxPush = Mathf.Max(maxPush, Vector3.Distance(catSpot, cat.transform.position));
            }
            editor.EndDrag();
            yield return null;

            Assert.Less(maxPush, 0.05f, $"Dragging the {propId} pushed the cat {maxPush:F2} m");
        }
    }
}

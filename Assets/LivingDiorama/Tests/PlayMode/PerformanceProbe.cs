using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using LivingDiorama.Core;
using LivingDiorama.Persistence;
using LivingDiorama.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace LivingDiorama.Tests
{
    /// <summary>
    /// Not a pass/fail test: fills the garden (40 creatures, 60 props), runs it and logs frame time
    /// and per-frame allocations. Batchmode has no display, so the numbers are mostly CPU cost;
    /// compare runs against each other rather than reading them as real frame rates.
    /// Run with: -runTests -testPlatform PlayMode -testFilter PerformanceProbe
    /// </summary>
    [Explicit("Benchmark; run on demand.")]
    public class PerformanceProbe
    {
        private const int Creatures = 40;
        private const int Props = 60;
        private const int WarmupFrames = 120;
        private const int MeasuredFrames = 900;

        [UnityTest]
        public IEnumerator BusyGarden()
        {
            yield return SceneManager.LoadSceneAsync("Garden", LoadSceneMode.Single);
            yield return null;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;

            var world = Object.FindAnyObjectByType<DioramaWorld>();
            var random = new System.Random(1);
            string[] props = { "tree", "rock", "bench" };
            for (int i = 0; i < Props; i++)
                world.Spawn(new PlacedObjectData { definitionId = props[i % props.Length], position = RandomSpot(random), scale = 1f });
            for (int i = 0; i < Creatures; i++)
                world.Spawn(new PlacedObjectData { definitionId = i % 2 == 0 ? "human" : "cat", position = RandomSpot(random), scale = 1f });

            Object.FindAnyObjectByType<AppModeController>().SetMode(AppMode.Preview);
            for (int i = 0; i < WarmupFrames; i++) yield return null;

            var frameTimes = new List<double>(MeasuredFrames);
            long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < MeasuredFrames; i++)
            {
                yield return null;
                frameTimes.Add(Time.unscaledDeltaTime * 1000.0);
            }
            long allocatedPerFrame = (GC.GetAllocatedBytesForCurrentThread() - allocatedBefore) / MeasuredFrames;

            frameTimes.Sort();
            double average = frameTimes.Average();
            double p95 = frameTimes[(int)(frameTimes.Count * 0.95)];
            int simulating = Object.FindObjectsByType<LivingDiorama.Entities.LivingEntity>(FindObjectsSortMode.None).Count(c => c.IsSimulating);
            Debug.Log($"[Perf] objects={world.Objects.Count} creaturesSimulating={simulating} " +
                      $"frame avg={average:F2} ms p95={p95:F2} ms allocated/frame={allocatedPerFrame} B");
        }

        private static Vector3 RandomSpot(System.Random random)
        {
            var candidate = new Vector3((float)(random.NextDouble() * 18 - 9), 0f, (float)(random.NextDouble() * 10 - 5));
            return NavMesh.SamplePosition(candidate, out var hit, 2f, NavMesh.AllAreas) ? hit.position : candidate;
        }
    }
}

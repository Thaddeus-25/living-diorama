using System.Collections.Generic;
using LivingDiorama.Data;
using LivingDiorama.Entities;
using UnityEngine;
using UnityEngine.AI;

namespace LivingDiorama.Interactions
{
    /// <summary>All active interaction points, plus the search creatures use to pick one.</summary>
    public static class InteractionRegistry
    {
        private static readonly List<InteractionPoint> Points = new();
        private static readonly List<InteractionPoint> Candidates = new();
        private static NavMeshPath scratchPath;

        public static IReadOnlyList<InteractionPoint> All => Points;

        // Static state must be reset when entering Play mode with domain reload turned off.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Points.Clear();

        internal static void Register(InteractionPoint point)
        {
            if (!Points.Contains(point)) Points.Add(point);
        }

        internal static void Unregister(InteractionPoint point) => Points.Remove(point);

        /// <summary>
        /// The nearest free point of <paramref name="type"/> that this creature may use and can walk to,
        /// or null. <paramref name="approach"/> is the reachable spot in front of it.
        /// </summary>
        public static InteractionPoint FindNearestAvailable(LivingEntity entity, InteractionType type, float maxDistance,
            out Vector3 approach)
        {
            var from = entity.transform.position;
            Candidates.Clear();
            foreach (var point in Points)
            {
                if (point == null || point.Type != type || !point.IsAvailableFor(entity)) continue;
                if ((point.Position - from).sqrMagnitude > maxDistance * maxDistance) continue;
                Candidates.Add(point);
            }
            Candidates.Sort((a, b) => (a.Position - from).sqrMagnitude.CompareTo((b.Position - from).sqrMagnitude));

            foreach (var point in Candidates)
                if (TryGetReachableApproach(from, point, out approach))
                    return point;

            approach = default;
            return null;
        }

        /// <summary>Snaps the approach spot to the NavMesh and checks a complete path exists from <paramref name="from"/>.</summary>
        public static bool TryGetReachableApproach(Vector3 from, InteractionPoint point, out Vector3 approach)
        {
            approach = default;
            if (!NavMesh.SamplePosition(point.ApproachPosition, out var hit, 0.75f, NavMesh.AllAreas)) return false;

            scratchPath ??= new NavMeshPath();
            if (!NavMesh.CalculatePath(from, hit.position, NavMesh.AllAreas, scratchPath)) return false;
            if (scratchPath.status != NavMeshPathStatus.PathComplete) return false;

            approach = hit.position;
            return true;
        }
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace LivingDiorama.World
{
    /// <summary>Something that lives in a <see cref="WaterArea"/> and moves along when the water is moved.</summary>
    public interface IWaterResident
    {
        void ShiftBy(Vector3 offset);
    }

    /// <summary>
    /// An elliptical body of water on the ground (e.g. a pond), centred on this object. Water creatures
    /// stay inside it, and are carried along when it is dragged in the editor or moved by undo.
    /// Ponds can't rotate, so the ellipse is always axis-aligned.
    /// </summary>
    public class WaterArea : MonoBehaviour, IPlacementAware
    {
        private static readonly List<WaterArea> Areas = new();

        [Tooltip("Open-water ellipse radii in metres before scaling (x = across, y = depth).")]
        [SerializeField] private Vector2 radii = new(1.75f, 1f);

        private readonly List<IWaterResident> residents = new();
        private Vector3 lastPosition;

        public static IReadOnlyList<WaterArea> All => Areas;

        /// <summary>World-space radii, including the object's scale.</summary>
        public Vector2 Radii => new(radii.x * transform.lossyScale.x, radii.y * transform.lossyScale.z);

        // Static state must be reset when entering Play mode with domain reload turned off.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Areas.Clear();

        public void Configure(Vector2 newRadii) => radii = newRadii;

        private void OnEnable()
        {
            if (!Areas.Contains(this)) Areas.Add(this);
            lastPosition = transform.position;
        }

        private void OnDisable() => Areas.Remove(this);

        public void AddResident(IWaterResident resident)
        {
            if (!residents.Contains(resident)) residents.Add(resident);
        }

        public void RemoveResident(IWaterResident resident) => residents.Remove(resident);

        /// <summary>True when the point is inside the water, at least <paramref name="margin"/> metres from the edge.</summary>
        public bool Contains(Vector3 point, float margin = 0f)
        {
            var (x, z) = Normalised(point, margin);
            return x * x + z * z <= 1f;
        }

        /// <summary>The point itself if it's inside the water (with margin), otherwise the nearest spot just inside.</summary>
        public Vector3 ClosestPointInside(Vector3 point, float margin = 0f)
        {
            var centre = transform.position;
            if (Contains(point, margin)) return new Vector3(point.x, centre.y, point.z);

            var (x, z) = Normalised(point, margin);
            var direction = new Vector2(x, z);
            direction = direction.sqrMagnitude > 1e-8f ? direction.normalized * 0.98f : Vector2.zero;
            var (radiusX, radiusZ) = InnerRadii(margin);
            return new Vector3(centre.x + direction.x * radiusX, centre.y, centre.z + direction.y * radiusZ);
        }

        /// <summary>A uniformly random point inside the water, at least <paramref name="margin"/> from the edge.</summary>
        public Vector3 RandomPointInside(System.Random random, float margin = 0f)
        {
            var (radiusX, radiusZ) = InnerRadii(margin);
            float angle = (float)random.NextDouble() * Mathf.PI * 2f;
            float distance = Mathf.Sqrt((float)random.NextDouble()); // sqrt: even spread over the area
            var centre = transform.position;
            return new Vector3(centre.x + Mathf.Cos(angle) * radiusX * distance, centre.y,
                centre.z + Mathf.Sin(angle) * radiusZ * distance);
        }

        public static WaterArea FindContaining(Vector3 point, float margin = 0f)
        {
            foreach (var area in Areas)
                if (area != null && area.Contains(point, margin)) return area;
            return null;
        }

        public static WaterArea FindNearest(Vector3 point)
        {
            WaterArea nearest = null;
            float best = float.MaxValue;
            foreach (var area in Areas)
            {
                if (area == null) continue;
                float distance = (area.ClosestPointInside(point) - point).sqrMagnitude;
                if (distance >= best) continue;
                best = distance;
                nearest = area;
            }
            return nearest;
        }

        /// <summary>The pond was moved (dragged, undo, load): carry its residents by the same offset.</summary>
        public void OnPlacementApplied()
        {
            var offset = transform.position - lastPosition;
            lastPosition = transform.position;
            if (offset.sqrMagnitude < 1e-10f) return;
            foreach (var resident in residents.ToArray()) resident.ShiftBy(offset);
        }

        public Vector3 SavedPosition => transform.position;

        private (float x, float z) Normalised(Vector3 point, float margin)
        {
            var (radiusX, radiusZ) = InnerRadii(margin);
            var offset = point - transform.position;
            return (offset.x / radiusX, offset.z / radiusZ);
        }

        private (float x, float z) InnerRadii(float margin)
        {
            var size = Radii;
            return (Mathf.Max(0.05f, size.x - margin), Mathf.Max(0.05f, size.y - margin));
        }
    }
}

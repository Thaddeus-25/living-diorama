using UnityEngine;

namespace LivingDiorama.SceneEditing
{
    public static class GridSnap
    {
        /// <summary>Rounds X and Z to the nearest multiple of <paramref name="step"/>; Y is left alone.</summary>
        public static Vector3 Snap(Vector3 position, float step)
        {
            if (step <= 0f) return position;
            return new Vector3(
                Mathf.Round(position.x / step) * step,
                position.y,
                Mathf.Round(position.z / step) * step);
        }
    }
}

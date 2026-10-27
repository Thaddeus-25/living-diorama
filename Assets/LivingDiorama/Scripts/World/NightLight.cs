using UnityEngine;

namespace LivingDiorama.World
{
    /// <summary>
    /// A lamp or fire light. Fades up as the scene gets dark (see <see cref="DayNightCycle"/>);
    /// fires can stay lit by day and flicker. Lives on prefabs, so it finds the cycle by itself.
    /// </summary>
    [RequireComponent(typeof(Light))]
    public class NightLight : MonoBehaviour
    {
        [SerializeField] private float nightIntensity = 2f;
        [Tooltip("Brightness in full daylight (0 = off, e.g. streetlights; a fire stays a little lit).")]
        [SerializeField] private float dayIntensity;
        [Tooltip("Random flicker as a fraction of the brightness (0 = steady, ~0.25 = fire).")]
        [SerializeField, Range(0f, 0.5f)] private float flicker;
        [SerializeField] private float flickerSpeed = 7f;

        private Light lamp;
        private float noiseSeed;

        /// <summary>Darkness used when there is no cycle in the scene (e.g. isolated tests).</summary>
        public static float FallbackDarkness { get; set; }

        public void Configure(float night, float day, float flickerAmount)
        {
            nightIntensity = night;
            dayIntensity = day;
            flicker = flickerAmount;
        }

        private void Awake()
        {
            lamp = GetComponent<Light>();
            noiseSeed = Random.value * 100f;
        }

        private void Update()
        {
            var cycle = DayNightCycle.Instance;
            float darkness = cycle != null ? cycle.Darkness : FallbackDarkness;
            float intensity = Mathf.Lerp(dayIntensity, nightIntensity, darkness);
            if (flicker > 0f)
                intensity *= 1f + (Mathf.PerlinNoise(noiseSeed, Time.time * flickerSpeed) - 0.5f) * 2f * flicker;

            lamp.intensity = intensity;
            lamp.enabled = intensity > 0.01f;
        }
    }
}

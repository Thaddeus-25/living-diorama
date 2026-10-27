using System;
using UnityEngine;

namespace LivingDiorama.World
{
    /// <summary>
    /// Time of day for the diorama. Moves the sun (a dim, cool moon at night), and recolours the
    /// sun, ambient light, sky and backdrop. Lamps read <see cref="Darkness"/> through
    /// <see cref="NightLight"/>. Time passes on its own while <see cref="AutoAdvance"/> is on.
    /// </summary>
    public class DayNightCycle : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField] private Light sun;
        [SerializeField] private Renderer sky;
        [SerializeField] private SpriteVisual[] backdrop = Array.Empty<SpriteVisual>();
        [SerializeField] private Camera worldCamera;

        [Header("Time")]
        [SerializeField, Range(0f, 24f)] private float timeOfDay = 10f;
        [SerializeField] private bool autoAdvance = true;
        [Tooltip("Real seconds for one full 24-hour day.")]
        [SerializeField, Min(10f)] private float dayLengthSeconds = 240f;

        [Header("Light")]
        [SerializeField] private float sunIntensity = 1.3f;
        [SerializeField] private float moonIntensity = 0.22f;
        [SerializeField] private float maxSunElevation = 65f;
        [SerializeField] private Color sunNoon = new(1f, 0.95f, 0.84f);
        [SerializeField] private Color sunLow = new(1f, 0.62f, 0.38f);
        [SerializeField] private Color moon = new(0.55f, 0.66f, 1f);

        [Header("Ambient (sky / horizon / ground)")]
        [SerializeField] private Color ambientSkyDay = new(0.66f, 0.74f, 0.84f);
        [SerializeField] private Color ambientEquatorDay = new(0.59f, 0.65f, 0.56f);
        [SerializeField] private Color ambientGroundDay = new(0.42f, 0.35f, 0.28f);
        [SerializeField] private Color ambientSkyNight = new(0.12f, 0.15f, 0.3f);
        [SerializeField] private Color ambientEquatorNight = new(0.08f, 0.1f, 0.2f);
        [SerializeField] private Color ambientGroundNight = new(0.05f, 0.05f, 0.09f);

        [Header("Sky and backdrop tint")]
        [SerializeField] private Color dusk = new(1f, 0.6f, 0.45f);
        [Tooltip("Sky colour at sunrise and sunset (blended in, not multiplied, so it doesn't go muddy).")]
        [SerializeField] private Color skyDusk = new(1f, 0.62f, 0.5f);
        [SerializeField] private Color skyNight = new(0.12f, 0.15f, 0.32f);
        [SerializeField] private Color backdropNight = new(0.2f, 0.24f, 0.4f);

        private MaterialPropertyBlock skyBlock;
        private Color cameraDayBackground;

        /// <summary>The scene's cycle, for lamps spawned from prefabs. Null when there is none.</summary>
        public static DayNightCycle Instance { get; private set; }

        /// <summary>Hours, 0 to 24 (12 = noon).</summary>
        public float TimeOfDay => timeOfDay;
        public bool AutoAdvance => autoAdvance;

        /// <summary>0 in full daylight, 1 at night, in between around sunrise and sunset.</summary>
        public float Darkness { get; private set; }

        /// <summary>Raised when the time or the auto setting changes (not every frame while time passes).</summary>
        public event Action SettingsChanged;

        private void Awake()
        {
            Instance = this;
            if (worldCamera != null) cameraDayBackground = worldCamera.backgroundColor;
            Apply();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            if (!autoAdvance) return;
            timeOfDay = Mathf.Repeat(timeOfDay + 24f * Time.deltaTime / dayLengthSeconds, 24f);
            Apply();
        }

        public void SetTime(float hours)
        {
            timeOfDay = Mathf.Repeat(hours, 24f);
            Apply();
            SettingsChanged?.Invoke();
        }

        public void SetAutoAdvance(bool enabled)
        {
            if (autoAdvance == enabled) return;
            autoAdvance = enabled;
            SettingsChanged?.Invoke();
        }

        /// <summary>"07:30" style text for the current time.</summary>
        public string FormattedTime
        {
            get
            {
                int minutes = Mathf.FloorToInt(timeOfDay * 60f) % (24 * 60);
                return $"{minutes / 60:00}:{minutes % 60:00}";
            }
        }

        /// <summary>Sun height in degrees for an hour: rises at 6, highest at 12, sets at 18, negative at night.</summary>
        public static float SunElevation(float hours, float maxElevation) =>
            Mathf.Sin((hours - 6f) / 12f * Mathf.PI) * maxElevation;

        /// <summary>How much daylight there is for a sun elevation: 0 below -9 degrees, 1 above 15 (a long, gentle twilight).</summary>
        public static float Daylight(float elevation) => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-9f, 15f, elevation));

        private void Apply()
        {
            float elevation = SunElevation(timeOfDay, maxSunElevation);
            float daylight = Daylight(elevation);
            Darkness = 1f - daylight;
            // Strongest at sunrise and sunset.
            float duskAmount = 1f - Mathf.Abs(daylight * 2f - 1f);

            if (sun != null)
            {
                // At night the same light acts as the moon, following the mirrored arc so shadows don't jump.
                float lightElevation = Mathf.Max(Mathf.Abs(elevation), 12f);
                float azimuth = -35f + (timeOfDay - 12f) * 6f;
                sun.transform.rotation = Quaternion.Euler(lightElevation, azimuth, 0f);

                var sunColour = Color.Lerp(sunLow, sunNoon, Mathf.InverseLerp(5f, 40f, elevation));
                sun.color = Color.Lerp(moon, sunColour, daylight);
                sun.intensity = Mathf.Lerp(moonIntensity, sunIntensity * Mathf.Lerp(0.75f, 1f, Mathf.InverseLerp(0f, 35f, elevation)), daylight);
            }

            RenderSettings.ambientSkyColor = Color.Lerp(ambientSkyNight, ambientSkyDay, daylight);
            RenderSettings.ambientEquatorColor = Color.Lerp(Color.Lerp(ambientEquatorNight, ambientEquatorDay, daylight), dusk * 0.8f, duskAmount * 0.5f);
            RenderSettings.ambientGroundColor = Color.Lerp(ambientGroundNight, ambientGroundDay, daylight);

            var skyTint = Color.Lerp(Color.Lerp(skyNight, Color.white, daylight), skyDusk, duskAmount * 0.95f);
            if (sky != null)
            {
                skyBlock ??= new MaterialPropertyBlock();
                sky.GetPropertyBlock(skyBlock);
                skyBlock.SetColor(BaseColorId, skyTint);
                sky.SetPropertyBlock(skyBlock);
            }
            if (worldCamera != null) worldCamera.backgroundColor = cameraDayBackground * skyTint;

            var backdropTint = Color.Lerp(Color.Lerp(backdropNight, Color.white, daylight), dusk, duskAmount * 0.6f);
            foreach (var layer in backdrop)
                if (layer != null) layer.Tint = backdropTint;
        }
    }
}

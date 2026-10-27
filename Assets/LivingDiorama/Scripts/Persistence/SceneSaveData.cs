using System;
using System.Collections.Generic;

namespace LivingDiorama.Persistence
{
    /// <summary>
    /// Root of a saved scene file (JSON). When the format changes, bump
    /// <see cref="SceneSerializer.CurrentVersion"/> and add a step to <see cref="SaveMigrations"/>.
    /// </summary>
    [Serializable]
    public class SceneSaveData
    {
        public int version;
        public string sceneName;
        /// <summary>ISO 8601, UTC.</summary>
        public string savedAtUtc;
        public EnvironmentData environment = new();
        /// <summary>Every placed object, creatures included (their kind comes from definitionId).</summary>
        public List<PlacedObjectData> objects = new();
    }

    /// <summary>Scene-wide settings that aren't objects.</summary>
    [Serializable]
    public class EnvironmentData
    {
        public const string DefaultEnvironmentId = "grassy_garden";

        /// <summary>Which base environment the scene uses. Only the grassy garden exists so far.</summary>
        public string environmentId = DefaultEnvironmentId;
        /// <summary>Camera orthographic size (zoom). 0 or less means "keep the current zoom".</summary>
        public float cameraZoom;
        /// <summary>Hours, 0 to 24. Added in format 2.</summary>
        public float timeOfDay = DefaultTimeOfDay;
        /// <summary>Whether time passes on its own. Added in format 2.</summary>
        public bool timeAdvances = true;

        public const float DefaultTimeOfDay = 10f;
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;

namespace LivingDiorama.Persistence
{
    /// <summary>Outcome of reading a scene: either data (possibly with warnings) or an error message for the user.</summary>
    public sealed class LoadResult
    {
        public bool Success { get; private set; }
        public SceneSaveData Data { get; private set; }
        public string Error { get; private set; }
        public List<string> Warnings { get; } = new();

        public static LoadResult Ok(SceneSaveData data, IEnumerable<string> warnings)
        {
            var result = new LoadResult { Success = true, Data = data };
            result.Warnings.AddRange(warnings);
            return result;
        }

        public static LoadResult Fail(string error) => new() { Success = false, Error = error };
    }

    /// <summary>Converts scenes to and from versioned JSON, validating everything on the way in.</summary>
    public static class SceneSerializer
    {
        /// <summary>Format version written by this build.</summary>
        public const int CurrentVersion = 1;

        public static string ToJson(SceneSaveData data)
        {
            data.version = CurrentVersion;
            return JsonUtility.ToJson(data, prettyPrint: true);
        }

        /// <summary>
        /// Parses and checks a scene file. Never throws for bad input: problems with the whole file
        /// become <see cref="LoadResult.Error"/>; problems with single objects become warnings and
        /// those objects are dropped.
        /// </summary>
        public static LoadResult FromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return LoadResult.Fail("The file is empty.");

            SceneSaveData data;
            try
            {
                data = JsonUtility.FromJson<SceneSaveData>(json);
            }
            catch (ArgumentException e)
            {
                return LoadResult.Fail($"The file isn't valid scene data ({e.Message}).");
            }

            if (data == null) return LoadResult.Fail("The file isn't valid scene data.");
            if (data.version <= 0) return LoadResult.Fail("The file has no format version, so it isn't a Living Diorama scene.");
            if (data.version > CurrentVersion)
                return LoadResult.Fail($"The file was saved by a newer version of Living Diorama (format {data.version}; this one reads up to {CurrentVersion}).");

            var warnings = new List<string>();
            try
            {
                SaveMigrations.Upgrade(data, warnings);
            }
            catch (InvalidOperationException e)
            {
                return LoadResult.Fail(e.Message);
            }

            data.environment ??= new EnvironmentData();
            data.objects ??= new List<PlacedObjectData>();
            data.objects.RemoveAll(entry => !IsUsable(entry, warnings));
            return LoadResult.Ok(data, warnings);
        }

        private static bool IsUsable(PlacedObjectData entry, List<string> warnings)
        {
            if (entry == null) return false;
            if (string.IsNullOrWhiteSpace(entry.definitionId))
            {
                warnings.Add("Skipped an object with no type.");
                return false;
            }
            if (!IsFinite(entry.position.x) || !IsFinite(entry.position.y) || !IsFinite(entry.position.z)
                || !IsFinite(entry.yaw) || !IsFinite(entry.scale) || entry.scale <= 0f)
            {
                warnings.Add($"Skipped a '{entry.definitionId}' with an invalid position, rotation or size.");
                return false;
            }
            return true;
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }

    /// <summary>
    /// Upgrades older save files step by step to <see cref="SceneSerializer.CurrentVersion"/>.
    /// To change the format: bump CurrentVersion, then add a step keyed by the old version that
    /// fixes up the data (fill new fields, convert old values). Never edit an existing step.
    /// Note: JsonUtility drops fields this build doesn't know, so a renamed field needs its old
    /// name kept on <see cref="SceneSaveData"/> for the step to read.
    /// </summary>
    public static class SaveMigrations
    {
        private static readonly Dictionary<int, Action<SceneSaveData, List<string>>> Steps = new()
        {
            // Example for the future:
            // [1] = (data, warnings) => { /* turn a version-1 file into version 2 */ },
        };

        public static void Upgrade(SceneSaveData data, List<string> warnings)
        {
            while (data.version < SceneSerializer.CurrentVersion)
            {
                if (!Steps.TryGetValue(data.version, out var step))
                    throw new InvalidOperationException($"No upgrade path from save format {data.version}.");
                step(data, warnings);
                data.version++;
            }
        }
    }
}

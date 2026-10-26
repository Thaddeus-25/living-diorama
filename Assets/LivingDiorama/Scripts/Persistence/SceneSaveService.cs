using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using LivingDiorama.Core;
using LivingDiorama.SceneEditing;
using LivingDiorama.World;
using UnityEngine;

namespace LivingDiorama.Persistence
{
    /// <summary>Saves the live diorama to files and loads files back into it (New / Save / Load).</summary>
    public class SceneSaveService : MonoBehaviour
    {
        public const string DefaultSceneName = "My Garden";
        public const string UntitledSceneName = "Untitled";

        [SerializeField] private DioramaWorld world;
        [SerializeField] private DioramaCamera dioramaCamera;
        [SerializeField] private SceneEditorController editor;

        private SaveFileStore store;

        public SaveFileStore Store => store ??= new SaveFileStore(SaveFileStore.DefaultDirectory);

        /// <summary>Name the scene will be saved under.</summary>
        public string CurrentName { get; private set; } = DefaultSceneName;

        /// <summary>True once the current scene has a file (saved or loaded), so Ctrl+S can save without asking.</summary>
        public bool HasFile { get; private set; }

        /// <summary>True when there are edits since the last save, load or new scene.</summary>
        public bool IsDirty { get; private set; }

        /// <summary>Name, file or dirty state changed.</summary>
        public event Action StateChanged;

        private void OnEnable()
        {
            if (editor != null) editor.Commands.Changed += MarkDirty;
        }

        private void OnDisable()
        {
            if (editor != null) editor.Commands.Changed -= MarkDirty;
        }

        /// <summary>Sets up a service created from code (tests, tools).</summary>
        public void Initialize(DioramaWorld targetWorld, DioramaCamera targetCamera, SceneEditorController targetEditor)
        {
            OnDisable();
            world = targetWorld;
            dioramaCamera = targetCamera;
            editor = targetEditor;
            if (isActiveAndEnabled) OnEnable();
        }

        /// <summary>Saves to a different folder (tests use a temporary one).</summary>
        public void UseStore(SaveFileStore customStore) => store = customStore;

        public SceneSaveData Capture(string sceneName)
        {
            var data = new SceneSaveData
            {
                sceneName = sceneName,
                savedAtUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
                environment = new EnvironmentData
                {
                    cameraZoom = dioramaCamera != null ? dioramaCamera.TargetSize : 0f
                }
            };
            foreach (var placed in world.Objects)
                if (placed != null) data.objects.Add(world.Capture(placed));
            return data;
        }

        /// <summary>Saves the scene as <paramref name="sceneName"/>. Returns null on success, else a message for the user.</summary>
        public string Save(string sceneName)
        {
            sceneName = string.IsNullOrWhiteSpace(sceneName) ? CurrentName : sceneName.Trim();
            try
            {
                Store.Write(sceneName, SceneSerializer.ToJson(Capture(sceneName)));
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                Debug.LogException(e);
                return e.Message;
            }

            CurrentName = sceneName;
            HasFile = true;
            IsDirty = false;
            StateChanged?.Invoke();
            return null;
        }

        /// <summary>
        /// Loads a saved scene. The file is read and fully checked before anything in the current
        /// scene changes, so a broken file leaves the scene as it was.
        /// </summary>
        public LoadResult Load(string sceneName)
        {
            string json;
            try
            {
                json = Store.Read(sceneName);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                // A missing save folder throws DirectoryNotFoundException rather than FileNotFoundException.
                bool missing = e is FileNotFoundException or DirectoryNotFoundException;
                return LoadResult.Fail(missing ? "That scene file no longer exists." : e.Message);
            }

            var result = SceneSerializer.FromJson(json);
            if (!result.Success) return result;

            Apply(result.Data, result.Warnings);
            CurrentName = string.IsNullOrWhiteSpace(result.Data.sceneName) ? sceneName : result.Data.sceneName;
            HasFile = true;
            IsDirty = false;
            StateChanged?.Invoke();
            return result;
        }

        /// <summary>Empties the garden for a fresh start.</summary>
        public void NewScene()
        {
            editor?.ResetForSceneChange();
            world.Clear();
            CurrentName = UntitledSceneName;
            HasFile = false;
            IsDirty = false;
            StateChanged?.Invoke();
        }

        /// <summary>Replaces the live scene with <paramref name="data"/>. Objects of unknown types are skipped and reported.</summary>
        public void Apply(SceneSaveData data, List<string> warnings)
        {
            editor?.ResetForSceneChange();
            world.Clear();

            if (data.environment != null && data.environment.environmentId != EnvironmentData.DefaultEnvironmentId)
                warnings.Add($"Unknown environment '{data.environment.environmentId}'; using the grassy garden.");

            foreach (var entry in data.objects)
                if (world.Spawn(entry.Clone()) == null)
                    warnings.Add($"Skipped unknown object type '{entry.definitionId}'.");

            if (dioramaCamera != null && data.environment != null && data.environment.cameraZoom > 0f)
                dioramaCamera.SetZoom(data.environment.cameraZoom, instant: true);
        }

        private void MarkDirty()
        {
            if (IsDirty) return;
            IsDirty = true;
            StateChanged?.Invoke();
        }
    }
}

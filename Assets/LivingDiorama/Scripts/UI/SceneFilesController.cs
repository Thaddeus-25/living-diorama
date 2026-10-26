using System;
using System.Collections.Generic;
using System.Linq;
using LivingDiorama.Persistence;
using UnityEngine.UIElements;

namespace LivingDiorama.UI
{
    /// <summary>The New / Save / Load buttons and their dialogs, toasts and confirmations.</summary>
    public sealed class SceneFilesController : IDisposable
    {
        private readonly SceneSaveService service;
        private readonly DialogPanel dialog;
        private readonly Toast toast;
        private readonly Label sceneNameLabel;
        private readonly Button newButton;
        private readonly Button saveButton;
        private readonly Button loadButton;

        public SceneFilesController(VisualElement root, SceneSaveService service, DialogPanel dialog, Toast toast)
        {
            this.service = service;
            this.dialog = dialog;
            this.toast = toast;
            sceneNameLabel = root.Q<Label>("scene-name");
            newButton = root.Q<Button>("new-button");
            saveButton = root.Q<Button>("save-button");
            loadButton = root.Q<Button>("load-button");

            newButton.clicked += AskNewScene;
            saveButton.clicked += OpenSaveDialog;
            loadButton.clicked += OpenLoadDialog;
            service.StateChanged += RefreshSceneName;
            RefreshSceneName();
        }

        /// <summary>Ctrl+S: save straight away if the scene already has a file, otherwise ask for a name.</summary>
        public void QuickSave()
        {
            if (service.HasFile) SaveNow(service.CurrentName);
            else OpenSaveDialog();
        }

        public void OpenSaveDialog()
        {
            dialog.ShowPrompt("Save scene", $"Saved in {service.Store.DirectoryPath}", service.CurrentName, "Save", TrySaveAs);
        }

        public void OpenLoadDialog()
        {
            var items = service.Store.List()
                .Select(file => new DialogPanel.ListItem(file.Name, file.LastWriteTimeUtc.ToLocalTime().ToString("g"),
                    () => AskThenLoad(file.Name)))
                .ToList();
            string text = service.IsDirty ? "Your current scene has unsaved changes." : null;
            dialog.ShowList("Load scene", text, items, "No saved scenes yet. Use Save to create one.");
        }

        public void AskNewScene()
        {
            string text = (service.IsDirty ? "Unsaved changes will be lost. " : string.Empty)
                          + "Everything in the garden will be removed.";
            dialog.ShowConfirm("Start a new scene?", text, "New scene", danger: true, () =>
            {
                service.NewScene();
                toast.Show("Started a new scene.");
            });
        }

        /// <returns>False to keep the name dialog open.</returns>
        private bool TrySaveAs(string name)
        {
            name = name?.Trim();
            if (string.IsNullOrEmpty(name))
            {
                dialog.SetMessage("Please type a name for the scene.", isError: true);
                return false;
            }

            bool sameFileAsCurrent = service.HasFile
                                     && SaveFileStore.ToFileName(name) == SaveFileStore.ToFileName(service.CurrentName);
            if (service.Store.Exists(name) && !sameFileAsCurrent)
            {
                dialog.ShowConfirm("Replace scene?", $"A scene called \"{SaveFileStore.ToFileName(name)}\" already exists. Replace it?",
                    "Replace", danger: true, () => SaveNow(name));
                return true;
            }

            SaveNow(name);
            return true;
        }

        private void SaveNow(string name)
        {
            string error = service.Save(name);
            if (error == null) toast.Show($"Saved \"{service.CurrentName}\".");
            else toast.Show($"Couldn't save: {error}", isError: true, seconds: 6f);
        }

        private void AskThenLoad(string fileName)
        {
            if (!service.IsDirty)
            {
                LoadNow(fileName);
                return;
            }
            dialog.ShowConfirm("Discard changes?", $"Your unsaved changes will be lost if you load \"{fileName}\".",
                "Load anyway", danger: true, () => LoadNow(fileName));
        }

        private void LoadNow(string fileName)
        {
            var result = service.Load(fileName);
            if (!result.Success)
            {
                toast.Show($"Couldn't load \"{fileName}\": {result.Error}", isError: true, seconds: 7f);
                return;
            }

            if (result.Warnings.Count == 0)
                toast.Show($"Loaded \"{service.CurrentName}\".");
            else
                toast.Show($"Loaded \"{service.CurrentName}\" with {Describe(result.Warnings)}", isError: true, seconds: 7f);
        }

        private static string Describe(List<string> warnings) =>
            warnings.Count == 1 ? $"1 problem: {warnings[0]}" : $"{warnings.Count} problems, e.g. {warnings[0]}";

        private void RefreshSceneName()
        {
            sceneNameLabel.text = service.CurrentName + (service.IsDirty ? " *" : string.Empty);
            sceneNameLabel.tooltip = service.IsDirty ? "Unsaved changes" : (service.HasFile ? "Saved" : "Not saved yet");
        }

        public void Dispose()
        {
            newButton.clicked -= AskNewScene;
            saveButton.clicked -= OpenSaveDialog;
            loadButton.clicked -= OpenLoadDialog;
            service.StateChanged -= RefreshSceneName;
        }
    }
}

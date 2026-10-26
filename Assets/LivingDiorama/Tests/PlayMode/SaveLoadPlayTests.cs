using System;
using System.Collections;
using System.IO;
using System.Linq;
using LivingDiorama.Core;
using LivingDiorama.Data;
using LivingDiorama.Entities;
using LivingDiorama.Interactions;
using LivingDiorama.Persistence;
using LivingDiorama.SceneEditing;
using LivingDiorama.UI;
using LivingDiorama.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace LivingDiorama.Tests
{
    /// <summary>Save/load in the real Garden scene, through the service and through the HUD's dialogs.</summary>
    public class SaveLoadPlayTests
    {
        private AppModeController modes;
        private DioramaWorld world;
        private SceneEditorController editor;
        private SceneSaveService service;
        private EditorHud hud;
        private string directory;

        [UnitySetUp]
        public IEnumerator LoadGarden()
        {
            yield return SceneManager.LoadSceneAsync("Garden", LoadSceneMode.Single);
            yield return null;
            modes = Object.FindAnyObjectByType<AppModeController>();
            world = Object.FindAnyObjectByType<DioramaWorld>();
            editor = Object.FindAnyObjectByType<SceneEditorController>();
            service = Object.FindAnyObjectByType<SceneSaveService>();
            hud = Object.FindAnyObjectByType<EditorHud>();
            Assert.IsNotNull(service, "SceneSaveService missing from Garden scene");

            // Never touch the user's real save folder.
            directory = Path.Combine(Path.GetTempPath(), "LivingDioramaTests", Guid.NewGuid().ToString("N"));
            service.UseStore(new SaveFileStore(directory));
        }

        [TearDown]
        public void Cleanup()
        {
            Time.timeScale = 1f;
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }

        [UnityTest]
        public IEnumerator EditSaveNewLoad_RestoresSceneAndCreaturesStillLive()
        {
            Assert.IsFalse(service.IsDirty);
            var extraTree = editor.PlaceAt(world.Database.Find("tree"), new Vector3(-8f, 0f, -4f));
            Assert.IsTrue(service.IsDirty, "Editing marks the scene as unsaved");

            var snapshot = world.Objects.Select(o => world.Capture(o)).ToList();
            Assert.IsNull(service.Save("Play Test"));
            Assert.IsFalse(service.IsDirty);

            service.NewScene();
            yield return null;
            Assert.AreEqual(0, world.Objects.Count);
            Assert.IsFalse(editor.Commands.CanUndo, "Undo history is cleared with the scene");

            var result = service.Load("Play Test");
            Assert.IsTrue(result.Success, result.Error);
            yield return null;
            Assert.AreEqual(snapshot.Count, world.Objects.Count);
            foreach (var saved in snapshot)
                Assert.IsTrue(world.Capture(world.Find(saved.id)).ApproximatelyEquals(saved), $"{saved.definitionId} {saved.id} changed");
            Assert.IsNotNull(world.Find(extraTree.InstanceId), "The tree placed before saving comes back");

            // The loaded scene is fully alive: creatures run and the bench is usable.
            Assert.AreEqual(2, InteractionRegistry.All.Count(p => p.Type == InteractionType.Sit),
                "Only the loaded bench's seats should be registered");
            modes.SetMode(AppMode.Preview);
            yield return null;
            var human = Object.FindObjectsByType<LivingEntity>(FindObjectsSortMode.None).First(c => c.Definition.Id == "human");
            Assert.IsTrue(human.IsSimulating);
            Assert.IsTrue(human.Agent.isOnNavMesh);
            human.StartBehaviour(BehaviourWeight.Interact(InteractionType.Sit, 1f, new Vector2(2f, 2f)));
            Assert.AreEqual("Seek Sit", human.CurrentState.Name);
        }

        [UnityTest]
        public IEnumerator LoadRestoresCameraZoom()
        {
            var cameraRig = Object.FindAnyObjectByType<DioramaCamera>();
            cameraRig.SetZoom(4.5f, instant: true);
            service.Save("Zoomed");
            cameraRig.SetZoom(7f, instant: true);
            yield return null;

            service.Load("Zoomed");
            Assert.AreEqual(4.5f, cameraRig.TargetSize, 0.001f);
        }

        [UnityTest]
        public IEnumerator SaveDialog_SavesUnderTypedName_AndAsksBeforeReplacing()
        {
            hud.Files.OpenSaveDialog();
            Assert.IsTrue(hud.Dialog.IsOpen);
            Assert.IsTrue(editor.IsTextInputFocused(), "Editor shortcuts are blocked while a dialog is open");

            var root = hud.GetComponent<UIDocument>().rootVisualElement;
            root.Q<TextField>("dialog-name").value = "Dialog Scene";
            hud.Dialog.Confirm();
            yield return null;
            Assert.IsFalse(hud.Dialog.IsOpen);
            Assert.IsTrue(service.Store.Exists("Dialog Scene"));
            Assert.AreEqual("Dialog Scene", service.CurrentName);

            // An empty name keeps the dialog open with an explanation.
            hud.Files.OpenSaveDialog();
            root.Q<TextField>("dialog-name").value = "   ";
            hud.Dialog.Confirm();
            Assert.IsTrue(hud.Dialog.IsOpen);
            StringAssert.Contains("name", root.Q<Label>("dialog-message").text);

            // Saving a different scene under an existing name asks first.
            service.Store.Write("Someone Else", "{}");
            root.Q<TextField>("dialog-name").value = "Someone Else";
            hud.Dialog.Confirm();
            Assert.IsTrue(hud.Dialog.IsOpen);
            Assert.AreEqual("Replace scene?", root.Q<Label>("dialog-title").text);
            hud.Dialog.Confirm();
            Assert.IsFalse(hud.Dialog.IsOpen);
            Assert.AreNotEqual("{}", service.Store.Read("Someone Else"), "Replaced with the real scene");
        }

        [UnityTest]
        public IEnumerator LoadDialog_ListsScenes_AndCorruptFileShowsErrorWithoutChangingScene()
        {
            service.Save("Good One");
            service.Store.Write("Broken One", "{ not json");
            int objectsBefore = world.Objects.Count;

            hud.Files.OpenLoadDialog();
            var root = hud.GetComponent<UIDocument>().rootVisualElement;
            var items = root.Query<Button>(className: "dialog-item").ToList();
            Assert.AreEqual(2, items.Count);

            // Pick the broken file through the same code path a click uses.
            var broken = items.First(b => b.Q<Label>(className: "dialog-item__label").text == "Broken One");
            using (var click = NavigationSubmitEvent.GetPooled())
            {
                click.target = broken;
                broken.SendEvent(click);
            }
            yield return null;

            Assert.AreEqual(objectsBefore, world.Objects.Count, "A broken file must not change the scene");
            StringAssert.Contains("Couldn't load", root.Q<Label>("toast").text);
        }

        [UnityTest]
        public IEnumerator LoadingWithUnsavedChanges_AsksFirst()
        {
            service.Save("Saved");
            editor.PlaceAt(world.Database.Find("rock"), new Vector3(0f, 0f, -5f));
            Assert.IsTrue(service.IsDirty);

            hud.Files.OpenLoadDialog();
            var root = hud.GetComponent<UIDocument>().rootVisualElement;
            var item = root.Query<Button>(className: "dialog-item").First();
            using (var click = NavigationSubmitEvent.GetPooled())
            {
                click.target = item;
                item.SendEvent(click);
            }
            yield return null;

            Assert.IsTrue(hud.Dialog.IsOpen);
            Assert.AreEqual("Discard changes?", root.Q<Label>("dialog-title").text);
            hud.Dialog.Close();
            Assert.IsTrue(service.IsDirty, "Cancelling keeps the unsaved scene");
        }
    }
}

using System.Collections;
using System.Linq;
using LivingDiorama.Core;
using LivingDiorama.SceneEditing;
using LivingDiorama.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace LivingDiorama.Tests
{
    /// <summary>Loads the real Garden scene and drives the editor through its public API.</summary>
    public class GardenEditorPlayTests
    {
        private SceneEditorController editor;
        private DioramaWorld world;
        private AppModeController modes;

        [UnitySetUp]
        public IEnumerator LoadGarden()
        {
            yield return SceneManager.LoadSceneAsync("Garden", LoadSceneMode.Single);
            yield return null; // let Start() run
            editor = Object.FindAnyObjectByType<SceneEditorController>();
            world = Object.FindAnyObjectByType<DioramaWorld>();
            modes = Object.FindAnyObjectByType<AppModeController>();
            Assert.IsNotNull(editor, "SceneEditorController missing from Garden scene");
            Assert.IsNotNull(world, "DioramaWorld missing from Garden scene");
            Assert.IsNotNull(modes, "AppModeController missing from Garden scene");
        }

        [UnityTest]
        public IEnumerator Scene_StartsInEditModeWithStartingProps()
        {
            Assert.AreEqual(AppMode.Edit, modes.Mode);
            Assert.GreaterOrEqual(world.Objects.Count, 7);
            Assert.IsTrue(world.Objects.All(o => o.Definition != null));
            yield return null;
        }

        [UnityTest]
        public IEnumerator PlaceDuplicateDeleteUndo_KeepsWorldConsistent()
        {
            int startCount = world.Objects.Count;
            var tree = world.Database.Find("tree");

            var placed = editor.PlaceAt(tree, new Vector3(2f, 0f, 0f));
            Assert.IsNotNull(placed);
            Assert.AreEqual(startCount + 1, world.Objects.Count);

            editor.Select(placed);
            editor.DuplicateSelected();
            Assert.AreEqual(startCount + 2, world.Objects.Count);
            Assert.AreNotSame(placed, editor.Selected, "The duplicate becomes the selection");

            editor.DeleteSelected();
            Assert.IsNull(editor.Selected);
            Assert.AreEqual(startCount + 1, world.Objects.Count);
            yield return null;

            editor.Undo(); // restore the duplicate
            Assert.AreEqual(startCount + 2, world.Objects.Count);
            editor.Undo(); // remove the duplicate
            editor.Undo(); // remove the placed tree
            yield return null;
            Assert.AreEqual(startCount, world.Objects.Count);
        }

        [UnityTest]
        public IEnumerator SliderStyleEdits_MergeIntoOneUndoStep()
        {
            var rock = world.Objects.First(o => o.Definition.Id == "rock");
            var start = world.Capture(rock);
            editor.Select(rock);

            int historyBefore = editor.Commands.UndoCount;
            for (int i = 1; i <= 5; i++)
                editor.EditSelected(d => d.position.x = start.position.x + i * 0.2f);

            // Wait past the merge window so the edit is committed.
            yield return new WaitForSecondsRealtime(0.6f);
            Assert.AreEqual(historyBefore + 1, editor.Commands.UndoCount);

            editor.Undo();
            Assert.IsTrue(world.Capture(rock).ApproximatelyEquals(start));
        }

        [UnityTest]
        public IEnumerator PreviewMode_ClearsSelectionAndToggleReturnsToEdit()
        {
            editor.Select(world.Objects.First());
            modes.SetMode(AppMode.Preview);
            yield return null;
            Assert.IsNull(editor.Selected);
            Assert.IsTrue(modes.IsSimulationRunning);

            modes.Toggle();
            Assert.AreEqual(AppMode.Edit, modes.Mode);
        }
    }
}

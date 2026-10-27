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
        public IEnumerator Hover_ShowsFaintRing_ButNotOnTheSelection()
        {
            var rock = world.Objects.First(o => o.Definition.Id == "rock");
            var tree = world.Objects.First(o => o.Definition.Id == "tree");
            var hoverRing = Object.FindObjectsByType<SelectionIndicator>(FindObjectsSortMode.None).First(i => i.name == "HoverIndicator");

            editor.SetHovered(rock);
            yield return null;
            Assert.AreSame(rock, editor.Hovered);
            Assert.AreSame(rock.transform, hoverRing.Target);

            editor.Select(rock);
            Assert.IsNull(editor.Hovered, "Selecting the hovered object hands it over to the selection ring");
            editor.SetHovered(rock);
            Assert.IsNull(editor.Hovered, "The selected object never gets a hover ring");

            editor.SetHovered(tree);
            Assert.AreSame(tree, editor.Hovered);
            modes.SetMode(AppMode.Preview);
            yield return null;
            Assert.IsNull(editor.Hovered, "No hover ring in Preview");
        }

        [UnityTest]
        public IEnumerator PlacementPreview_IsTintedAndCastsNoShadow()
        {
            editor.StartPlacing(world.Database.Find("tree"));
            yield return null;
            // The preview stays hidden until the mouse is over the ground, so include inactive objects.
            var ghost = Object.FindObjectsByType<SpriteVisual>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .First(v => v.GetComponentInParent<PlacedObject>() == null && v.transform.root.name.StartsWith("Ghost"));
            Assert.AreNotEqual(Color.white, ghost.Tint);
            Assert.AreEqual(UnityEngine.Rendering.ShadowCastingMode.Off, ghost.Renderer.shadowCastingMode);
            Assert.IsEmpty(ghost.transform.root.GetComponentsInChildren<ParticleSystem>(true), "No falling leaves from a preview");
            editor.CancelPlacing();
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

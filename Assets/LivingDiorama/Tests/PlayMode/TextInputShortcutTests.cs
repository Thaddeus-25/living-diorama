using System.Collections;
using System.Linq;
using LivingDiorama.SceneEditing;
using LivingDiorama.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace LivingDiorama.Tests
{
    /// <summary>
    /// Typing into a field must not trigger editor shortcuts (Backspace used to delete the selection).
    /// UI Toolkit reports the whole Slider as focused while its number box is being edited, so the
    /// editor has to track focus on the text inputs themselves.
    /// </summary>
    public class TextInputShortcutTests
    {
        [UnityTest]
        public IEnumerator FocusedSliderNumberBox_BlocksShortcutsUntilFocusLeaves()
        {
            yield return SceneManager.LoadSceneAsync("Garden", LoadSceneMode.Single);
            yield return null;

            var editor = Object.FindAnyObjectByType<SceneEditorController>();
            var world = Object.FindAnyObjectByType<DioramaWorld>();
            var root = Object.FindAnyObjectByType<UIDocument>().rootVisualElement;

            editor.Select(world.Objects.First());
            yield return null;
            Assert.IsFalse(editor.IsTextInputFocused(), "Nothing is being typed yet");

            var numberBox = root.Q<Slider>("position-x").Q(className: TextInputBaseField<string>.inputUssClassName);
            Assert.IsNotNull(numberBox, "Slider has no number box");
            numberBox.Focus();
            yield return null;
            Assert.IsTrue(editor.IsTextInputFocused(), "Typing in the number box must block shortcuts");

            // What the HUD does when the user clicks in the world.
            Object.FindAnyObjectByType<LivingDiorama.UI.EditorHud>().ReleaseUiFocus();
            yield return null;
            Assert.IsFalse(editor.IsTextInputFocused(), "Shortcuts work again after leaving the box");
        }
    }
}

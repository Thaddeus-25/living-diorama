using LivingDiorama.Core;
using LivingDiorama.SceneEditing;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace LivingDiorama.UI
{
    /// <summary>
    /// Connects the UI Toolkit editor overlay (EditorHud.uxml) to the scene editor.
    /// Panel-specific logic lives in <see cref="ObjectLibraryPanel"/> and <see cref="PropertiesPanel"/>.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class EditorHud : MonoBehaviour
    {
        private const string EditHint =
            "Click to select  ·  Drag to move  ·  R rotate  ·  Ctrl+D duplicate  ·  Del delete  ·  G grid  ·  Ctrl+Z / Ctrl+Y undo/redo  ·  Scroll zoom  ·  Tab preview";

        [SerializeField] private SceneEditorController editor;
        [SerializeField] private AppModeController modes;
        [SerializeField] private DioramaCamera dioramaCamera;
        [Tooltip("Seconds the 'Press Tab to edit' hint stays visible after entering Preview.")]
        [SerializeField] private float previewHintSeconds = 3f;

        private UIDocument document;
        private VisualElement hudRoot;
        private Label hintBar;
        private Label previewHint;
        private Button undoButton;
        private Button redoButton;
        private Toggle gridToggle;
        private ObjectLibraryPanel library;
        private PropertiesPanel properties;
        private float previewHintHideTime;
        private VisualElement focusedTextInput;

        private void OnEnable()
        {
            document = GetComponent<UIDocument>();
            var root = document.rootVisualElement;
            // The document root covers the whole screen; it must not count as "UI under the mouse".
            root.pickingMode = PickingMode.Ignore;

            hudRoot = root.Q("hud-root");
            hintBar = root.Q<Label>("hint-bar");
            previewHint = root.Q<Label>("preview-hint");
            undoButton = root.Q<Button>("undo-button");
            redoButton = root.Q<Button>("redo-button");
            gridToggle = root.Q<Toggle>("grid-toggle");

            undoButton.clicked += editor.Undo;
            redoButton.clicked += editor.Redo;
            root.Q<Button>("preview-button").clicked += EnterPreview;
            gridToggle.RegisterValueChangedCallback(OnGridToggled);

            library = new ObjectLibraryPanel(root.Q("library-panel"), editor);
            properties = new PropertiesPanel(root.Q("properties-panel"), editor);
            TrackTextInputs(root);

            editor.IsPointerOverUi = IsPointerOverUi;
            editor.IsTextInputFocused = IsTextInputFocused;
            editor.Commands.Changed += RefreshHistoryButtons;
            editor.ToolStateChanged += RefreshToolState;
            modes.ModeChanged += OnModeChanged;

            RefreshHistoryButtons();
            RefreshToolState();
            OnModeChanged(modes.Mode);
        }

        private void OnDisable()
        {
            if (editor != null)
            {
                editor.Commands.Changed -= RefreshHistoryButtons;
                editor.ToolStateChanged -= RefreshToolState;
                editor.IsPointerOverUi = null;
                editor.IsTextInputFocused = null;
                if (undoButton != null) undoButton.clicked -= editor.Undo;
                if (redoButton != null) redoButton.clicked -= editor.Redo;
            }
            if (modes != null) modes.ModeChanged -= OnModeChanged;
            library?.Dispose();
            properties?.Dispose();
        }

        private void Update()
        {
            var mouse = Mouse.current;
            if (mouse != null)
            {
                bool overUi = IsPointerOverUi(mouse.position.ReadValue());
                // Scrolling over a panel (e.g. the library list) shouldn't also zoom the camera.
                if (dioramaCamera != null) dioramaCamera.ZoomEnabled = !overUi;
                // Clicking the world finishes any typing, so shortcuts work again.
                if (!overUi && (mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame))
                    ReleaseUiFocus();
            }

            if (previewHint.resolvedStyle.opacity > 0f && Time.unscaledTime > previewHintHideTime)
                previewHint.style.opacity = 0f;
        }

        /// <summary>True when the screen position (pixels, origin bottom-left) is over a visible panel.</summary>
        public bool IsPointerOverUi(Vector2 screenPosition)
        {
            var root = document != null ? document.rootVisualElement : null;
            var panel = root?.panel;
            if (panel == null || hudRoot.resolvedStyle.display == DisplayStyle.None) return false;

            // Panel coordinates have their origin at the top-left.
            var flipped = new Vector2(screenPosition.x, Screen.height - screenPosition.y);
            var picked = panel.Pick(RuntimePanelUtils.ScreenToPanel(panel, flipped));
            return picked != null && picked != root && picked != panel.visualTree;
        }

        /// <summary>
        /// True while the user types into any text or number field (including the sliders' input
        /// boxes), so keys like Backspace edit the text instead of deleting the selected object.
        /// </summary>
        private bool IsTextInputFocused() => focusedTextInput != null;

        /// <summary>
        /// Removes keyboard focus from the UI. Blur() must be called on the element the focus
        /// controller reports (e.g. the Slider), not on the inner text box, or nothing happens.
        /// </summary>
        public void ReleaseUiFocus()
        {
            document.rootVisualElement.focusController?.focusedElement?.Blur();
            focusedTextInput = null;
        }

        /// <summary>
        /// Tracks focus on every text input under <paramref name="container"/>. The callbacks sit on
        /// the inputs themselves because UI Toolkit retargets focus to composite controls: while a
        /// slider's number box is edited, focusController.focusedElement reports the Slider.
        /// Call again for UI that is added later.
        /// </summary>
        public void TrackTextInputs(VisualElement container)
        {
            container.Query(className: TextInputBaseField<string>.inputUssClassName).ForEach(input =>
            {
                input.RegisterCallback<FocusInEvent>(_ => focusedTextInput = input);
                input.RegisterCallback<FocusOutEvent>(_ =>
                {
                    if (focusedTextInput == input) focusedTextInput = null;
                });
            });
        }

        private void EnterPreview() => modes.SetMode(AppMode.Preview);

        private void OnGridToggled(ChangeEvent<bool> change) => editor.SetGridSnap(change.newValue);

        private void RefreshHistoryButtons()
        {
            undoButton.SetEnabled(editor.Commands.CanUndo);
            redoButton.SetEnabled(editor.Commands.CanRedo);
            undoButton.tooltip = editor.Commands.CanUndo ? $"Undo {editor.Commands.NextUndoDescription} (Ctrl+Z)" : "Nothing to undo";
            redoButton.tooltip = editor.Commands.CanRedo ? $"Redo {editor.Commands.NextRedoDescription} (Ctrl+Y)" : "Nothing to redo";
        }

        private void RefreshToolState()
        {
            gridToggle.SetValueWithoutNotify(editor.GridSnapEnabled);
            var placing = editor.PlacingDefinition;
            hintBar.text = placing != null
                ? $"Click to place {placing.DisplayName}  ·  Hold Shift to place several  ·  Right-click or Esc to stop"
                : EditHint;
            library.SetActiveItem(placing);
        }

        private void OnModeChanged(AppMode mode)
        {
            bool editing = mode == AppMode.Edit;
            hudRoot.style.display = editing ? DisplayStyle.Flex : DisplayStyle.None;
            previewHint.style.display = editing ? DisplayStyle.None : DisplayStyle.Flex;
            if (!editing)
            {
                // A hidden field could otherwise keep "typing" focus and block shortcuts.
                ReleaseUiFocus();
                previewHint.style.opacity = 1f;
                previewHintHideTime = Time.unscaledTime + previewHintSeconds;
            }
        }
    }
}

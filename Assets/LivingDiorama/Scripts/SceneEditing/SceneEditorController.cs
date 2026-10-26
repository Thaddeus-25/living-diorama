using System;
using LivingDiorama.Core;
using LivingDiorama.Data;
using LivingDiorama.Persistence;
using LivingDiorama.SceneEditing.Commands;
using LivingDiorama.World;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

namespace LivingDiorama.SceneEditing
{
    /// <summary>
    /// Mouse and keyboard editing of the diorama: place, select, drag, rotate, duplicate,
    /// delete, grid snap, undo/redo and the Edit/Preview toggle.
    /// Every change goes through <see cref="Commands"/> so it can be undone.
    /// </summary>
    public class SceneEditorController : MonoBehaviour
    {
        [SerializeField] private DioramaWorld world;
        [SerializeField] private Camera worldCamera;
        [SerializeField] private AppModeController modes;
        [SerializeField] private SelectionIndicator selectionIndicator;

        [Header("Tuning")]
        [SerializeField] private float gridStep = 0.5f;
        [SerializeField] private float rotateStep = 45f;
        [Tooltip("The mouse must move this far (pixels) before a click turns into a drag.")]
        [SerializeField] private float dragThresholdPixels = 4f;
        [Tooltip("Slider edits are merged into one undo step once they pause for this long.")]
        [SerializeField] private float editMergeSeconds = 0.4f;
        [SerializeField] private Vector3 duplicateOffset = new(1f, 0f, -0.5f);

        private static readonly Plane GroundPlane = new(Vector3.up, Vector3.zero);

        // Press / drag state.
        private PlacedObject pressedObject;
        private Vector2 pressScreenPosition;
        private Vector3 grabOffset;
        private PlacedObjectData dragBefore;
        private bool isDragging;

        // Placement state.
        private ObjectDefinition placingDefinition;
        private GameObject ghost;

        // Property edits waiting to be merged into one undo step.
        private PlacedObjectData pendingEditBefore;
        private float lastEditTime;

        public CommandStack Commands { get; } = new();
        public DioramaWorld World => world;
        public PlacedObject Selected { get; private set; }
        public bool GridSnapEnabled { get; private set; }
        public ObjectDefinition PlacingDefinition => placingDefinition;
        public bool IsDragging => isDragging;

        /// <summary>Set by the UI so clicks on panels don't reach the world.</summary>
        public Func<Vector2, bool> IsPointerOverUi { get; set; }

        /// <summary>Set by the UI so typing in a text field doesn't trigger shortcuts.</summary>
        public Func<bool> IsTextInputFocused { get; set; }

        public event Action SelectionChanged;
        /// <summary>Grid snap or placement mode changed.</summary>
        public event Action ToolStateChanged;
        /// <summary>The selected object's state changed (drag, edit, undo...).</summary>
        public event Action SelectedObjectChanged;

        private void OnEnable()
        {
            Commands.Changed += OnHistoryChanged;
            if (modes != null) modes.ModeChanged += OnModeChanged;
            if (world != null) world.ObjectChanged += OnWorldObjectChanged;
        }

        private void OnDisable()
        {
            Commands.Changed -= OnHistoryChanged;
            if (modes != null) modes.ModeChanged -= OnModeChanged;
            if (world != null) world.ObjectChanged -= OnWorldObjectChanged;
        }

        private void Update()
        {
            HandleKeyboard();

            if (pendingEditBefore != null && Time.unscaledTime - lastEditTime > editMergeSeconds)
                CommitPendingEdit();

            var mouse = Mouse.current;
            if (mouse == null || !modes.IsEditing) return;

            Vector2 screenPosition = mouse.position.ReadValue();
            bool overUi = IsPointerOverUi?.Invoke(screenPosition) ?? false;

            if (placingDefinition != null)
            {
                UpdatePlacement(mouse, screenPosition, overUi);
                return;
            }

            if (mouse.leftButton.wasPressedThisFrame && !overUi) BeginPress(screenPosition);
            if (pressedObject != null && mouse.leftButton.isPressed) ContinuePress(screenPosition);
            if (pressedObject != null && mouse.leftButton.wasReleasedThisFrame) EndPress();
        }

        // ------------------------------------------------------------------ public API (used by UI)

        public void Select(PlacedObject placed)
        {
            if (ReferenceEquals(Selected, placed)) return;
            CommitPendingEdit();
            Selected = placed;
            if (selectionIndicator != null) selectionIndicator.Show(placed);
            SelectionChanged?.Invoke();
        }

        public void StartPlacing(ObjectDefinition definition)
        {
            if (definition == null || definition.Prefab == null) return;
            CancelPlacing();
            Select(null);
            placingDefinition = definition;
            ghost = CreateGhost(definition);
            ToolStateChanged?.Invoke();
        }

        public void CancelPlacing()
        {
            if (ghost != null) Destroy(ghost);
            ghost = null;
            if (placingDefinition == null) return;
            placingDefinition = null;
            ToolStateChanged?.Invoke();
        }

        public void SetGridSnap(bool enabled)
        {
            if (GridSnapEnabled == enabled) return;
            GridSnapEnabled = enabled;
            ToolStateChanged?.Invoke();
        }

        public PlacedObject PlaceAt(ObjectDefinition definition, Vector3 position)
        {
            var data = new PlacedObjectData
            {
                id = PlacedObject.NewId(),
                definitionId = definition.Id,
                position = position,
                scale = definition.DefaultScale
            };
            Commands.Execute(new PlaceObjectCommand(world, data, $"Place {definition.DisplayName}"));
            return world.Find(data.id);
        }

        public void DeleteSelected()
        {
            if (Selected == null) return;
            CommitPendingEdit();
            var data = world.Capture(Selected);
            Select(null);
            Commands.Execute(new DeleteObjectCommand(world, data));
        }

        public void DuplicateSelected()
        {
            if (Selected == null) return;
            CommitPendingEdit();
            var copy = world.Capture(Selected);
            copy.id = PlacedObject.NewId();
            copy.position = world.ClampToGarden(copy.position + duplicateOffset);
            Commands.Execute(new PlaceObjectCommand(world, copy, "Duplicate"));
            Select(world.Find(copy.id));
        }

        public void RotateSelected(float degrees)
        {
            if (Selected == null || !Selected.Definition.AllowRotation) return;
            EditSelected(data => data.yaw = Mathf.Repeat(data.yaw + degrees, 360f));
            CommitPendingEdit();
        }

        /// <summary>
        /// Applies a change to the selected object right away. Rapid changes (e.g. dragging a slider)
        /// are merged into a single undo step.
        /// </summary>
        public void EditSelected(Action<PlacedObjectData> change)
        {
            if (Selected == null) return;
            pendingEditBefore ??= world.Capture(Selected);
            var data = world.Capture(Selected);
            change(data);
            world.Apply(data);
            lastEditTime = Time.unscaledTime;
        }

        public void Undo()
        {
            CommitPendingEdit();
            EndPressWithoutCommit();
            Commands.Undo();
        }

        public void Redo()
        {
            CommitPendingEdit();
            EndPressWithoutCommit();
            Commands.Redo();
        }

        // ------------------------------------------------------------------ keyboard

        private void HandleKeyboard()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;
            if (IsTextInputFocused?.Invoke() ?? false) return;

            if (keyboard.tabKey.wasPressedThisFrame)
            {
                modes.Toggle();
                return;
            }

            if (!modes.IsEditing) return;

            bool ctrl = keyboard.ctrlKey.isPressed;
            bool shift = keyboard.shiftKey.isPressed;

            if (ctrl && keyboard.zKey.wasPressedThisFrame)
            {
                if (shift) Redo();
                else Undo();
            }
            else if (ctrl && keyboard.yKey.wasPressedThisFrame) Redo();
            else if (ctrl && keyboard.dKey.wasPressedThisFrame) DuplicateSelected();
            else if (keyboard.deleteKey.wasPressedThisFrame || keyboard.backspaceKey.wasPressedThisFrame) DeleteSelected();
            else if (keyboard.gKey.wasPressedThisFrame) SetGridSnap(!GridSnapEnabled);
            else if (keyboard.rKey.wasPressedThisFrame) RotateSelected(shift ? -rotateStep : rotateStep);
            else if (keyboard.escapeKey.wasPressedThisFrame)
            {
                if (placingDefinition != null) CancelPlacing();
                else Select(null);
            }
        }

        // ------------------------------------------------------------------ select & drag

        private void BeginPress(Vector2 screenPosition)
        {
            var picked = PickObject(screenPosition);
            Select(picked);
            if (picked == null) return;

            pressedObject = picked;
            pressScreenPosition = screenPosition;
            isDragging = false;
            if (TryGetGroundPoint(screenPosition, out var groundPoint))
                grabOffset = picked.transform.position - groundPoint;
        }

        private void ContinuePress(Vector2 screenPosition)
        {
            if (!isDragging)
            {
                if (Vector2.Distance(screenPosition, pressScreenPosition) < dragThresholdPixels) return;
                CommitPendingEdit();
                isDragging = true;
                dragBefore = world.Capture(pressedObject);
                SetObstacleCarving(pressedObject, false);
            }

            if (!TryGetGroundPoint(screenPosition, out var groundPoint)) return;
            var target = groundPoint + grabOffset;
            if (GridSnapEnabled) target = GridSnap.Snap(target, gridStep);
            world.Move(pressedObject, target);
        }

        private void EndPress()
        {
            if (isDragging && pressedObject != null)
            {
                SetObstacleCarving(pressedObject, true);
                var after = world.Capture(pressedObject);
                if (!after.ApproximatelyEquals(dragBefore))
                    Commands.Record(new ModifyObjectCommand(world, dragBefore, after, "Move"));
            }
            EndPressWithoutCommit();
        }

        private void EndPressWithoutCommit()
        {
            if (pressedObject != null) SetObstacleCarving(pressedObject, true);
            pressedObject = null;
            isDragging = false;
            dragBefore = null;
        }

        /// <summary>A moving obstacle shouldn't carve the NavMesh on every frame of a drag.</summary>
        private static void SetObstacleCarving(PlacedObject placed, bool enabled)
        {
            if (placed != null && placed.TryGetComponent(out NavMeshObstacle obstacle))
                obstacle.carving = enabled;
        }

        private PlacedObject PickObject(Vector2 screenPosition)
        {
            var ray = worldCamera.ScreenPointToRay(screenPosition);
            var hits = Physics.RaycastAll(ray, 500f, ~0, QueryTriggerInteraction.Collide);
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var hit in hits)
            {
                var placed = hit.collider.GetComponentInParent<PlacedObject>();
                if (placed != null && world.Find(placed.InstanceId) == placed) return placed;
            }
            return null;
        }

        private bool TryGetGroundPoint(Vector2 screenPosition, out Vector3 point)
        {
            var ray = worldCamera.ScreenPointToRay(screenPosition);
            if (GroundPlane.Raycast(ray, out float enter))
            {
                point = ray.GetPoint(enter);
                return true;
            }
            point = default;
            return false;
        }

        // ------------------------------------------------------------------ placement

        private void UpdatePlacement(Mouse mouse, Vector2 screenPosition, bool overUi)
        {
            if (mouse.rightButton.wasPressedThisFrame)
            {
                CancelPlacing();
                return;
            }

            bool onGround = TryGetGroundPoint(screenPosition, out var groundPoint);
            if (ghost != null)
            {
                ghost.SetActive(onGround && !overUi);
                if (onGround)
                {
                    var target = GridSnapEnabled ? GridSnap.Snap(groundPoint, gridStep) : groundPoint;
                    ghost.transform.position = world.ClampToGarden(target);
                }
            }

            if (onGround && !overUi && mouse.leftButton.wasPressedThisFrame)
            {
                var definition = placingDefinition;
                var placed = PlaceAt(definition, ghost != null ? ghost.transform.position : world.ClampToGarden(groundPoint));
                // Shift keeps the tool active for placing several in a row.
                bool keepPlacing = Keyboard.current != null && Keyboard.current.shiftKey.isPressed;
                if (!keepPlacing)
                {
                    CancelPlacing();
                    Select(placed);
                }
            }
        }

        /// <summary>
        /// A preview copy of the prefab that follows the mouse. It's built inside an inactive parent
        /// so its gameplay components can be stripped before they ever run.
        /// </summary>
        private GameObject CreateGhost(ObjectDefinition definition)
        {
            var holder = new GameObject("GhostHolder");
            holder.SetActive(false);
            var preview = Instantiate(definition.Prefab, holder.transform);
            preview.name = $"Ghost ({definition.DisplayName})";

            foreach (var behaviour in preview.GetComponentsInChildren<PlacedObject>(true)) DestroyImmediate(behaviour);
            foreach (var obstacle in preview.GetComponentsInChildren<NavMeshObstacle>(true)) DestroyImmediate(obstacle);
            foreach (var collider in preview.GetComponentsInChildren<Collider>(true)) DestroyImmediate(collider);

            preview.transform.localScale = Vector3.one * definition.DefaultScale;
            preview.transform.SetParent(null, false);
            Destroy(holder);
            preview.SetActive(false); // shown once the mouse is over the ground
            return preview;
        }

        // ------------------------------------------------------------------ events

        private void CommitPendingEdit()
        {
            if (pendingEditBefore == null) return;
            var before = pendingEditBefore;
            pendingEditBefore = null;

            var current = world.Find(before.id);
            if (current == null) return;
            var after = world.Capture(current);
            if (!after.ApproximatelyEquals(before))
                Commands.Record(new ModifyObjectCommand(world, before, after));
        }

        private void OnHistoryChanged()
        {
            // Undo/redo may have removed the selected object. The (object) cast skips Unity's
            // destroyed-object null check, so a destroyed selection is cleared too.
            bool selectionGone = (object)Selected != null
                                 && (Selected == null || world.Find(Selected.InstanceId) != Selected);
            if (selectionGone) ClearSelection();
            else SelectedObjectChanged?.Invoke();
        }

        private void ClearSelection()
        {
            Selected = null;
            if (selectionIndicator != null) selectionIndicator.Show(null);
            SelectionChanged?.Invoke();
        }

        private void OnWorldObjectChanged(PlacedObject placed)
        {
            if (placed == Selected) SelectedObjectChanged?.Invoke();
        }

        private void OnModeChanged(AppMode mode)
        {
            if (mode == AppMode.Edit) return;
            CommitPendingEdit();
            EndPress();
            CancelPlacing();
            Select(null);
        }
    }
}

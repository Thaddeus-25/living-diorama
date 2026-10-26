using System;
using LivingDiorama.SceneEditing;
using UnityEngine;
using UnityEngine.UIElements;

namespace LivingDiorama.UI
{
    /// <summary>Shows and edits the selected object's position, depth, rotation, size and mirroring.</summary>
    public sealed class PropertiesPanel : IDisposable
    {
        private readonly VisualElement panel;
        private readonly SceneEditorController editor;
        private readonly Label title;
        private readonly Slider positionX;
        private readonly Slider positionZ;
        private readonly Slider rotation;
        private readonly Slider scale;
        private readonly Toggle flip;
        private readonly Button duplicateButton;
        private readonly Button deleteButton;

        public PropertiesPanel(VisualElement panel, SceneEditorController editor)
        {
            this.panel = panel;
            this.editor = editor;

            title = panel.Q<Label>("properties-title");
            positionX = panel.Q<Slider>("position-x");
            positionZ = panel.Q<Slider>("position-z");
            rotation = panel.Q<Slider>("rotation");
            scale = panel.Q<Slider>("scale");
            flip = panel.Q<Toggle>("flip");
            duplicateButton = panel.Q<Button>("duplicate-button");
            deleteButton = panel.Q<Button>("delete-button");

            var garden = editor.World.GardenSize;
            positionX.lowValue = -garden.x * 0.5f;
            positionX.highValue = garden.x * 0.5f;
            // Depth reads front (low) to back (high), matching world Z.
            positionZ.lowValue = -garden.y * 0.5f;
            positionZ.highValue = garden.y * 0.5f;

            positionX.RegisterValueChangedCallback(e => editor.EditSelected(d => d.position.x = e.newValue));
            positionZ.RegisterValueChangedCallback(e => editor.EditSelected(d => d.position.z = e.newValue));
            rotation.RegisterValueChangedCallback(e => editor.EditSelected(d => d.yaw = e.newValue));
            scale.RegisterValueChangedCallback(e => editor.EditSelected(d => d.scale = e.newValue));
            flip.RegisterValueChangedCallback(e => editor.EditSelected(d => d.flipX = e.newValue));
            duplicateButton.clicked += editor.DuplicateSelected;
            deleteButton.clicked += editor.DeleteSelected;

            editor.SelectionChanged += Refresh;
            editor.SelectedObjectChanged += Refresh;
            Refresh();
        }

        private void Refresh()
        {
            var selected = editor.Selected;
            panel.style.display = selected != null ? DisplayStyle.Flex : DisplayStyle.None;
            if (selected == null) return;

            var data = editor.World.Capture(selected);
            title.text = selected.Definition != null ? selected.Definition.DisplayName : "Object";
            positionX.SetValueWithoutNotify(Round(data.position.x));
            positionZ.SetValueWithoutNotify(Round(data.position.z));
            rotation.SetValueWithoutNotify(Round(Mathf.Repeat(data.yaw, 360f)));
            scale.SetValueWithoutNotify(Round(data.scale));
            flip.SetValueWithoutNotify(data.flipX);

            bool canRotate = selected.Definition == null || selected.Definition.AllowRotation;
            rotation.style.display = canRotate ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private static float Round(float value) => Mathf.Round(value * 100f) / 100f;

        public void Dispose()
        {
            editor.SelectionChanged -= Refresh;
            editor.SelectedObjectChanged -= Refresh;
            duplicateButton.clicked -= editor.DuplicateSelected;
            deleteButton.clicked -= editor.DeleteSelected;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using LivingDiorama.Data;
using LivingDiorama.SceneEditing;
using UnityEngine.UIElements;

namespace LivingDiorama.UI
{
    /// <summary>
    /// Category tabs plus a grid of placeable objects, built from the <see cref="ObjectDatabase"/>.
    /// New definitions show up automatically; there is nothing to register here.
    /// </summary>
    public sealed class ObjectLibraryPanel : IDisposable
    {
        private readonly SceneEditorController editor;
        private readonly VisualElement tabs;
        private readonly ScrollView items;
        private readonly Dictionary<ObjectDefinition, Button> itemButtons = new();
        private ObjectCategory activeCategory;

        public ObjectLibraryPanel(VisualElement panel, SceneEditorController editor)
        {
            this.editor = editor;
            tabs = panel.Q("category-tabs");
            items = panel.Q<ScrollView>("library-items");

            var categories = editor.World.Database.Definitions
                .Where(definition => definition != null)
                .Select(definition => definition.Category)
                .Distinct()
                .OrderBy(category => category)
                .ToList();

            foreach (var category in categories)
            {
                var tab = new Button(() => ShowCategory(category)) { text = Pretty(category), name = $"tab-{category}" };
                tab.AddToClassList("category-tab");
                tabs.Add(tab);
            }

            if (categories.Count > 0) ShowCategory(categories[0]);
        }

        public void SetActiveItem(ObjectDefinition placing)
        {
            foreach (var (definition, button) in itemButtons)
                button.EnableInClassList("library-item--active", definition == placing);
        }

        private void ShowCategory(ObjectCategory category)
        {
            activeCategory = category;
            foreach (var tab in tabs.Children())
                tab.EnableInClassList("category-tab--active", tab.name == $"tab-{category}");

            items.Clear();
            itemButtons.Clear();
            foreach (var definition in editor.World.Database.Definitions)
            {
                if (definition == null || definition.Category != category) continue;
                items.Add(CreateItem(definition));
            }
            SetActiveItem(editor.PlacingDefinition);
        }

        private Button CreateItem(ObjectDefinition definition)
        {
            var button = new Button(() => OnItemClicked(definition)) { tooltip = $"Place {definition.DisplayName}" };
            button.AddToClassList("library-item");

            var icon = new VisualElement();
            icon.AddToClassList("library-item__icon");
            if (definition.Icon != null) icon.style.backgroundImage = new StyleBackground(definition.Icon);
            button.Add(icon);

            var label = new Label(definition.DisplayName);
            label.AddToClassList("library-item__label");
            button.Add(label);

            itemButtons[definition] = button;
            return button;
        }

        private void OnItemClicked(ObjectDefinition definition)
        {
            // Clicking the active item again stops placing.
            if (editor.PlacingDefinition == definition) editor.CancelPlacing();
            else editor.StartPlacing(definition);
        }

        private static string Pretty(ObjectCategory category) =>
            category == ObjectCategory.LivingThings ? "Living" : category.ToString();

        public void Dispose()
        {
            itemButtons.Clear();
        }
    }
}

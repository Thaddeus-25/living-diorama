using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace LivingDiorama.UI
{
    /// <summary>
    /// One modal dialog (defined in EditorHud.uxml) reused for prompts, confirmations and lists.
    /// A callback may open another dialog (e.g. "replace existing file?"); otherwise the dialog closes.
    /// </summary>
    public sealed class DialogPanel
    {
        public readonly struct ListItem
        {
            public readonly string Label;
            public readonly string Detail;
            public readonly Action OnPick;

            public ListItem(string label, string detail, Action onPick)
            {
                Label = label;
                Detail = detail;
                OnPick = onPick;
            }
        }

        private readonly VisualElement overlay;
        private readonly Label title;
        private readonly Label message;
        private readonly TextField nameField;
        private readonly ScrollView list;
        private readonly Button confirmButton;
        private readonly Button cancelButton;

        private Func<string, bool> onConfirm;
        // Incremented by every Show*, so we can tell whether a callback opened a follow-up dialog.
        private int showCount;

        public DialogPanel(VisualElement root)
        {
            overlay = root.Q("dialog-overlay");
            title = root.Q<Label>("dialog-title");
            message = root.Q<Label>("dialog-message");
            nameField = root.Q<TextField>("dialog-name");
            list = root.Q<ScrollView>("dialog-list");
            confirmButton = root.Q<Button>("dialog-confirm");
            cancelButton = root.Q<Button>("dialog-cancel");

            confirmButton.clicked += Confirm;
            cancelButton.clicked += Close;
            Close();
        }

        public bool IsOpen { get; private set; }

        /// <summary>Asks for a name. Return false from <paramref name="onAccept"/> to keep the dialog open.</summary>
        public void ShowPrompt(string heading, string text, string initialValue, string confirmLabel, Func<string, bool> onAccept)
        {
            Open(heading, text, confirmLabel, danger: false, onAccept);
            nameField.style.display = DisplayStyle.Flex;
            nameField.SetValueWithoutNotify(initialValue ?? string.Empty);
            // Focus after layout so the caret lands in the field.
            nameField.schedule.Execute(() =>
            {
                nameField.Focus();
                nameField.SelectAll();
            });
        }

        public void ShowConfirm(string heading, string text, string confirmLabel, bool danger, Action onAccept)
        {
            Open(heading, text, confirmLabel, danger, _ =>
            {
                onAccept();
                return true;
            });
        }

        public void ShowList(string heading, string text, IReadOnlyList<ListItem> items, string emptyText)
        {
            Open(heading, text, confirmLabel: null, danger: false, onAccept: null);
            list.style.display = DisplayStyle.Flex;

            if (items.Count == 0)
            {
                var empty = new Label(emptyText);
                empty.AddToClassList("dialog-empty");
                list.Add(empty);
                return;
            }

            foreach (var item in items)
            {
                var pick = item.OnPick;
                var row = new Button(() => RunAndMaybeClose(() => pick())) { name = "dialog-item" };
                row.AddToClassList("dialog-item");
                var label = new Label(item.Label);
                label.AddToClassList("dialog-item__label");
                var detail = new Label(item.Detail);
                detail.AddToClassList("dialog-item__detail");
                row.Add(label);
                row.Add(detail);
                list.Add(row);
            }
        }

        /// <summary>Changes the message while open (e.g. to explain why a name was rejected).</summary>
        public void SetMessage(string text, bool isError)
        {
            message.text = text;
            message.EnableInClassList("dialog-message--error", isError);
        }

        public void Confirm()
        {
            if (!IsOpen || onConfirm == null) return;
            var accept = onConfirm;
            int before = showCount;
            bool close = accept(nameField.value);
            if (close && before == showCount) Close();
        }

        public void Close()
        {
            IsOpen = false;
            onConfirm = null;
            overlay.style.display = DisplayStyle.None;
            list.Clear();
        }

        private void Open(string heading, string text, string confirmLabel, bool danger, Func<string, bool> onAccept)
        {
            showCount++;
            IsOpen = true;
            onConfirm = onAccept;
            overlay.style.display = DisplayStyle.Flex;
            title.text = heading;
            SetMessage(text, isError: false);
            message.style.display = string.IsNullOrEmpty(text) ? DisplayStyle.None : DisplayStyle.Flex;

            nameField.style.display = DisplayStyle.None;
            list.Clear();
            list.style.display = DisplayStyle.None;

            confirmButton.style.display = confirmLabel != null ? DisplayStyle.Flex : DisplayStyle.None;
            confirmButton.text = confirmLabel ?? string.Empty;
            confirmButton.EnableInClassList("danger", danger);
            confirmButton.EnableInClassList("accent", !danger);
        }

        private void RunAndMaybeClose(Action action)
        {
            int before = showCount;
            action();
            if (before == showCount) Close();
        }
    }

    /// <summary>A short message at the bottom of the screen that fades out on its own.</summary>
    public sealed class Toast
    {
        private readonly Label label;
        private float hideAt;

        public Toast(Label label)
        {
            this.label = label;
            label.style.opacity = 0f;
        }

        public string Text => label.text;

        public void Show(string text, bool isError = false, float seconds = 4f)
        {
            label.text = text;
            label.EnableInClassList("toast--error", isError);
            label.style.opacity = 1f;
            hideAt = Time.unscaledTime + seconds;
        }

        public void Tick()
        {
            if (label.resolvedStyle.opacity > 0f && Time.unscaledTime > hideAt) label.style.opacity = 0f;
        }
    }
}

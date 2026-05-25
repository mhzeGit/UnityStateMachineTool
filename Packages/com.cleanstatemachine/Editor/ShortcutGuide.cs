using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace CleanStateMachine
{
    internal class ShortcutGuide
    {
        private readonly CleanStateMachineWindow _window;
        private VisualElement _overlay;
        private bool _isVisible;

        public ShortcutGuide(CleanStateMachineWindow window)
        {
            _window = window;
        }

        public bool IsVisible => _isVisible;

        public void Show()
        {
            if (_isVisible) return;
            _isVisible = true;

            _overlay = new VisualElement();
            _overlay.style.position = Position.Absolute;
            _overlay.style.left = 0;
            _overlay.style.top = 0;
            _overlay.style.right = 0;
            _overlay.style.bottom = 0;
            _overlay.style.backgroundColor = new Color(0, 0, 0, 0.5f);
            _overlay.pickingMode = PickingMode.Position;
            _overlay.focusable = true;

            var container = new VisualElement();
            container.style.position = Position.Absolute;
            container.style.left = Length.Percent(20);
            container.style.right = Length.Percent(20);
            container.style.top = Length.Percent(10);
            container.style.bottom = Length.Percent(10);
            container.style.backgroundColor = new Color(0.18f, 0.18f, 0.18f);
            container.style.borderTopLeftRadius = 6;
            container.style.borderTopRightRadius = 6;
            container.style.borderBottomLeftRadius = 6;
            container.style.borderBottomRightRadius = 6;
            container.style.flexDirection = FlexDirection.Column;
            container.style.paddingLeft = 16;
            container.style.paddingRight = 16;
            container.style.paddingTop = 12;
            container.style.paddingBottom = 12;

            var headerRow = new VisualElement();
            headerRow.style.flexDirection = FlexDirection.Row;
            headerRow.style.justifyContent = Justify.SpaceBetween;
            headerRow.style.alignItems = Align.Center;
            headerRow.style.marginBottom = 8;
            headerRow.style.flexShrink = 0;

            var title = new Label("Keyboard Shortcuts");
            title.style.fontSize = 18;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.color = new Color(0.95f, 0.95f, 0.95f);
            headerRow.Add(title);

            var closeBtn = new Button(() => Hide());
            closeBtn.text = "X";
            closeBtn.style.width = 28;
            closeBtn.style.height = 28;
            closeBtn.style.backgroundColor = new Color(0.3f, 0.3f, 0.3f);
            closeBtn.style.color = new Color(0.8f, 0.8f, 0.8f);
            closeBtn.style.fontSize = 14;
            closeBtn.style.unityFontStyleAndWeight = FontStyle.Bold;
            closeBtn.style.justifyContent = Justify.Center;
            closeBtn.style.alignItems = Align.Center;
            closeBtn.style.borderTopLeftRadius = 4;
            closeBtn.style.borderTopRightRadius = 4;
            closeBtn.style.borderBottomLeftRadius = 4;
            closeBtn.style.borderBottomRightRadius = 4;
            closeBtn.style.paddingLeft = 0;
            closeBtn.style.paddingRight = 0;
            closeBtn.style.flexShrink = 0;
            headerRow.Add(closeBtn);

            container.Add(headerRow);

            var scroll = new ScrollView();
            scroll.style.flexGrow = 1;
            scroll.style.paddingLeft = 4;
            scroll.style.paddingRight = 4;

            var shortcuts = GetShortcuts();
            foreach (var group in shortcuts)
            {
                var groupLabel = new Label(group.Category);
                groupLabel.style.fontSize = 13;
                groupLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
                groupLabel.style.color = new Color(0.6f, 0.8f, 1.0f);
                groupLabel.style.marginTop = 8;
                groupLabel.style.marginBottom = 4;
                groupLabel.style.paddingLeft = 4;
                groupLabel.style.flexShrink = 0;
                scroll.Add(groupLabel);

                foreach (var sc in group.Items)
                {
                    var row = new VisualElement();
                    row.style.flexDirection = FlexDirection.Row;
                    row.style.alignItems = Align.Center;
                    row.style.paddingLeft = 8;
                    row.style.paddingRight = 8;
                    row.style.paddingTop = 4;
                    row.style.paddingBottom = 4;
                    row.style.minHeight = 28;
                    row.style.borderBottomColor = new Color(0.1f, 0.1f, 0.1f);
                    row.style.borderBottomWidth = 1;
                    row.style.flexShrink = 0;

                    var keyLabel = new Label(sc.Key);
                    keyLabel.style.width = 160;
                    keyLabel.style.flexShrink = 0;
                    keyLabel.style.fontSize = 12;
                    keyLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
                    keyLabel.style.color = new Color(1f, 1f, 0.6f);
                    keyLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
                    row.Add(keyLabel);

                    var descLabel = new Label(sc.Description);
                    descLabel.style.flexGrow = 1;
                    descLabel.style.fontSize = 12;
                    descLabel.style.color = new Color(0.8f, 0.8f, 0.8f);
                    descLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
                    row.Add(descLabel);

                    scroll.Add(row);
                }
            }

            container.Add(scroll);

            _overlay.Add(container);

            _overlay.RegisterCallback<ClickEvent>(evt =>
            {
                if (evt.target == _overlay)
                    Hide();
            });

            _overlay.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode == KeyCode.Escape)
                {
                    Hide();
                    evt.StopPropagation();
                }
            });

            _window.rootVisualElement.Add(_overlay);
            _overlay.schedule.Execute(() => _overlay.Focus()).StartingIn(10);
        }

        public void Hide()
        {
            if (!_isVisible) return;
            _isVisible = false;
            if (_overlay?.parent != null)
                _overlay.RemoveFromHierarchy();
            _overlay = null;
        }

        private struct ShortcutGroup
        {
            public string Category;
            public List<ShortcutItem> Items;
        }

        private struct ShortcutItem
        {
            public string Key;
            public string Description;
        }

        private static List<ShortcutGroup> GetShortcuts()
        {
            return new List<ShortcutGroup>
            {
                new ShortcutGroup
                {
                    Category = "General",
                    Items = new List<ShortcutItem>
                    {
                        new ShortcutItem { Key = "Ctrl + S", Description = "Save current state machine" },
                        new ShortcutItem { Key = "Ctrl + Z", Description = "Undo last action" },
                        new ShortcutItem { Key = "Ctrl + Y", Description = "Redo last action" },
                        new ShortcutItem { Key = "Ctrl + / ?", Description = "Show this shortcut guide" },
                    }
                },
                new ShortcutGroup
                {
                    Category = "States",
                    Items = new List<ShortcutItem>
                    {
                        new ShortcutItem { Key = "Ctrl + C", Description = "Copy selected states" },
                        new ShortcutItem { Key = "Ctrl + V", Description = "Paste states" },
                        new ShortcutItem { Key = "Ctrl + D", Description = "Duplicate selected states" },
                        new ShortcutItem { Key = "Ctrl + G", Description = "Group selected states into a comment box" },
                        new ShortcutItem { Key = "F2", Description = "Rename selected state or group" },
                        new ShortcutItem { Key = "Delete / Backspace", Description = "Delete selected items" },
                        new ShortcutItem { Key = "C", Description = "Start connection from selected state" },
                        new ShortcutItem { Key = "B", Description = "Toggle breakpoint on selected states" },
                    }
                },
                new ShortcutGroup
                {
                    Category = "Navigation",
                    Items = new List<ShortcutItem>
                    {
                        new ShortcutItem { Key = "Enter", Description = "Enter or exit a sub-state-machine" },
                        new ShortcutItem { Key = "Escape", Description = "Cancel connection / Exit expanded view" },
                        new ShortcutItem { Key = "Shift + Click", Description = "Toggle multiple selection" },
                        new ShortcutItem { Key = "Right-click + Drag", Description = "Pan the graph view" },
                        new ShortcutItem { Key = "Scroll Wheel", Description = "Zoom in / out" },
                        new ShortcutItem { Key = "Left-click + Drag", Description = "Box selection / Move items" },
                    }
                },
            };
        }
    }
}

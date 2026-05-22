using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace CleanStateMachine
{
    public class BlackboardPanel : VisualElement
    {
        private readonly CleanStateMachineWindow _window;
        private List<BlackboardVariable> _variables;
        private readonly ScrollView _scrollView;
        private readonly List<VisualElement> _rows = new();
        private int _editingIndex = -1;
        private int _dragIndex = -1;
        private bool _isDragging;
        private bool _focusNameField;

        private static readonly string[] TypeLabels =
            { "bool", "int", "float", "string", "V2", "V3" };

        public BlackboardPanel(CleanStateMachineWindow window)
        {
            _window = window;
            AddToClassList("blackboard-panel");

            var header = new VisualElement();
            header.AddToClassList("panel-header");

            var title = new Label("Blackboard");
            title.AddToClassList("panel-title");
            header.Add(title);

            var addBtn = new Button(OnAddVariable);
            addBtn.text = "+";
            addBtn.AddToClassList("add-button");
            header.Add(addBtn);
            Add(header);

            _scrollView = new ScrollView();
            _scrollView.AddToClassList("blackboard-scroll");
            Add(_scrollView);
        }

        public void UpdateVariables(List<BlackboardVariable> variables)
        {
            _variables = variables;
            Rebuild();
        }

        private void Rebuild()
        {
            _scrollView.Clear();
            _rows.Clear();

            if (_variables == null) return;

            for (int i = 0; i < _variables.Count; i++)
            {
                int index = i;
                var row = CreateRow(_variables[i], index);
                _scrollView.Add(row);
                _rows.Add(row);
            }
        }

        private VisualElement CreateRow(BlackboardVariable variable, int index)
        {
            var row = new VisualElement();
            row.AddToClassList("variable-row");
            row.userData = index;

            // Drag handle
            var handle = new Label("\u2022\u2022");
            handle.AddToClassList("drag-handle");
            handle.RegisterCallback<MouseDownEvent>(e => OnHandleDown(e, index));
            row.Add(handle);

            // Type badge
            var badge = new Label(GetTypeLabel(variable.Type));
            badge.AddToClassList("type-badge");
            row.Add(badge);

            // Name label (or input when editing)
            var nameContainer = new VisualElement();
            nameContainer.AddToClassList("variable-name");

            var nameLabel = new Label(variable.Name);
            nameLabel.AddToClassList("variable-name");

            var nameInput = new TextField();
            nameInput.AddToClassList("variable-name-input");
            nameInput.value = variable.Name;
            nameInput.style.display = DisplayStyle.None;
            nameInput.RegisterCallback<FocusOutEvent>(e => OnNameEditEnd(index, nameInput));
            nameInput.RegisterCallback<KeyDownEvent>(e =>
            {
                if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter || e.keyCode == KeyCode.Escape)
                {
                    OnNameEditEnd(index, nameInput);
                    e.StopPropagation();
                }
            });

            nameContainer.Add(nameLabel);
            nameContainer.Add(nameInput);

            nameLabel.RegisterCallback<MouseDownEvent>(e =>
            {
                if (e.clickCount == 2 && _editingIndex < 0)
                {
                    _editingIndex = index;
                    nameLabel.style.display = DisplayStyle.None;
                    nameInput.style.display = DisplayStyle.Flex;
                    nameInput.Focus();
                    nameInput.SelectAll();
                    e.StopPropagation();
                }
            });

            row.Add(nameContainer);

            if (variable.Type == BlackboardVariableType.Bool)
            {
                var toggleContainer = new VisualElement();
                toggleContainer.AddToClassList("variable-value-toggle");
                var toggle = new Toggle();
                toggle.value = variable.BoolValue;
                toggle.RegisterValueChangedCallback(e =>
                {
                    variable.BoolValue = e.newValue;
                    _window.NotifySidePanelChanged();
                });
                toggleContainer.Add(toggle);
                row.Add(toggleContainer);
            }
            else
            {
                var valueField = new TextField();
                valueField.AddToClassList("variable-value");
                valueField.value = variable.StringValue;
                valueField.RegisterValueChangedCallback(e =>
                {
                    variable.StringValue = e.newValue;
                    _window.NotifySidePanelChanged();
                });
                row.Add(valueField);
            }

            // Delete button
            var deleteBtn = new Label("\u00D7");
            deleteBtn.AddToClassList("variable-delete");
            deleteBtn.RegisterCallback<ClickEvent>(e =>
            {
                if (_variables == null) return;
                _variables.RemoveAt(index);
                _window.NotifySidePanelChanged();
                Rebuild();
                e.StopPropagation();
            });
            row.Add(deleteBtn);

            // Right-click context menu
            row.RegisterCallback<ContextClickEvent>(e =>
            {
                var menu = new GenericMenu();
                int captured = index;
                menu.AddItem(new GUIContent("Delete Variable"), false, () =>
                {
                    if (_variables == null) return;
                    _variables.RemoveAt(captured);
                    _window.NotifySidePanelChanged();
                    Rebuild();
                });
                menu.ShowAsContext();
                e.StopPropagation();
            });

            // Click to select
            row.RegisterCallback<MouseDownEvent>(e =>
            {
                if (e.button == 0)
                {
                    ClearRowSelection();
                    row.AddToClassList("variable-row-selected");
                }
            });

            return row;
        }

        private void ClearRowSelection()
        {
            foreach (var r in _rows)
                r.RemoveFromClassList("variable-row-selected");
        }

        private void OnNameEditEnd(int index, TextField input)
        {
            if (index < 0 || _variables == null || index >= _variables.Count)
                return;

            string newName = input.value;
            if (!string.IsNullOrEmpty(newName) && newName != _variables[index].Name)
            {
                _variables[index].Name = newName;
                _window.NotifySidePanelChanged();
            }

            _editingIndex = -1;

            if (index < _rows.Count)
            {
                var row = _rows[index];
                var nameContainer = row.Q<VisualElement>(className: "variable-name");
                if (nameContainer != null && nameContainer.childCount >= 2)
                {
                    var label = nameContainer[0];
                    var textField = nameContainer[1];
                    if (label is Label nameLbl)
                    {
                        nameLbl.text = newName;
                        label.style.display = DisplayStyle.Flex;
                    }
                    textField.style.display = DisplayStyle.None;
                }
            }
        }

        private void OnHandleDown(MouseDownEvent evt, int index)
        {
            if (_variables == null || _variables.Count <= 1) return;
            _dragIndex = index;
            _isDragging = true;
            _rows[index].AddToClassList("variable-row-drag");
            this.RegisterCallback<MouseMoveEvent>(OnDragMove);
            this.RegisterCallback<MouseUpEvent>(OnDragUp);
            evt.StopPropagation();
        }

        private void OnDragMove(MouseMoveEvent evt)
        {
            if (!_isDragging || _variables == null) return;

            Vector2 localPos = this.WorldToLocal(evt.mousePosition);
            float rowHeight = 32f;
            int targetIndex = Mathf.Clamp(
                Mathf.FloorToInt((localPos.y + _scrollView.scrollOffset.y) / rowHeight),
                0, _variables.Count - 1);

            if (targetIndex != _dragIndex)
            {
                var item = _variables[_dragIndex];
                _variables.RemoveAt(_dragIndex);
                _variables.Insert(targetIndex, item);
                _window.NotifySidePanelChanged();
                Rebuild();

                if (targetIndex < _rows.Count)
                    _rows[targetIndex].AddToClassList("variable-row-drag");
                _dragIndex = targetIndex;
            }

            evt.StopPropagation();
        }

        private void OnDragUp(MouseUpEvent evt)
        {
            _isDragging = false;
            this.UnregisterCallback<MouseMoveEvent>(OnDragMove);
            this.UnregisterCallback<MouseUpEvent>(OnDragUp);

            if (_dragIndex >= 0 && _dragIndex < _rows.Count)
                _rows[_dragIndex].RemoveFromClassList("variable-row-drag");
            _dragIndex = -1;
            evt.StopPropagation();
        }

        private void OnAddVariable()
        {
            if (_variables == null) return;

            var v = new BlackboardVariable
            {
                Name = GetUniqueName("New Variable"),
                Type = BlackboardVariableType.Float,
                StringValue = "0"
            };
            _variables.Add(v);
            _window.NotifySidePanelChanged();
            Rebuild();

            if (_rows.Count > 0)
                _scrollView.scrollOffset = new Vector2(0, float.MaxValue);
        }

        private string GetUniqueName(string baseName)
        {
            if (_variables == null) return baseName;
            if (!_variables.Exists(x => x.Name == baseName))
                return baseName;

            for (int i = 1; i < 1000; i++)
            {
                string candidate = $"{baseName} {i}";
                if (!_variables.Exists(x => x.Name == candidate))
                    return candidate;
            }
            return baseName;
        }

        private static string GetTypeLabel(BlackboardVariableType type)
        {
            int idx = (int)type;
            return idx >= 0 && idx < TypeLabels.Length ? TypeLabels[idx] : type.ToString();
        }
    }
}

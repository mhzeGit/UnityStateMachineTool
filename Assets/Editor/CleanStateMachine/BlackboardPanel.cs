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
        private int _dragStartIndex = -1;
        private int _dragIndex = -1;
        private bool _isDragging;

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
                var row = CreateRow(_variables[i], i);
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
            var handle = new Label("\u22EE\u22EE");
            handle.AddToClassList("drag-handle");
            int captured = index;
            handle.RegisterCallback<MouseDownEvent>(e => OnHandleDown(e, captured));
            row.Add(handle);

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

            // Right-aligned group: value
            var rightGroup = new VisualElement();
            rightGroup.AddToClassList("variable-right");

            if (variable.Type == BlackboardVariableType.Bool)
            {
                var toggleContainer = new VisualElement();
                toggleContainer.AddToClassList("variable-value-toggle");
                var toggle = new Toggle();
                toggle.value = variable.BoolValue;
                toggle.RegisterValueChangedCallback(e =>
                {
                    var cmd = new ModifyBlackboardVariableCommand(
                        variable, e.previousValue.ToString(), e.newValue.ToString());
                    _window.UndoRedoSystem.Execute(cmd);
                    _window.NotifySidePanelChanged();
                });
                toggleContainer.Add(toggle);
                rightGroup.Add(toggleContainer);
            }
            else
            {
                var valueField = new TextField();
                valueField.AddToClassList("variable-value");
                valueField.value = variable.StringValue;
                valueField.RegisterValueChangedCallback(e =>
                {
                    var cmd = new ModifyBlackboardVariableCommand(
                        variable, e.previousValue, e.newValue);
                    _window.UndoRedoSystem.Execute(cmd);
                    _window.NotifySidePanelChanged();
                });
                rightGroup.Add(valueField);
            }

            row.Add(rightGroup);

            // Right-click context menu
            row.RegisterCallback<ContextClickEvent>(e =>
            {
                var menu = new GenericMenu();
                int capturedIdx = index;
                menu.AddItem(new GUIContent("Delete Variable"), false, () =>
                {
                    if (_variables == null) return;
                    var cmd = new DeleteBlackboardVariableCommand(_variables, capturedIdx);
                    _window.UndoRedoSystem.Execute(cmd);
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
            _isDragging = true;
            _dragStartIndex = index;
            _dragIndex = index;
            _rows[index].AddToClassList("variable-row-drag");
            this.RegisterCallback<MouseMoveEvent>(OnDragMove);
            this.RegisterCallback<MouseUpEvent>(OnDragUp);
            evt.StopPropagation();
        }

        private void OnDragMove(MouseMoveEvent evt)
        {
            if (!_isDragging || _variables == null) return;

            Vector2 localPos = this.WorldToLocal(evt.mousePosition);
            float rowHeight = 30f;
            int targetIndex = Mathf.Clamp(
                Mathf.FloorToInt((localPos.y + _scrollView.scrollOffset.y) / rowHeight),
                0, _variables.Count - 1);

            if (targetIndex == _dragIndex) return;

            // Move row element in the scroll view (no rebuild)
            var row = _rows[_dragIndex];
            _scrollView.Remove(row);

            if (targetIndex >= _scrollView.childCount)
                _scrollView.Add(row);
            else
                _scrollView.Insert(targetIndex, row);

            _rows.RemoveAt(_dragIndex);
            _rows.Insert(targetIndex, row);

            _dragIndex = targetIndex;
            evt.StopPropagation();
        }

        private void OnDragUp(MouseUpEvent evt)
        {
            _isDragging = false;
            this.UnregisterCallback<MouseMoveEvent>(OnDragMove);
            this.UnregisterCallback<MouseUpEvent>(OnDragUp);

            if (_dragIndex >= 0 && _dragIndex < _rows.Count)
                _rows[_dragIndex].RemoveFromClassList("variable-row-drag");

            if (_dragStartIndex >= 0 && _dragStartIndex != _dragIndex && _variables != null)
            {
                var item = _variables[_dragStartIndex];
                _variables.RemoveAt(_dragStartIndex);
                _variables.Insert(_dragIndex, item);
                _window.NotifySidePanelChanged();
                Rebuild();
            }

            _dragStartIndex = -1;
            _dragIndex = -1;
            evt.StopPropagation();
        }

        private void OnAddVariable()
        {
            if (_variables == null) return;

            var menu = new GenericMenu();
            foreach (BlackboardVariableType type in System.Enum.GetValues(typeof(BlackboardVariableType)))
            {
                BlackboardVariableType capturedType = type;
                string label = ObjectNames.NicifyVariableName(type.ToString());
                menu.AddItem(new GUIContent(label), false, () => AddVariable(capturedType));
            }
            menu.ShowAsContext();
        }

        private void AddVariable(BlackboardVariableType type)
        {
            if (_variables == null) return;

            var v = new BlackboardVariable
            {
                Name = GetUniqueName("New Variable"),
                Type = type,
                StringValue = type == BlackboardVariableType.Bool ? "False" : "0"
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
    }
}

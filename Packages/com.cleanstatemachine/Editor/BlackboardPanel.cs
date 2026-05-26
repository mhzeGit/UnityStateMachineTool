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
        private int _selectedIndex = -1;
        private int _lastClickIndex = -1;
        private float _lastClickTime;
        private const float DoubleClickTime = 0.35f;
        private bool _isMouseOver;
        private int _dragStartIndex = -1;
        private int _dragIndex = -1;
        private bool _isDragging;
        private Vector2 _dragMouseStartPos;
        private bool _dragPastThreshold;
        private const float DragThreshold = 5f;
        private const float AutoScrollEdgeThreshold = 20f;
        private const float AutoScrollSpeed = 30f;
        private const float RowHeight = 30f;

        public BlackboardPanel(CleanStateMachineWindow window)
        {
            _window = window;
            AddToClassList("blackboard-panel");

            var header = new VisualElement();
            header.AddToClassList("panel-header");

            var title = new Label("Blackboard");
            title.AddToClassList("panel-title");
            header.Add(title);

            var addBtn = new Button();
            addBtn.text = "+";
            addBtn.AddToClassList("add-button");
            addBtn.clicked += () =>
            {
                var pos = _window.rootVisualElement.WorldToLocal(
                    new Vector2(addBtn.worldBound.x, addBtn.worldBound.y + addBtn.worldBound.height));
                MenuDropdown.Show(_window.rootVisualElement, pos, menu =>
                {
                    foreach (BlackboardVariableType type in System.Enum.GetValues(typeof(BlackboardVariableType)))
                    {
                        BlackboardVariableType capturedType = type;
                        string label = ObjectNames.NicifyVariableName(type.ToString());
                        menu.AddItem(label, () => AddVariable(capturedType));
                    }
                });
            };
            header.Add(addBtn);
            Add(header);

            _scrollView = new ScrollView();
            _scrollView.AddToClassList("blackboard-scroll");
            Add(_scrollView);

            focusable = true;
            RegisterCallback<MouseEnterEvent>(e => _isMouseOver = true);
            RegisterCallback<MouseLeaveEvent>(e => _isMouseOver = false);
            RegisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);
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
            var handle = new Label("\u2807");
            handle.AddToClassList("drag-handle");
            handle.RegisterCallback<MouseDownEvent>(OnHandleDown);
            row.Add(handle);

            // Name — Label for display, TextField for editing (swapped on double-click)
            var nameContainer = new VisualElement();
            nameContainer.AddToClassList("variable-name");

            var nameLabel = new Label(variable.Name);
            nameLabel.AddToClassList("variable-name-label");

            var nameInput = new TextField();
            nameInput.AddToClassList("variable-name-input");
            nameInput.value = variable.Name;
            nameInput.style.display = DisplayStyle.None;

            nameInput.RegisterCallback<KeyDownEvent>(e =>
            {
                if (_editingIndex != index) return;
                if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)
                {
                    CommitNameEdit(index);
                    e.StopPropagation();
                }
                else if (e.keyCode == KeyCode.Escape)
                {
                    CancelNameEdit(index);
                    e.StopPropagation();
                }
            });

            nameInput.RegisterCallback<FocusOutEvent>(e =>
            {
                if (_editingIndex == index)
                    CommitNameEdit(index);
            });

            nameContainer.Add(nameLabel);
            nameContainer.Add(nameInput);
            row.Add(nameContainer);

            // Right-aligned group: value
            var rightGroup = new VisualElement();
            rightGroup.AddToClassList("variable-right");

            if (variable.Type == BlackboardVariableType.Bool || variable.Type == BlackboardVariableType.Trigger)
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
                switch (variable.Type)
                {
                    case BlackboardVariableType.Int:
                    {
                        var intField = new IntegerField();
                        intField.AddToClassList("variable-value");
                        intField.value = variable.IntValue;
                        intField.RegisterValueChangedCallback(e =>
                        {
                            var cmd = new ModifyBlackboardVariableCommand(
                                variable, e.previousValue.ToString(), e.newValue.ToString());
                            _window.UndoRedoSystem.Execute(cmd);
                            _window.NotifySidePanelChanged();
                        });
                        rightGroup.Add(intField);
                        break;
                    }
                    case BlackboardVariableType.Float:
                    {
                        var floatField = new FloatField();
                        floatField.AddToClassList("variable-value");
                        floatField.value = variable.FloatValue;
                        floatField.RegisterValueChangedCallback(e =>
                        {
                            var cmd = new ModifyBlackboardVariableCommand(
                                variable, e.previousValue.ToString("G"), e.newValue.ToString("G"));
                            _window.UndoRedoSystem.Execute(cmd);
                            _window.NotifySidePanelChanged();
                        });
                        rightGroup.Add(floatField);
                        break;
                    }
                    case BlackboardVariableType.String:
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
                        break;
                    }
                }
            }

            row.Add(rightGroup);

            // Right-click context menu
            row.RegisterCallback<ContextClickEvent>(e =>
            {
                int idx = (int)row.userData;
                MenuDropdown.Show(_window.rootVisualElement, _window.rootVisualElement.WorldToLocal(e.mousePosition), menu =>
                {
                    menu.AddItem("Delete Variable", new Color(0.85f, 0.2f, 0.2f), () =>
                    {
                        if (_variables == null || idx < 0 || idx >= _variables.Count) return;
                        var cmd = new DeleteBlackboardVariableCommand(_variables, idx);
                        _window.UndoRedoSystem.Execute(cmd);
                        Rebuild();
                    });
                });
                e.StopPropagation();
            });

            // Click to select (or double-click to rename, matching StateView pattern)
            row.RegisterCallback<MouseDownEvent>(e =>
            {
                if (e.button == 0)
                {
                    if (_editingIndex < 0)
                    {
                        float now = (float)EditorApplication.timeSinceStartup;
                        if (_lastClickIndex == index && (now - _lastClickTime) < DoubleClickTime)
                        {
                            _lastClickIndex = -1;
                            StartNameEdit(index);
                            e.StopPropagation();
                            return;
                        }
                        _lastClickIndex = index;
                        _lastClickTime = now;
                    }

                    ClearRowSelection();
                    row.AddToClassList("variable-row-selected");
                    _selectedIndex = index;
                    Focus();
                }
            });

            return row;
        }

        private void ClearRowSelection()
        {
            foreach (var r in _rows)
                r.RemoveFromClassList("variable-row-selected");
        }

        private void StartNameEdit(int index)
        {
            if (index < 0 || _variables == null || index >= _variables.Count)
                return;

            var row = _rows[index];
            var nameLabel = row.Q<Label>(className: "variable-name-label");
            var nameInput = row.Q<TextField>(className: "variable-name-input");
            if (nameLabel == null || nameInput == null) return;

            _editingIndex = index;
            nameInput.value = _variables[index].Name;
            nameLabel.style.display = DisplayStyle.None;
            nameInput.style.display = DisplayStyle.Flex;
            nameInput.schedule.Execute(() =>
            {
                nameInput.Focus();
                nameInput.SelectAll();
            }).StartingIn(0);
        }

        private void CommitNameEdit(int index)
        {
            if (_editingIndex != index) return;
            if (index < 0 || _variables == null || index >= _variables.Count)
                return;

            var row = _rows[index];
            var nameLabel = row.Q<Label>(className: "variable-name-label");
            var nameInput = row.Q<TextField>(className: "variable-name-input");
            if (nameLabel == null || nameInput == null) return;

            string newName = nameInput.value;
            if (!string.IsNullOrEmpty(newName) && newName != _variables[index].Name)
            {
                _variables[index].Name = newName;
                _window.NotifySidePanelChanged();
            }

            _editingIndex = -1;
            nameLabel.text = _variables[index].Name;
            nameLabel.style.display = DisplayStyle.Flex;
            nameInput.style.display = DisplayStyle.None;
        }

        private void CancelNameEdit(int index)
        {
            if (_editingIndex != index) return;
            if (index < 0 || _variables == null || index >= _variables.Count)
                return;

            var row = _rows[index];
            var nameLabel = row.Q<Label>(className: "variable-name-label");
            var nameInput = row.Q<TextField>(className: "variable-name-input");
            if (nameLabel == null || nameInput == null) return;

            _editingIndex = -1;
            nameLabel.style.display = DisplayStyle.Flex;
            nameInput.style.display = DisplayStyle.None;
        }

        private void OnHandleDown(MouseDownEvent evt)
        {
            if (_variables == null || _variables.Count <= 1) return;
            var handle = evt.currentTarget as VisualElement;
            int index = (int)handle.parent.userData;
            ClearRowSelection();
            _selectedIndex = -1;
            _isDragging = true;
            _dragPastThreshold = false;
            _dragStartIndex = index;
            _dragIndex = index;
            _dragMouseStartPos = evt.mousePosition;
            _rows[index].AddToClassList("variable-row-drag");
            this.RegisterCallback<MouseMoveEvent>(OnDragMove);
            this.RegisterCallback<MouseUpEvent>(OnDragUp);
            evt.StopPropagation();
        }

        private void OnDragMove(MouseMoveEvent evt)
        {
            if (!_isDragging || _variables == null) return;

            if (!_dragPastThreshold)
            {
                if (Vector2.Distance(evt.mousePosition, _dragMouseStartPos) < DragThreshold)
                    return;
                _dragPastThreshold = true;
            }

            Vector2 scrollViewLocal = _scrollView.WorldToLocal(evt.mousePosition);
            float viewHeight = _scrollView.resolvedStyle.height;
            if (scrollViewLocal.y < AutoScrollEdgeThreshold)
                _scrollView.scrollOffset = new Vector2(0, Mathf.Max(0, _scrollView.scrollOffset.y - AutoScrollSpeed));
            else if (scrollViewLocal.y > viewHeight - AutoScrollEdgeThreshold)
                _scrollView.scrollOffset = new Vector2(0, _scrollView.scrollOffset.y + AutoScrollSpeed);

            Vector2 contentLocal = _scrollView.contentContainer.WorldToLocal(evt.mousePosition);
            int targetIndex = Mathf.Clamp(
                Mathf.FloorToInt(contentLocal.y / RowHeight),
                0, _variables.Count - 1);

            if (targetIndex == _dragIndex) return;

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
            _dragPastThreshold = false;
            this.UnregisterCallback<MouseMoveEvent>(OnDragMove);
            this.UnregisterCallback<MouseUpEvent>(OnDragUp);

            if (_dragIndex >= 0 && _dragIndex < _rows.Count)
                _rows[_dragIndex].RemoveFromClassList("variable-row-drag");

            if (_dragStartIndex >= 0 && _dragStartIndex != _dragIndex && _variables != null)
            {
                var item = _variables[_dragStartIndex];
                _variables.RemoveAt(_dragStartIndex);
                _variables.Insert(_dragIndex, item);

                for (int i = 0; i < _rows.Count; i++)
                    _rows[i].userData = i;

                _window.NotifySidePanelChanged();
            }

            _dragStartIndex = -1;
            _dragIndex = -1;
            evt.StopPropagation();
        }

        private void OnKeyDown(KeyDownEvent e)
        {
            if (e.keyCode == KeyCode.S && e.ctrlKey)
            {
                _window.OnSaveCommand();
                e.StopPropagation();
                return;
            }

            if (e.keyCode == KeyCode.F2)
            {
                if (_editingIndex < 0 && _selectedIndex >= 0)
                {
                    StartNameEdit(_selectedIndex);
                    e.StopPropagation();
                    return;
                }
            }

            if (e.keyCode != KeyCode.Delete && e.keyCode != KeyCode.Backspace) return;
            if (!_isMouseOver) return;
            if (_editingIndex >= 0) return;

            var focused = focusController?.focusedElement as VisualElement;
            if (focused != null && focused != this && this.Contains(focused))
                return;

            if (_selectedIndex < 0 || _selectedIndex >= (_variables?.Count ?? 0)) return;

            var cmd = new DeleteBlackboardVariableCommand(_variables, _selectedIndex);
            _window.UndoRedoSystem.Execute(cmd);
            _selectedIndex = -1;
            Rebuild();
            e.StopPropagation();
        }

        private void AddVariable(BlackboardVariableType type)
        {
            if (_variables == null) return;

            var v = new BlackboardVariable
            {
                Name = GetUniqueName("New Variable"),
                Type = type,
                StringValue = type switch
                {
                    BlackboardVariableType.Bool => "False",
                    BlackboardVariableType.String => "",
                    BlackboardVariableType.Trigger => "False",
                    _ => "0"
                }
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

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
        private int _dragStartIndex = -1;
        private int _dragIndex = -1;
        private bool _isDragging;
        private Vector2 _dragMouseStartPos;
        private bool _dragPastThreshold;
        private const float DragThreshold = 5f;
        private const float AutoScrollEdgeThreshold = 20f;
        private const float AutoScrollSpeed = 30f;
        private const float RowHeight = 30f;
        private IVisualElementScheduledItem _pendingSmoothMove;

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
            var handle = new Label("\u2807");
            handle.AddToClassList("drag-handle");
            handle.RegisterCallback<MouseDownEvent>(OnHandleDown);
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
            nameInput.RegisterCallback<FocusOutEvent>(e => OnNameEditEnd(_editingIndex, nameInput));
            nameInput.RegisterCallback<KeyDownEvent>(e =>
            {
                if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter || e.keyCode == KeyCode.Escape)
                {
                    OnNameEditEnd(_editingIndex, nameInput);
                    e.StopPropagation();
                }
            });

            nameContainer.Add(nameLabel);
            nameContainer.Add(nameInput);

            nameLabel.RegisterCallback<MouseDownEvent>(e =>
            {
                if (e.clickCount == 2 && _editingIndex < 0 && e.button == 0)
                {
                    var rowElement = (e.currentTarget as VisualElement).parent.parent;
                    int idx = (int)rowElement.userData;
                    _editingIndex = idx;
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
                    case BlackboardVariableType.Vector2:
                    {
                        var vectorContainer = new VisualElement();
                        vectorContainer.AddToClassList("variable-value-vector");
                        var v2 = variable.Vector2Value;
                        AddAxisField(vectorContainer, "X", v2.x,
                            newValue => {
                                var current = variable.Vector2Value;
                                UpdateVector2(variable, newValue, current.y);
                            });
                        AddAxisField(vectorContainer, "Y", v2.y,
                            newValue => {
                                var current = variable.Vector2Value;
                                UpdateVector2(variable, current.x, newValue);
                            });
                        rightGroup.Add(vectorContainer);
                        break;
                    }
                    case BlackboardVariableType.Vector3:
                    {
                        var vectorContainer = new VisualElement();
                        vectorContainer.AddToClassList("variable-value-vector");
                        var v3 = variable.Vector3Value;
                        AddAxisField(vectorContainer, "X", v3.x,
                            newValue => {
                                var current = variable.Vector3Value;
                                UpdateVector3(variable, newValue, current.y, current.z);
                            });
                        AddAxisField(vectorContainer, "Y", v3.y,
                            newValue => {
                                var current = variable.Vector3Value;
                                UpdateVector3(variable, current.x, newValue, current.z);
                            });
                        AddAxisField(vectorContainer, "Z", v3.z,
                            newValue => {
                                var current = variable.Vector3Value;
                                UpdateVector3(variable, current.x, current.y, newValue);
                            });
                        rightGroup.Add(vectorContainer);
                        break;
                    }
                }
            }

            row.Add(rightGroup);

            // Right-click context menu
            row.RegisterCallback<ContextClickEvent>(e =>
            {
                var rowElement = e.currentTarget as VisualElement;
                int idx = (int)rowElement.userData;
                var menu = new GenericMenu();
                menu.AddItem(new GUIContent("Delete Variable"), false, () =>
                {
                    if (_variables == null || idx < 0 || idx >= _variables.Count) return;
                    var cmd = new DeleteBlackboardVariableCommand(_variables, idx);
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

        private void OnHandleDown(MouseDownEvent evt)
        {
            if (_variables == null || _variables.Count <= 1) return;
            var handle = evt.currentTarget as VisualElement;
            int index = (int)handle.parent.userData;
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

            _pendingSmoothMove?.Pause();

            // FLIP: record old world positions before layout change
            var oldPositions = new Dictionary<VisualElement, Vector2>(_rows.Count);
            for (int i = 0; i < _rows.Count; i++)
                oldPositions[_rows[i]] = _rows[i].LocalToWorld(Vector2.zero);

            // Perform the remove/insert (layout snaps)
            var row = _rows[_dragIndex];
            _scrollView.Remove(row);

            if (targetIndex >= _scrollView.childCount)
                _scrollView.Add(row);
            else
                _scrollView.Insert(targetIndex, row);

            _rows.RemoveAt(_dragIndex);
            _rows.Insert(targetIndex, row);

            // INVERT: apply inverse translate so rows stay visually where they were
            for (int i = 0; i < _rows.Count; i++)
            {
                Vector2 newWorldPos = _rows[i].LocalToWorld(Vector2.zero);
                if (oldPositions.TryGetValue(_rows[i], out Vector2 oldWorldPos))
                {
                    float deltaY = oldWorldPos.y - newWorldPos.y;
                    if (Mathf.Abs(deltaY) > 0.5f)
                    {
                        _rows[i].AddToClassList("variable-row-no-animate");
                        _rows[i].style.translate = new Translate(0, deltaY);
                    }
                }
            }

            // PLAY: on next frame, remove no-animate class and clear translate to animate back
            _pendingSmoothMove = schedule.Execute(() =>
            {
                for (int i = 0; i < _rows.Count; i++)
                {
                    _rows[i].RemoveFromClassList("variable-row-no-animate");
                    _rows[i].style.translate = new Translate(0, 0);
                }
                _pendingSmoothMove = null;
            }).StartingIn(16);

            _dragIndex = targetIndex;
            evt.StopPropagation();
        }

        private void OnDragUp(MouseUpEvent evt)
        {
            _isDragging = false;
            _dragPastThreshold = false;
            _pendingSmoothMove?.Pause();
            _pendingSmoothMove = null;
            this.UnregisterCallback<MouseMoveEvent>(OnDragMove);
            this.UnregisterCallback<MouseUpEvent>(OnDragUp);

            // Reset all translates immediately (no animation on drop)
            for (int i = 0; i < _rows.Count; i++)
            {
                _rows[i].RemoveFromClassList("variable-row-no-animate");
                _rows[i].style.translate = new Translate(0, 0);
            }

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

        private void AddAxisField(VisualElement parent, string axisName, float initialValue, Action<float> onChanged)
        {
            var container = new VisualElement();
            container.AddToClassList("axis-field");

            var label = new Label(axisName);
            label.AddToClassList("axis-label");
            container.Add(label);

            var field = new FloatField();
            field.AddToClassList("axis-input");
            field.value = initialValue;
            field.RegisterValueChangedCallback(e => onChanged(e.newValue));
            container.Add(field);

            parent.Add(container);
        }

        private void UpdateVector2(BlackboardVariable variable, float x, float y)
        {
            string oldStr = variable.StringValue;
            variable.Vector2Value = new Vector2(x, y);
            var cmd = new ModifyBlackboardVariableCommand(variable, oldStr, variable.StringValue);
            _window.UndoRedoSystem.Execute(cmd);
            _window.NotifySidePanelChanged();
        }

        private void UpdateVector3(BlackboardVariable variable, float x, float y, float z)
        {
            string oldStr = variable.StringValue;
            variable.Vector3Value = new Vector3(x, y, z);
            var cmd = new ModifyBlackboardVariableCommand(variable, oldStr, variable.StringValue);
            _window.UndoRedoSystem.Execute(cmd);
            _window.NotifySidePanelChanged();
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
                StringValue = type switch
                {
                    BlackboardVariableType.Bool => "False",
                    BlackboardVariableType.String => "",
                    BlackboardVariableType.Vector2 => "0,0",
                    BlackboardVariableType.Vector3 => "0,0,0",
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

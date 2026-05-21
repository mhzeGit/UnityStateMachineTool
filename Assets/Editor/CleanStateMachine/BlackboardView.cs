using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CleanStateMachine
{
    public class BlackboardView
    {
        private Vector2 _scrollPos;
        private int _selectedIndex = -1;
        private int _editingIndex = -1;
        private bool _focusNameField;
        private bool _isDragging;
        private int _dragIndex = -1;
        private int _pendingDeleteIndex = -1;

        public event System.Action VariablesChanged;
        public event System.Action RepaintRequested;

        public void Draw(Rect rect, List<BlackboardVariable> variables, bool reserveToggleSpace = true)
        {
            if (variables == null)
                return;

            UITheme.DrawPanelBackground(rect);

            Rect headerRect = new Rect(rect.x, rect.y, rect.width, UITheme.HeaderHeight);
            DrawHeader(headerRect, variables, reserveToggleSpace);

            Rect listRect = new Rect(
                rect.x,
                rect.y + UITheme.HeaderHeight,
                rect.width,
                rect.height - UITheme.HeaderHeight
            );
            DrawVariableList(listRect, variables);
        }

        private void DrawHeader(Rect rect, List<BlackboardVariable> variables, bool reserveToggleSpace = true)
        {
            UITheme.DrawHeaderBackground(rect);

            float toggleSize = reserveToggleSpace ? 20f : 0f;
            float addSize = 24f;
            float gap = 8f;
            float rightEdge = rect.x + rect.width;

            Rect addRect = new Rect(
                rightEdge - toggleSize - addSize - gap,
                rect.y + (rect.height - addSize) * 0.5f,
                addSize,
                addSize
            );

            Rect labelRect = new Rect(rect.x, rect.y, addRect.x - rect.x - 4f, rect.height);
            GUI.Label(labelRect, "Blackboard", UITheme.HeaderStyle);

            var e = Event.current;
            if (e.type == EventType.Repaint)
                UITheme.DrawPlusIcon(addRect,
                    addRect.Contains(e.mousePosition) ? UITheme.TextColor : UITheme.IconColor);

            if (e.type == EventType.MouseDown && e.button == 0 && addRect.Contains(e.mousePosition))
            {
                var menu = new GenericMenu();
                foreach (BlackboardVariableType type in Enum.GetValues(typeof(BlackboardVariableType)))
                {
                    BlackboardVariableType captured = type;
                    string label = ObjectNames.NicifyVariableName(type.ToString());
                    menu.AddItem(new GUIContent(label), false, () =>
                    {
                        variables.Add(new BlackboardVariable
                        {
                            Name = GetUniqueName(variables, "New Variable"),
                            Type = captured
                        });
                        ResetValueForType(variables[variables.Count - 1]);
                        VariablesChanged?.Invoke();
                        _scrollPos.y = float.MaxValue;
                    });
                }
                e.Use();
                menu.DropDown(addRect);
            }
        }

        private void HandleDragBeforeScroll(Rect scrollViewRect, List<BlackboardVariable> variables, Event e)
        {
            if (!_isDragging)
                return;

            if (e.type == EventType.MouseDrag)
            {
                float contentMouseY = e.mousePosition.y - scrollViewRect.y + _scrollPos.y;

                int targetIndex = Mathf.Clamp(
                    Mathf.FloorToInt(contentMouseY / UITheme.RowHeight),
                    0, variables.Count - 1
                );

                if (variables.Count > 1 && targetIndex != _dragIndex)
                {
                    var item = variables[_dragIndex];
                    variables.RemoveAt(_dragIndex);
                    variables.Insert(targetIndex, item);
                    _dragIndex = targetIndex;
                    _selectedIndex = targetIndex;
                    VariablesChanged?.Invoke();
                }

                e.Use();
                return;
            }

            if (e.type == EventType.MouseUp || e.rawType == EventType.MouseUp)
            {
                _isDragging = false;
                _selectedIndex = _dragIndex;
                _dragIndex = -1;
                e.Use();
                return;
            }

            if (e.type != EventType.Layout && e.type != EventType.Repaint)
                Debug.LogWarning($"[BlackboardView] Drag state active but unexpected event type: {e.type}");
        }

        private void DrawVariableList(Rect rect, List<BlackboardVariable> variables)
        {
            var e = Event.current;

            HandleDragBeforeScroll(rect, variables, e);

            float totalHeight = variables.Count * UITheme.RowHeight;
            Rect viewRect = new Rect(0f, 0f, rect.width - 14f, totalHeight);

            _scrollPos = GUI.BeginScrollView(rect, _scrollPos, viewRect);

            if (e.type == EventType.MouseDown && e.button == 0)
            {
                int clickedIndex = Mathf.FloorToInt(e.mousePosition.y / UITheme.RowHeight);
                if (clickedIndex >= 0 && clickedIndex < variables.Count)
                {
                    if (_selectedIndex != clickedIndex)
                    {
                        _selectedIndex = clickedIndex;
                        RepaintRequested?.Invoke();
                    }
                }
                else
                {
                    if (_selectedIndex != -1)
                    {
                        _selectedIndex = -1;
                        _editingIndex = -1;
                        DefocusTextField();
                        RepaintRequested?.Invoke();
                    }
                }
            }

            for (int i = 0; i < variables.Count; i++)
            {
                Rect rowRect = new Rect(0f, i * UITheme.RowHeight, viewRect.width, UITheme.RowHeight);
                DrawVariableRow(rowRect, variables[i], i, variables, (idx) => _pendingDeleteIndex = idx);
            }

            if (_pendingDeleteIndex >= 0)
            {
                int deleteIndex = _pendingDeleteIndex;
                _pendingDeleteIndex = -1;
                variables.RemoveAt(deleteIndex);
                VariablesChanged?.Invoke();
                DefocusTextField();

                if (_dragIndex >= 0)
                {
                    if (_dragIndex == deleteIndex)
                    {
                        _isDragging = false;
                        _dragIndex = -1;
                    }
                    else if (_dragIndex > deleteIndex)
                    {
                        _dragIndex--;
                    }
                }
            }

            if (e.type == EventType.KeyDown &&
                (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter))
            {
                _editingIndex = -1;
                DefocusTextField();
            }

            GUI.EndScrollView();
        }

        private static void DefocusTextField()
        {
            GUIUtility.keyboardControl = 0;
            GUIUtility.hotControl = 0;
            EditorGUIUtility.editingTextField = false;
            GUI.FocusControl("");
        }

        private void DrawVariableRow(Rect rect, BlackboardVariable variable,
            int index, List<BlackboardVariable> variables, Action<int> onDeleteRequested)
        {
            var e = Event.current;

            bool isDragSource = _isDragging && index == _dragIndex;

            if (e.type == EventType.MouseDown && e.button == 1 && rect.Contains(e.mousePosition))
            {
                int captured = index;
                var menu = new GenericMenu();
                menu.AddItem(new GUIContent("Delete Variable"), false, () => onDeleteRequested?.Invoke(captured));
                menu.ShowAsContext();
                e.Use();
            }

            Color rowBg = isDragSource
                ? UITheme.RowBgDrag
                : (index == _selectedIndex && !_isDragging ? UITheme.RowBgSelected : UITheme.RowBg);
            EditorGUI.DrawRect(rect, rowBg);

            if (isDragSource)
            {
                Color accent = UITheme.DropIndicator;
                accent.a = 0.6f;
                EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 1f), accent);
                EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), accent);
            }

            float pad = 8f;
            float fieldH = rect.height - 8f;
            float fieldY = rect.y + 4f;
            float gap = 4f;
            float innerW = rect.width - pad * 2f;
            float handleW = 20f;
            float valueW = innerW * 0.45f;
            float nameW = innerW - handleW - gap - valueW;

            Rect handleRect = new Rect(rect.x + pad, fieldY, handleW, fieldH);
            DrawDragHandle(handleRect, isDragSource);

            if (e.type == EventType.MouseDown && e.button == 0 && handleRect.Contains(e.mousePosition))
            {
                if (_editingIndex < 0 && variables.Count > 1)
                {
                    _isDragging = true;
                    _dragIndex = index;
                }
                e.Use();
            }

            if (e.type == EventType.Repaint && handleRect.Contains(e.mousePosition) && !_isDragging)
                EditorGUIUtility.AddCursorRect(handleRect, MouseCursor.MoveArrow);

            Rect nameRect = new Rect(handleRect.xMax + gap, fieldY, nameW, fieldH);

            if (_editingIndex == index)
            {
                string nameCtrl = $"bb_name_{index}";
                GUI.SetNextControlName(nameCtrl);
                EditorGUI.BeginChangeCheck();
                string newName = EditorGUI.TextField(nameRect, variable.Name, UITheme.RowNameFieldStyle);
                if (EditorGUI.EndChangeCheck())
                {
                    variable.Name = newName;
                    VariablesChanged?.Invoke();
                }

                if (_focusNameField)
                {
                    EditorGUI.FocusTextInControl(nameCtrl);
                    _focusNameField = false;
                }

                if (e.isKey && (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter || e.keyCode == KeyCode.Escape))
                {
                    _editingIndex = -1;
                    DefocusTextField();
                    e.Use();
                }
            }
            else
            {
                GUI.Label(nameRect, variable.Name, UITheme.VariableLabelStyle);

                if (e.type == EventType.MouseDown && e.button == 0 && e.clickCount == 2 && nameRect.Contains(e.mousePosition))
                {
                    _editingIndex = index;
                    _focusNameField = true;
                    e.Use();
                }
            }

            Rect valueRect = new Rect(rect.xMax - pad - valueW, fieldY, valueW, fieldH);
            DrawValueField(valueRect, variable);

            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), UITheme.RowBoundary);
        }

        private void DrawDragHandle(Rect rect, bool isActive)
        {
            int cols = 2;
            int rows = 3;
            float dotSize = 2f;
            float spacingX = 4f;
            float spacingY = 3f;

            float totalW = cols * dotSize + (cols - 1) * spacingX;
            float totalH = rows * dotSize + (rows - 1) * spacingY;
            float startX = rect.x + (rect.width - totalW) * 0.5f;
            float startY = rect.y + (rect.height - totalH) * 0.5f;

            Color dotColor = isActive ? UITheme.TextColor : UITheme.TextMuted;

            for (int row = 0; row < rows; row++)
            {
                for (int col = 0; col < cols; col++)
                {
                    Rect dotRect = new Rect(
                        startX + col * (dotSize + spacingX),
                        startY + row * (dotSize + spacingY),
                        dotSize,
                        dotSize
                    );
                    EditorGUI.DrawRect(dotRect, dotColor);
                }
            }
        }

        private static void ResetValueForType(BlackboardVariable v)
        {
            v.StringValue = v.Type switch
            {
                BlackboardVariableType.Bool => "False",
                BlackboardVariableType.Int => "0",
                BlackboardVariableType.Float => "0",
                BlackboardVariableType.String => "",
                BlackboardVariableType.Vector2 => "0,0",
                BlackboardVariableType.Vector3 => "0,0,0",
                _ => "0"
            };
        }

        private void DrawValueField(Rect rect, BlackboardVariable variable)
        {
            switch (variable.Type)
            {
                case BlackboardVariableType.Bool:
                {
                    bool val = variable.BoolValue;
                    float toggleWidth = 16f;
                    Rect toggleRect = new Rect(
                        rect.x + (rect.width - toggleWidth) * 0.5f,
                        rect.y + (rect.height - 16f) * 0.5f,
                        toggleWidth,
                        16f
                    );
                    bool result = EditorGUI.Toggle(toggleRect, val);
                    if (result != val)
                    {
                        variable.BoolValue = result;
                        GUI.changed = true;
                    }
                    break;
                }
                case BlackboardVariableType.Int:
                {
                    int val = variable.IntValue;
                    int result = EditorGUI.IntField(rect, val);
                    if (result != val)
                    {
                        variable.IntValue = result;
                        GUI.changed = true;
                    }
                    break;
                }
                case BlackboardVariableType.Float:
                {
                    float val = variable.FloatValue;
                    float result = EditorGUI.FloatField(rect, val);
                    if (Mathf.Abs(result - val) > 1e-6f)
                    {
                        variable.FloatValue = result;
                        GUI.changed = true;
                    }
                    break;
                }
                case BlackboardVariableType.String:
                {
                    string result = EditorGUI.TextField(rect, variable.StringValue, UITheme.RowFieldStyle);
                    if (result != variable.StringValue)
                    {
                        variable.StringValue = result;
                        GUI.changed = true;
                    }
                    break;
                }
                case BlackboardVariableType.Vector2:
                {
                    Vector2 val = variable.Vector2Value;
                    Vector2 result = EditorGUI.Vector2Field(rect, GUIContent.none, val);
                    if (result != val)
                    {
                        variable.Vector2Value = result;
                        GUI.changed = true;
                    }
                    break;
                }
                case BlackboardVariableType.Vector3:
                {
                    Vector3 val = variable.Vector3Value;
                    Vector3 result = EditorGUI.Vector3Field(rect, GUIContent.none, val);
                    if (result != val)
                    {
                        variable.Vector3Value = result;
                        GUI.changed = true;
                    }
                    break;
                }
            }
        }

        private static string GetUniqueName(List<BlackboardVariable> variables, string baseName)
        {
            if (!VariableExists(variables, baseName))
                return baseName;

            for (int i = 1; i < 1000; i++)
            {
                string candidate = $"{baseName} {i}";
                if (!VariableExists(variables, candidate))
                    return candidate;
            }

            return baseName;
        }

        private static bool VariableExists(List<BlackboardVariable> variables, string name)
        {
            for (int i = 0; i < variables.Count; i++)
                if (variables[i].Name == name)
                    return true;
            return false;
        }
    }
}

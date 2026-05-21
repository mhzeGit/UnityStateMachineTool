using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CleanStateMachine
{
    public class BlackboardView
    {
        private Vector2 _scrollPos;

        public event System.Action VariablesChanged;

        public void Draw(Rect rect, List<BlackboardVariable> variables)
        {
            if (variables == null)
                return;

            var e = Event.current;

            EditorGUI.DrawRect(rect, UITheme.PanelBg);

            Rect headerRect = new Rect(rect.x, rect.y, rect.width, UITheme.HeaderHeight);
            DrawHeader(headerRect, variables);

            Rect listRect = new Rect(
                rect.x,
                rect.y + UITheme.HeaderHeight,
                rect.width,
                rect.height - UITheme.HeaderHeight
            );
            DrawVariableList(listRect, variables);

            if (listRect.Contains(e.mousePosition))
                EditorGUIUtility.AddCursorRect(listRect, MouseCursor.Text);
        }

        private void DrawHeader(Rect rect, List<BlackboardVariable> variables)
        {
            UITheme.DrawHeaderBackground(rect);

            float toggleSize = 20f;
            float addSize = 22f;
            float gap = 6f;
            float rightEdge = rect.x + rect.width;

            Rect addRect = new Rect(
                rightEdge - toggleSize - addSize - gap,
                rect.y + (rect.height - addSize) * 0.5f,
                addSize,
                addSize
            );

            Rect labelRect = new Rect(rect.x, rect.y, addRect.x - rect.x - 4f, rect.height);
            GUI.Label(labelRect, "Blackboard", UITheme.HeaderStyle);

            bool hover = addRect.Contains(Event.current.mousePosition);
            UITheme.DrawSmallButton(addRect, hover);

            if (GUI.Button(addRect, "+", UITheme.CloseButtonStyle))
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
                        VariablesChanged?.Invoke();
                        _scrollPos.y = float.MaxValue;
                    });
                }
                menu.ShowAsContext();
                Event.current.Use();
            }
        }

        private void DrawVariableList(Rect rect, List<BlackboardVariable> variables)
        {
            var e = Event.current;

            if (e.type == EventType.ContextClick && rect.Contains(e.mousePosition))
            {
                float contentY = e.mousePosition.y - rect.y + _scrollPos.y;
                int index = (int)(contentY / UITheme.RowHeight);
                if (index >= 0 && index < variables.Count)
                {
                    int capturedIndex = index;
                    var menu = new GenericMenu();
                    menu.AddItem(new GUIContent("Delete"), false, () =>
                    {
                        variables.RemoveAt(capturedIndex);
                        VariablesChanged?.Invoke();
                    });
                    menu.ShowAsContext();
                    e.Use();
                }
            }

            if (e.type == EventType.MouseDown && rect.Contains(e.mousePosition))
                DefocusTextField();

            float totalHeight = variables.Count * UITheme.RowHeight;
            Rect viewRect = new Rect(0f, 0f, rect.width - 14f, totalHeight);

            _scrollPos = GUI.BeginScrollView(rect, _scrollPos, viewRect);

            for (int i = 0; i < variables.Count; i++)
            {
                Rect rowRect = new Rect(0f, i * UITheme.RowHeight, viewRect.width, UITheme.RowHeight);
                DrawVariableRow(rowRect, variables[i], i);
            }

            if (e.type == EventType.KeyDown &&
                (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter))
                DefocusTextField();

            GUI.EndScrollView();
        }

        private static void DefocusTextField()
        {
            GUIUtility.keyboardControl = 0;
            GUIUtility.hotControl = 0;
            EditorGUIUtility.editingTextField = false;
            GUI.FocusControl("");
        }

        private void DrawVariableRow(Rect rect, BlackboardVariable variable, int index)
        {
            Color rowBg = index % 2 == 0 ? UITheme.RowEven : UITheme.RowOdd;
            EditorGUI.DrawRect(rect, rowBg);

            float pad = 6f;
            float innerW = rect.width - pad * 2;
            float fieldH = rect.height - pad;
            float fieldY = rect.y + pad * 0.5f;

            Rect fieldBg = new Rect(rect.x + pad, fieldY, innerW, fieldH);
            EditorGUI.DrawRect(fieldBg, UITheme.FieldBg);

            float gap = 3f;
            float nameW = (innerW - gap) * 0.35f;
            float valueW = innerW - gap - nameW;

            Rect nameRect = new Rect(rect.x + pad + 2f, fieldY, nameW - 2f, fieldH);
            EditorGUI.BeginChangeCheck();
            string newName = EditorGUI.TextField(nameRect, variable.Name, UITheme.RowFieldStyle);
            if (EditorGUI.EndChangeCheck())
            {
                variable.Name = newName;
                VariablesChanged?.Invoke();
            }

            Rect valueRect = new Rect(rect.x + pad + nameW + gap + 2f, fieldY, valueW - 2f, fieldH);
            DrawValueField(valueRect, variable);
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
                    float toggleWidth = 15f;
                    Rect toggleRect = new Rect(
                        rect.x + (rect.width - toggleWidth) * 0.5f,
                        rect.y,
                        toggleWidth,
                        rect.height
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

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CleanStateMachine
{
    public class BlackboardView
    {
        private Vector2 _scrollPos;
        private string _newVariableName = "New Variable";
        private BlackboardVariableType _newVariableType = BlackboardVariableType.Float;

        public event System.Action CloseRequested;
        public event System.Action VariablesChanged;

        public void Draw(Rect rect, List<BlackboardVariable> variables)
        {
            var e = Event.current;

            UITheme.DrawPanelBackground(rect);

            Rect headerRect = new Rect(rect.x, rect.y, rect.width, UITheme.HeaderHeight);
            DrawHeader(headerRect);

            Rect listRect = new Rect(
                rect.x,
                rect.y + UITheme.HeaderHeight,
                rect.width,
                rect.height - UITheme.HeaderHeight - UITheme.FooterHeight
            );
            DrawVariableList(listRect, variables);

            Rect footerRect = new Rect(
                rect.x,
                rect.y + rect.height - UITheme.FooterHeight,
                rect.width,
                UITheme.FooterHeight
            );
            DrawFooter(footerRect, variables);

            if (listRect.Contains(e.mousePosition))
                EditorGUIUtility.AddCursorRect(listRect, MouseCursor.Text);
        }

        private void DrawHeader(Rect rect)
        {
            EditorGUI.DrawRect(rect, UITheme.PanelHeaderBg);

            GUI.Label(rect, "Blackboard", UITheme.HeaderStyle);

            Rect closeRect = new Rect(rect.xMax - 20f, rect.y + (rect.height - 16f) * 0.5f, 16f, 16f);
            if (GUI.Button(closeRect, "X", UITheme.CloseButtonStyle))
                CloseRequested?.Invoke();
        }

        private void DrawVariableList(Rect rect, List<BlackboardVariable> variables)
        {
            float totalHeight = variables.Count * UITheme.RowHeight;
            Rect viewRect = new Rect(0f, 0f, rect.width - 14f, totalHeight);

            _scrollPos = GUI.BeginScrollView(rect, _scrollPos, viewRect);

            for (int i = 0; i < variables.Count; i++)
            {
                Rect rowRect = new Rect(0f, i * UITheme.RowHeight, viewRect.width, UITheme.RowHeight);
                Color rowBg = i % 2 == 0 ? UITheme.RowEven : UITheme.RowOdd;
                EditorGUI.DrawRect(rowRect, rowBg);
                DrawVariableRow(rowRect, variables[i], i, variables);
            }

            GUI.EndScrollView();
        }

        private void DrawVariableRow(Rect rect, BlackboardVariable variable, int index, List<BlackboardVariable> variables)
        {
            float deleteWidth = 18f;
            float nameWidth = rect.width * 0.38f;
            float typeWidth = rect.width * 0.30f;
            float valueWidth = rect.width - nameWidth - typeWidth - deleteWidth;

            float x = rect.x;
            Rect nameRect = new Rect(x, rect.y, nameWidth, rect.height);
            x += nameWidth;
            Rect typeRect = new Rect(x, rect.y, typeWidth, rect.height);
            x += typeWidth;
            Rect valueRect = new Rect(x, rect.y, valueWidth, rect.height);
            x += valueWidth;
            Rect deleteRect = new Rect(x, rect.y, deleteWidth, rect.height);

            DrawTextField(nameRect, variable.Name, v => variable.Name = v);

            var newType = (BlackboardVariableType)EditorGUI.EnumPopup(typeRect, variable.Type);
            if (newType != variable.Type)
            {
                variable.Type = newType;
                ResetValueForType(variable);
            }

            DrawValueField(valueRect, variable);

            if (GUI.Button(deleteRect, "X", UITheme.CloseButtonStyle))
            {
                variables.RemoveAt(index);
                VariablesChanged?.Invoke();
                GUI.changed = true;
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

        private static void DrawValueField(Rect rect, BlackboardVariable variable)
        {
            switch (variable.Type)
            {
                case BlackboardVariableType.Bool:
                {
                    bool val = variable.BoolValue;
                    bool result = EditorGUI.Toggle(rect, val);
                    if (result != val)
                        variable.BoolValue = result;
                    break;
                }
                case BlackboardVariableType.Int:
                {
                    int val = variable.IntValue;
                    int result = EditorGUI.IntField(rect, val);
                    if (result != val)
                        variable.IntValue = result;
                    break;
                }
                case BlackboardVariableType.Float:
                {
                    float val = variable.FloatValue;
                    float result = EditorGUI.FloatField(rect, val);
                    if (Mathf.Abs(result - val) > 1e-6f)
                        variable.FloatValue = result;
                    break;
                }
                case BlackboardVariableType.String:
                {
                    string result = EditorGUI.TextField(rect, variable.StringValue);
                    if (result != variable.StringValue)
                        variable.StringValue = result;
                    break;
                }
                case BlackboardVariableType.Vector2:
                {
                    Vector2 val = variable.Vector2Value;
                    Vector2 result = EditorGUI.Vector2Field(rect, GUIContent.none, val);
                    if (result != val)
                        variable.Vector2Value = result;
                    break;
                }
                case BlackboardVariableType.Vector3:
                {
                    Vector3 val = variable.Vector3Value;
                    Vector3 result = EditorGUI.Vector3Field(rect, GUIContent.none, val);
                    if (result != val)
                        variable.Vector3Value = result;
                    break;
                }
            }
        }

        private void DrawFooter(Rect rect, List<BlackboardVariable> variables)
        {
            EditorGUI.DrawRect(rect, UITheme.PanelHeaderBg);

            Rect borderTop = new Rect(rect.x, rect.y, rect.width, 1f);
            EditorGUI.DrawRect(borderTop, UITheme.PanelBorder);

            Rect addRect = new Rect(
                rect.x + UITheme.Padding,
                rect.y + UITheme.Padding,
                rect.width - UITheme.Padding * 2f,
                rect.height - UITheme.Padding * 2f
            );

            if (GUI.Button(addRect, "Add Variable"))
            {
                variables.Add(new BlackboardVariable
                {
                    Name = GetUniqueName(variables, _newVariableName),
                    Type = _newVariableType
                });
                VariablesChanged?.Invoke();
                _scrollPos.y = float.MaxValue;
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

        private static void DrawTextField(Rect rect, string text, System.Action<string> setter)
        {
            string result = EditorGUI.TextField(rect, text);
            if (result != text)
                setter(result);
        }
    }
}

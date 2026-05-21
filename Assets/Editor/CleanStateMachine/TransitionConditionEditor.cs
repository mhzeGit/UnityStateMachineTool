using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CleanStateMachine
{
    public class TransitionConditionEditor
    {
        private readonly HashSet<int> _expandedConditions = new HashSet<int>();

        public event Action Changed;

        public void Draw(Rect rect, List<TransitionCondition> conditions,
            List<BlackboardVariable> blackboardVariables, ref Vector2 scrollPos)
        {
            if (conditions == null) return;

            float totalHeight = ComputeTotalHeight(conditions);
            Rect viewRect = new Rect(0f, 0f, rect.width - 14f, Mathf.Max(totalHeight, rect.height));
            scrollPos = GUI.BeginScrollView(rect, scrollPos, viewRect);

            float y = 0f;
            float w = viewRect.width;

            DrawSectionHeader(ref y, w, "CONDITIONS");

            DrawAddButton(ref y, w, conditions);

            if (conditions.Count == 0)
            {
                Rect emptyRect = new Rect(UITheme.Padding * 3f, y, w - UITheme.Padding * 6f, UITheme.RowHeight);
                var emptyStyle = new GUIStyle(UITheme.SecondaryStyle)
                {
                    normal = { textColor = UITheme.TextMuted },
                    fontStyle = FontStyle.Italic
                };
                GUI.Label(emptyRect, "  No conditions — click + to add", emptyStyle);
                y += UITheme.RowHeight;
            }

            for (int i = 0; i < conditions.Count; i++)
            {
                var condition = conditions[i];
                bool isExpanded = _expandedConditions.Contains(i);
                DrawConditionRow(ref y, w, conditions, i, condition, blackboardVariables, isExpanded);
            }

            GUI.EndScrollView();
        }

        private void DrawAddButton(ref float y, float width, List<TransitionCondition> conditions)
        {
            Rect addRect = new Rect(0f, y, width, UITheme.RowHeight);
            EditorGUI.DrawRect(addRect, UITheme.RowOdd);

            Rect btnRect = new Rect(addRect.x + UITheme.Padding * 2f, addRect.y + 3f, 20f, addRect.height - 6f);
            bool btnHover = btnRect.Contains(Event.current.mousePosition);
            UITheme.DrawSmallButton(btnRect, btnHover);

            var btnStyle = new GUIStyle
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                normal = { textColor = UITheme.Accent }
            };

            if (GUI.Button(btnRect, "+", btnStyle))
            {
                conditions.Add(new TransitionCondition());
                Changed?.Invoke();
            }

            Rect labelRect = new Rect(btnRect.xMax + 6f, addRect.y, width - btnRect.xMax - 12f, addRect.height);
            GUI.Label(labelRect, "Add Condition", new GUIStyle(UITheme.SecondaryStyle)
            {
                normal = { textColor = UITheme.TextSecondary },
                fontSize = 10
            });

            y += UITheme.RowHeight;
        }

        private void DrawConditionRow(ref float y, float width, List<TransitionCondition> conditions,
            int index, TransitionCondition condition, List<BlackboardVariable> blackboardVariables,
            bool isExpanded)
        {
            float rowHeight = isExpanded ? GetExpandedHeight() : UITheme.RowHeight;
            Rect rowRect = new Rect(0f, y, width, rowHeight);
            Color rowBg = ((int)(y / UITheme.RowHeight)) % 2 == 0 ? UITheme.RowEven : UITheme.RowOdd;
            EditorGUI.DrawRect(rowRect, rowBg);

            if (isExpanded)
            {
                Color accentColor = UITheme.Accent;
                accentColor.a = 0.06f;
                EditorGUI.DrawRect(rowRect, accentColor);
            }

            Rect expandRect = new Rect(rowRect.x + UITheme.Padding * 2f, rowRect.y,
                rowRect.width - UITheme.Padding * 7f - 24f, UITheme.RowHeight);
            string arrow = isExpanded ? " ▲" : " ▼";

            if (GUI.Button(expandRect, GetConditionSummary(condition) + arrow, UITheme.SecondaryStyle))
            {
                if (isExpanded)
                    _expandedConditions.Remove(index);
                else
                    _expandedConditions.Add(index);
            }

            Rect deleteRect = new Rect(rowRect.xMax - 22f, rowRect.y + 3f, 16f, UITheme.RowHeight - 6f);
            if (GUI.Button(deleteRect, "✕", UITheme.DeleteButtonStyle))
            {
                conditions.RemoveAt(index);
                RebuildExpandedAfterDeletion(index);
                Changed?.Invoke();
            }

            y += UITheme.RowHeight;

            if (isExpanded)
            {
                DrawConditionFields(ref y, width, condition, blackboardVariables);
            }
        }

        private void DrawConditionFields(ref float y, float width, TransitionCondition condition,
            List<BlackboardVariable> blackboardVariables)
        {
            float indent = UITheme.Padding * 4f;
            float fieldWidth = width - indent - UITheme.Padding * 2f;

            DrawVariableSelector(ref y, width, indent, fieldWidth, condition, blackboardVariables);

            BlackboardVariable variable = FindVariable(condition.BlackboardVariableName, blackboardVariables);
            if (variable != null)
            {
                DrawComparisonSelector(ref y, width, indent, fieldWidth, condition, variable);
                DrawValueField(ref y, width, indent, fieldWidth, condition, variable);
            }
            else
            {
                Rect msgRect = new Rect(indent, y, fieldWidth, UITheme.RowHeight);
                var msgStyle = new GUIStyle(UITheme.LabelStyle)
                {
                    normal = { textColor = UITheme.Warning }
                };
                GUI.Label(msgRect, "Select a variable first", msgStyle);
                y += UITheme.RowHeight;
            }

            y += 4f;
        }

        private void DrawVariableSelector(ref float y, float width, float indent, float fieldWidth,
            TransitionCondition condition, List<BlackboardVariable> blackboardVariables)
        {
            Rect labelRect = new Rect(indent, y, 80f, UITheme.RowHeight);
            GUI.Label(labelRect, "Variable", UITheme.LabelStyle);

            Rect selectorRect = new Rect(indent + 84f, y + 2f,
                fieldWidth - 84f, UITheme.RowHeight - 4f);

            string currentName = condition.BlackboardVariableName ?? "";
            int selectedIndex = -1;
            for (int i = 0; i < blackboardVariables.Count; i++)
            {
                if (blackboardVariables[i].Name == currentName)
                {
                    selectedIndex = i;
                    break;
                }
            }

            string[] names = new string[blackboardVariables.Count + 1];
            names[0] = "(none)";
            for (int i = 0; i < blackboardVariables.Count; i++)
                names[i + 1] = blackboardVariables[i].Name;

            int newIndex = EditorGUI.Popup(selectorRect, selectedIndex + 1, names) - 1;
            string newName = newIndex >= 0 ? blackboardVariables[newIndex].Name : "";

            if (newName != currentName)
            {
                condition.BlackboardVariableName = newName;
                if (newIndex >= 0)
                    condition.Comparison = GetDefaultComparison(blackboardVariables[newIndex].Type);
                Changed?.Invoke();
            }

            y += UITheme.RowHeight;
        }

        private void DrawComparisonSelector(ref float y, float width, float indent, float fieldWidth,
            TransitionCondition condition, BlackboardVariable variable)
        {
            Rect labelRect = new Rect(indent, y, 80f, UITheme.RowHeight);
            GUI.Label(labelRect, "Compare", UITheme.LabelStyle);

            Rect selectorRect = new Rect(indent + 84f, y + 2f,
                fieldWidth - 84f, UITheme.RowHeight - 4f);

            List<ConditionComparison> validComparisons = GetValidComparisons(variable.Type);

            string[] comparisonNames = new string[validComparisons.Count];
            int selectedIndex = 0;
            for (int i = 0; i < validComparisons.Count; i++)
            {
                comparisonNames[i] = FormatComparison(validComparisons[i]);
                if (validComparisons[i] == condition.Comparison)
                    selectedIndex = i;
            }

            int newIndex = EditorGUI.Popup(selectorRect, selectedIndex, comparisonNames);
            if (newIndex >= 0 && newIndex < validComparisons.Count)
            {
                var newComparison = validComparisons[newIndex];
                if (newComparison != condition.Comparison)
                {
                    condition.Comparison = newComparison;
                    Changed?.Invoke();
                }
            }

            y += UITheme.RowHeight;
        }

        private void DrawValueField(ref float y, float width, float indent, float fieldWidth,
            TransitionCondition condition, BlackboardVariable variable)
        {
            Rect labelRect = new Rect(indent, y, 80f, UITheme.RowHeight);
            GUI.Label(labelRect, "Value", UITheme.LabelStyle);

            Rect fieldRect = new Rect(indent + 84f, y + 2f,
                fieldWidth - 84f, UITheme.RowHeight - 4f);

            string currentValue = condition.CompareValue ?? "";

            switch (variable.Type)
            {
                case BlackboardVariableType.Bool:
                {
                    bool boolVal = currentValue.ToLowerInvariant() == "true";
                    bool newVal = EditorGUI.Toggle(fieldRect, boolVal);
                    string newStr = newVal ? "true" : "false";
                    if (newStr != currentValue)
                    {
                        condition.CompareValue = newStr;
                        Changed?.Invoke();
                    }
                    break;
                }
                case BlackboardVariableType.Int:
                {
                    int.TryParse(currentValue, out int intVal);
                    int newInt = EditorGUI.IntField(fieldRect, intVal);
                    string newStr = newInt.ToString();
                    if (newStr != currentValue)
                    {
                        condition.CompareValue = newStr;
                        Changed?.Invoke();
                    }
                    break;
                }
                case BlackboardVariableType.Float:
                {
                    float.TryParse(currentValue,
                        System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture,
                        out float floatVal);
                    float newFloat = EditorGUI.FloatField(fieldRect, floatVal);
                    string newStr = newFloat.ToString("G", System.Globalization.CultureInfo.InvariantCulture);
                    if (newStr != currentValue)
                    {
                        condition.CompareValue = newStr;
                        Changed?.Invoke();
                    }
                    break;
                }
                default:
                {
                    EditorGUI.DrawRect(fieldRect, UITheme.FieldBg);
                    string newValue = GUI.TextField(fieldRect, currentValue, UITheme.RowFieldStyle);
                    if (newValue != currentValue)
                    {
                        condition.CompareValue = newValue;
                        Changed?.Invoke();
                    }
                    break;
                }
            }

            y += UITheme.RowHeight;
        }

        private static BlackboardVariable FindVariable(string name, List<BlackboardVariable> variables)
        {
            for (int i = 0; i < variables.Count; i++)
            {
                if (variables[i].Name == name)
                    return variables[i];
            }
            return null;
        }

        private static List<ConditionComparison> GetValidComparisons(BlackboardVariableType type)
        {
            switch (type)
            {
                case BlackboardVariableType.Bool:
                case BlackboardVariableType.Int:
                case BlackboardVariableType.Float:
                    return new List<ConditionComparison>
                    {
                        ConditionComparison.EqualTo, ConditionComparison.NotEqualTo,
                        ConditionComparison.GreaterThan, ConditionComparison.LessThan,
                        ConditionComparison.GreaterOrEqual, ConditionComparison.LessOrEqual
                    };
                case BlackboardVariableType.String:
                case BlackboardVariableType.Vector2:
                case BlackboardVariableType.Vector3:
                    return new List<ConditionComparison>
                    {
                        ConditionComparison.EqualTo, ConditionComparison.NotEqualTo
                    };
                default:
                    return new List<ConditionComparison> { ConditionComparison.EqualTo };
            }
        }

        private static ConditionComparison GetDefaultComparison(BlackboardVariableType type)
        {
            switch (type)
            {
                case BlackboardVariableType.Bool:
                    return ConditionComparison.EqualTo;
                case BlackboardVariableType.Int:
                case BlackboardVariableType.Float:
                    return ConditionComparison.GreaterThan;
                default:
                    return ConditionComparison.EqualTo;
            }
        }

        private static string FormatComparison(ConditionComparison comparison)
        {
            switch (comparison)
            {
                case ConditionComparison.GreaterThan: return "greater than";
                case ConditionComparison.LessThan: return "less than";
                case ConditionComparison.EqualTo: return "equal to";
                case ConditionComparison.NotEqualTo: return "not equal to";
                case ConditionComparison.GreaterOrEqual: return "greater or equal";
                case ConditionComparison.LessOrEqual: return "less or equal";
                default: return comparison.ToString();
            }
        }

        private void RebuildExpandedAfterDeletion(int deletedIndex)
        {
            var newExpanded = new HashSet<int>();
            foreach (int id in _expandedConditions)
            {
                if (id < deletedIndex)
                    newExpanded.Add(id);
                else if (id > deletedIndex)
                    newExpanded.Add(id - 1);
            }
            _expandedConditions.Clear();
            foreach (int id in newExpanded)
                _expandedConditions.Add(id);
        }

        private static string GetConditionSummary(TransitionCondition condition)
        {
            string varName = string.IsNullOrEmpty(condition.BlackboardVariableName)
                ? "(no variable)" : condition.BlackboardVariableName;
            string comp = FormatComparison(condition.Comparison);

            string value = condition.CompareValue;

            if (!string.IsNullOrEmpty(value))
                return $"  {varName} {comp} {value}";
            else
                return $"  {varName} {comp}";
        }

        private static float GetExpandedHeight()
        {
            return UITheme.RowHeight * 4f + 4f;
        }

        private float ComputeTotalHeight(List<TransitionCondition> conditions)
        {
            float h = UITheme.RowHeight * 2f;

            if (conditions.Count == 0)
                h += UITheme.RowHeight;
            else
            {
                for (int i = 0; i < conditions.Count; i++)
                {
                    h += _expandedConditions.Contains(i) ? GetExpandedHeight() : UITheme.RowHeight;
                }
            }
            return h + 20f;
        }

        private static void DrawSectionHeader(ref float y, float width, string label)
        {
            Rect rect = new Rect(0f, y, width, UITheme.RowHeight);
            EditorGUI.DrawRect(rect, UITheme.PanelHeaderBg);
            GUI.Label(rect, label, UITheme.SectionStyle);
            UITheme.DrawSectionDivider(y + UITheme.RowHeight - 1f, width);
            y += UITheme.RowHeight + 4f;
        }
    }
}

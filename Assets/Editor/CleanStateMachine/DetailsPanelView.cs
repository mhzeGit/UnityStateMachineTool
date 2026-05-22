using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CleanStateMachine
{
    public class DetailsPanelView
    {
        private Vector2 _scrollPos;

        public event Action Changed;

        public void Draw(Rect rect, IReadOnlyList<ISelectable> selected,
            List<StateView> states, List<ConnectionView> connections,
            List<BlackboardVariable> blackboardVariables)
        {
            UITheme.DrawPanelBackground(rect);

            Rect headerRect = new Rect(rect.x, rect.y, rect.width, UITheme.HeaderHeight);
            DrawHeader(headerRect);

            Rect contentRect = new Rect(
                rect.x,
                rect.y + UITheme.HeaderHeight,
                rect.width,
                rect.height - UITheme.HeaderHeight
            );

            DrawContent(contentRect, selected, states, connections, blackboardVariables);
        }

        private void DrawHeader(Rect rect)
        {
            UITheme.DrawHeaderBackground(rect);
            GUI.Label(rect, "Inspector", UITheme.HeaderStyle);
        }

        private void DrawContent(Rect rect, IReadOnlyList<ISelectable> selected,
            List<StateView> states, List<ConnectionView> connections,
            List<BlackboardVariable> blackboardVariables)
        {
            if (selected.Count == 0)
            {
                DrawEmptyState(rect);
                return;
            }

            if (selected.Count == 1)
            {
                DrawSingleSelection(rect, selected[0], connections, blackboardVariables);
                return;
            }

            DrawMultiSelection(rect, selected);
        }

        private static void DrawEmptyState(Rect rect)
        {
            Rect infoRect = new Rect(rect.x + 20f, rect.y + 20f, rect.width - 40f, 80f);
            GUI.Label(infoRect, "Select an item to inspect", UITheme.InfoBoxStyle);
        }

        private void DrawSingleSelection(Rect rect, ISelectable item,
            List<ConnectionView> connections, List<BlackboardVariable> blackboardVariables)
        {
            if (item is StateView state)
            {
                DrawStateContent(rect, state, connections, blackboardVariables);
            }
            else if (item is ConnectionView conn)
            {
                DrawConnectionContent(rect, conn, blackboardVariables);
            }
            else if (item is CommentGroupView group)
            {
                DrawGroupContent(rect, group);
            }
            else
            {
                DrawOtherContent(rect, item);
            }
        }

        private void DrawStateContent(Rect rect, StateView state, List<ConnectionView> connections,
            List<BlackboardVariable> blackboardVariables)
        {
            float w = rect.width;
            float totalHeight = UITheme.RowHeight * 5f + 100f;
            if (state.BehaviourInstance != null)
                totalHeight += GetPropertiesHeight(state.BehaviourInstance) + 60f;
            Rect viewRect = new Rect(0f, 0f, w - 14f, Mathf.Max(totalHeight, rect.height));
            _scrollPos = GUI.BeginScrollView(rect, _scrollPos, viewRect);

            float y = 12f;
            float iw = viewRect.width;

            Rect titleRect = new Rect(8f, y, iw - 16f, 24f);
            GUI.Label(titleRect, "State Information", UITheme.LargeTitleStyle);
            y += 28f;

            DrawInfoRow(ref y, iw, "Name", state.Name);
            DrawInfoRow(ref y, iw, "Position", $"({state.Position.x:F1}, {state.Position.y:F1})");
            DrawInfoRow(ref y, iw, "Size", $"({state.Size.x:F0} x {state.Size.y:F0})");
            DrawInfoRow(ref y, iw, "Connections", CountStateConnections(state, connections).ToString());

            y += 8f;
            UITheme.DrawSectionDivider(y, iw);
            y += 10f;

            Rect scriptTitleRect = new Rect(8f, y, iw - 16f, 22f);
            GUI.Label(scriptTitleRect, "State Behaviour", UITheme.LargeTitleStyle);
            y += 28f;

            DrawScriptRow(ref y, iw, state.BehaviourScript, IsValidStateBehaviour, (prev, next) =>
            {
                if (next == prev) return;
                if (state.BehaviourInstance != null)
                {
                    Object.DestroyImmediate(state.BehaviourInstance, true);
                    state.BehaviourInstance = null;
                }
                state.BehaviourScript = next;
                if (next != null)
                {
                    var type = next.GetClass();
                    if (type != null)
                    {
                        state.BehaviourInstance = (StateBehaviour)ScriptableObject.CreateInstance(type);
                        state.BehaviourInstance.name = $"{state.Name}_Behaviour";
                        state.BehaviourInstance.hideFlags = HideFlags.HideInHierarchy;
                    }
                }
                Changed?.Invoke();
            });

            if (state.BehaviourInstance != null)
            {
                DrawScriptableObjectProperties(ref y, iw, state.BehaviourInstance, blackboardVariables);
            }

            GUI.EndScrollView();
        }

        private void DrawConnectionContent(Rect rect, ConnectionView conn, List<BlackboardVariable> blackboardVariables)
        {
            float w = rect.width;
            float totalHeight = UITheme.RowHeight * 3f + 80f;
            if (conn.ConditionInstance != null)
                totalHeight += GetPropertiesHeight(conn.ConditionInstance) + 60f;
            Rect viewRect = new Rect(0f, 0f, w - 14f, Mathf.Max(totalHeight, rect.height));
            _scrollPos = GUI.BeginScrollView(rect, _scrollPos, viewRect);

            float y = 12f;
            float iw = viewRect.width;

            Rect titleRect = new Rect(8f, y, iw - 16f, 24f);
            GUI.Label(titleRect, "Connection Information", UITheme.LargeTitleStyle);
            y += 28f;

            DrawInfoRow(ref y, iw, "From", conn.From?.Name ?? "—");
            DrawInfoRow(ref y, iw, "To", conn.To?.Name ?? "—");

            y += 8f;
            UITheme.DrawSectionDivider(y, iw);
            y += 10f;

            Rect condTitleRect = new Rect(8f, y, iw - 16f, 22f);
            GUI.Label(condTitleRect, "Transition Condition", UITheme.LargeTitleStyle);
            y += 28f;

            DrawScriptRow(ref y, iw, conn.ConditionScript, IsValidConditionScript, (prev, next) =>
            {
                if (next == prev) return;
                if (conn.ConditionInstance != null)
                {
                    Object.DestroyImmediate(conn.ConditionInstance, true);
                    conn.ConditionInstance = null;
                }
                conn.ConditionScript = next;
                if (next != null)
                {
                    var type = next.GetClass();
                    if (type != null)
                    {
                        conn.ConditionInstance = (ConditionScript)ScriptableObject.CreateInstance(type);
                        conn.ConditionInstance.name = $"{conn.From?.Name ?? "?"}->{conn.To?.Name ?? "?"}_Condition";
                        conn.ConditionInstance.hideFlags = HideFlags.HideInHierarchy;
                    }
                }
                Changed?.Invoke();
            });

            if (conn.ConditionInstance != null)
            {
                DrawScriptableObjectProperties(ref y, iw, conn.ConditionInstance, blackboardVariables);
            }

            GUI.EndScrollView();
        }

        private static bool IsValidStateBehaviour(MonoScript script)
        {
            var type = script.GetClass();
            return type != null && type.IsSubclassOf(typeof(StateBehaviour));
        }

        private static bool IsValidConditionScript(MonoScript script)
        {
            var type = script.GetClass();
            return type != null && type.IsSubclassOf(typeof(ConditionScript));
        }

        private static void DrawGroupContent(Rect rect, CommentGroupView group)
        {
            float y = 12f;
            float w = rect.width - 14f;

            float listHeight = Mathf.Min(group.Members.Count, 20) * UITheme.RowHeight;
            float totalHeight = UITheme.RowHeight * 2f + 80f + listHeight;
            Rect viewRect = new Rect(0f, 0f, w, Mathf.Max(totalHeight, rect.height));

            var scrollPos = Vector2.zero;
            scrollPos = GUI.BeginScrollView(rect, scrollPos, viewRect);

            Rect titleRect = new Rect(8f, y, w - 16f, 24f);
            GUI.Label(titleRect, "Group Information", UITheme.LargeTitleStyle);
            y += 28f;

            float iw = viewRect.width;
            DrawInfoRow(ref y, iw, "Label", group.Label);
            DrawInfoRow(ref y, iw, "Members", group.Members.Count.ToString());

            y += 12f;
            UITheme.DrawSectionDivider(y, iw);
            y += 12f;

            Rect memTitleRect = new Rect(8f, y, iw - 16f, 24f);
            GUI.Label(memTitleRect, "Members", UITheme.LargeTitleStyle);
            y += 28f;

            for (int i = 0; i < group.Members.Count; i++)
            {
                if (y + UITheme.RowHeight > viewRect.height)
                    break;

                Rect rowRect = new Rect(0f, y, w, UITheme.RowHeight);
                EditorGUI.DrawRect(rowRect, i % 2 == 0 ? UITheme.RowEven : UITheme.RowOdd);
                GUI.Label(new Rect(16f, rowRect.y, w - 32f, rowRect.height),
                    group.Members[i].Name, UITheme.SecondaryStyle);
                y += UITheme.RowHeight;
            }

            GUI.EndScrollView();
        }

        private static void DrawOtherContent(Rect rect, ISelectable item)
        {
            float y = 12f;
            float w = rect.width - 14f;

            Rect viewRect = new Rect(0f, 0f, w, UITheme.RowHeight * 4f);
            var scrollPos = Vector2.zero;
            scrollPos = GUI.BeginScrollView(rect, scrollPos, viewRect);

            Rect titleRect = new Rect(8f, y, w - 16f, 24f);
            GUI.Label(titleRect, "Inspector", UITheme.LargeTitleStyle);
            y += 28f;

            DrawInfoRow(ref y, w, "Type", item.GetType().Name);

            GUI.EndScrollView();
        }

        private static void DrawMultiSelection(Rect rect, IReadOnlyList<ISelectable> selected)
        {
            float y = 12f;
            float w = rect.width - 14f;

            float totalHeight = 40f + selected.Count * UITheme.RowHeight;
            Rect viewRect = new Rect(0f, 0f, w, totalHeight);

            var scrollPos = Vector2.zero;
            scrollPos = GUI.BeginScrollView(rect, scrollPos, viewRect);

            Rect titleRect = new Rect(8f, y, w - 16f, 24f);
            GUI.Label(titleRect, $"Selected ({selected.Count})", UITheme.LargeTitleStyle);
            y += 28f;

            for (int i = 0; i < selected.Count; i++)
            {
                Rect rowRect = new Rect(0f, y, w, UITheme.RowHeight);
                EditorGUI.DrawRect(rowRect, i % 2 == 0 ? UITheme.RowEven : UITheme.RowOdd);

                string label = selected[i] switch
                {
                    StateView sv => sv.Name,
                    CommentGroupView gv => gv.Label,
                    ConnectionView cv => $"{cv.From?.Name ?? "?"} \u2192 {cv.To?.Name ?? "?"}",
                    _ => selected[i].GetType().Name
                };

                string typeLabel = selected[i] switch
                {
                    StateView => "STATE",
                    CommentGroupView => "GROUP",
                    ConnectionView => "CONNECTION",
                    _ => "ITEM"
                };

                float badgeW = 60f;
                Rect typeRect = new Rect(8f, rowRect.y + 6f, badgeW, UITheme.RowHeight - 12f);
                EditorGUI.DrawRect(typeRect, UITheme.TypeBadgeBg);
                var typeStyle = new GUIStyle(UITheme.TypeBadgeStyle) { fontSize = 8 };
                GUI.Label(typeRect, typeLabel, typeStyle);

                Rect labelRect = new Rect(badgeW + 16f, rowRect.y, w - badgeW - 24f, rowRect.height);
                GUI.Label(labelRect, label, UITheme.SecondaryStyle);

                y += UITheme.RowHeight;
            }

            GUI.EndScrollView();
        }

        private static int CountStateConnections(StateView state, List<ConnectionView> connections)
        {
            int count = 0;
            for (int i = 0; i < connections.Count; i++)
            {
                if (connections[i].From == state || connections[i].To == state)
                    count++;
            }
            return count;
        }

        private static void DrawInfoRow(ref float y, float width, string label, string value)
        {
            Rect rect = new Rect(0f, y, width, UITheme.RowHeight);
            EditorGUI.DrawRect(rect, UITheme.RowBg);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), UITheme.RowBoundary);

            float labelWidth = 72f;
            Rect labelRect = new Rect(8f, rect.y + 1f, labelWidth, rect.height - 2f);
            Rect valueRect = new Rect(8f + labelWidth, rect.y, width - labelWidth - 16f, rect.height);

            GUI.Label(labelRect, label, UITheme.VariableLabelStyle);
            GUI.Label(valueRect, value, UITheme.SecondaryStyle);

            y += UITheme.RowHeight;
        }

        private static void DrawScriptRow(ref float y, float width, MonoScript script,
            Func<MonoScript, bool> isValid, Action<MonoScript, MonoScript> onAssign)
        {
            Rect rowRect = new Rect(0f, y, width, UITheme.RowHeight);
            EditorGUI.DrawRect(rowRect, UITheme.RowBg);
            EditorGUI.DrawRect(new Rect(rowRect.x, rowRect.yMax - 1f, rowRect.width, 1f), UITheme.RowBoundary);

            Rect labelRect = new Rect(8f, y + 1f, 72f, UITheme.RowHeight - 2f);
            GUI.Label(labelRect, "Script", UITheme.VariableLabelStyle);

            Rect fieldRect = new Rect(84f, y + 3f, width - 96f - 70f, UITheme.RowHeight - 6f);
            var newScript = (MonoScript)EditorGUI.ObjectField(fieldRect, script, typeof(MonoScript), false);

            if (newScript != script)
            {
                if (newScript != null && !isValid(newScript))
                {
                    EditorUtility.DisplayDialog("Invalid Script",
                        "The selected script must inherit from the required base class.", "OK");
                    newScript = script;
                }
                onAssign(script, newScript);
            }

            if (script != null)
            {
                Rect openRect = new Rect(fieldRect.xMax + 4f, y + 4f, 62f, UITheme.RowHeight - 8f);
                if (GUI.Button(openRect, "Open", EditorStyles.miniButton))
                {
                    AssetDatabase.OpenAsset(script);
                }

                var scriptType = script.GetClass();
                string typeName = scriptType != null ? scriptType.Name : script.name;
                Rect typeRect = new Rect(8f, y + UITheme.RowHeight + 2f, width - 16f, 18f);
                var typeStyle = new GUIStyle(UITheme.SecondaryStyle)
                {
                    normal = { textColor = UITheme.TextMuted },
                    fontSize = 10,
                    fontStyle = FontStyle.Italic
                };
                GUI.Label(typeRect, typeName, typeStyle);
                y += UITheme.RowHeight + 22f;
            }
            else
            {
                Rect hintRect = new Rect(8f, y + UITheme.RowHeight + 2f, width - 16f, 18f);
                var hintStyle = new GUIStyle(UITheme.SecondaryStyle)
                {
                    normal = { textColor = UITheme.TextMuted },
                    fontSize = 10,
                    fontStyle = FontStyle.Italic
                };
                GUI.Label(hintRect, "Assign a script to define behaviour", hintStyle);
                y += UITheme.RowHeight + 22f;
            }
        }

        private static float GetPropertiesHeight(ScriptableObject obj)
        {
            var so = new SerializedObject(obj);
            float h = 0f;
            SerializedProperty prop = so.GetIterator();
            bool enterChildren = true;
            while (prop.NextVisible(enterChildren))
            {
                enterChildren = false;
                if (prop.name == "m_Script") continue;
                float propHeight = prop.type == "BlackboardVariableReference"
                    ? UITheme.RowHeight
                    : EditorGUI.GetPropertyHeight(prop, true);
                h += Mathf.Max(UITheme.RowHeight, propHeight) + 2f;
            }
            return h;
        }

        private static void DrawScriptableObjectProperties(ref float y, float width,
            ScriptableObject obj, List<BlackboardVariable> blackboardVariables)
        {
            var so = new SerializedObject(obj);
            so.Update();

            y += 4f;
            UITheme.DrawSectionDivider(y, width);
            y += 8f;

            Rect titleRect = new Rect(8f, y, width - 16f, 22f);
            GUI.Label(titleRect, "Properties", UITheme.LargeTitleStyle);
            y += 28f;

            SerializedProperty prop = so.GetIterator();
            bool enterChildren = true;
            while (prop.NextVisible(enterChildren))
            {
                enterChildren = false;
                if (prop.name == "m_Script") continue;

                float rowH = Mathf.Max(UITheme.RowHeight, EditorGUI.GetPropertyHeight(prop, true));

                Rect rowRect = new Rect(0f, y, width, rowH);
                EditorGUI.DrawRect(rowRect, UITheme.RowBg);
                EditorGUI.DrawRect(new Rect(rowRect.x, rowRect.yMax - 1f, rowRect.width, 1f), UITheme.RowBoundary);

                if (prop.type == "BlackboardVariableReference")
                {
                    DrawBlackboardVariableRefField(rowRect, prop, blackboardVariables);
                }
                else
                {
                    Rect labelRect = new Rect(8f, y + 1f, 72f, rowH - 2f);
                    GUI.Label(labelRect, prop.displayName, UITheme.VariableLabelStyle);

                    Rect fieldRect = new Rect(84f, y + 3f, width - 96f, rowH - 6f);
                    EditorGUI.PropertyField(fieldRect, prop, GUIContent.none, true);
                }

                y += rowH + 1f;
            }

            y += 2f;

            if (so.ApplyModifiedProperties())
            {
                EditorUtility.SetDirty(obj);
            }
        }

        internal static void DrawBlackboardVariableRefField(Rect rect, SerializedProperty prop,
            List<BlackboardVariable> blackboardVariables)
        {
            var useBbProp = prop.FindPropertyRelative("UseBlackboard");
            var varNameProp = prop.FindPropertyRelative("BlackboardVariableName");
            var valueTypeProp = prop.FindPropertyRelative("ValueType");
            var defaultValueProp = prop.FindPropertyRelative("DefaultValue");

            var bbType = (BlackboardVariableType)valueTypeProp.enumValueIndex;
            bool useBb = useBbProp.boolValue;

            float modeWidth = 120f;
            float gap = 4f;

            Rect labelRect = new Rect(8f, rect.y + 1f, 72f, rect.height - 2f);
            GUI.Label(labelRect, prop.displayName, UITheme.VariableLabelStyle);

            float modeX = 84f;
            Rect modeRect = new Rect(modeX, rect.y + 3f, modeWidth, rect.height - 6f);
            DrawModeToggle(modeRect, useBb, useBbProp);

            float fieldX = modeRect.xMax + gap;
            float fieldW = rect.width - fieldX - 8f;

            Rect fieldRect = new Rect(fieldX, rect.y + 3f, fieldW, rect.height - 6f);

            if (useBb)
            {
                DrawBbDropdown(fieldRect, varNameProp, useBbProp, bbType, blackboardVariables);
            }
            else
            {
                DrawValueField(fieldRect, bbType, defaultValueProp);
            }
        }

        private static void DrawModeToggle(Rect rect, bool useBb, SerializedProperty useBbProp)
        {
            float halfW = rect.width * 0.5f;

            Rect bgRect = new Rect(rect.x, rect.y, rect.width, rect.height);
            EditorGUI.DrawRect(bgRect, UITheme.RowFieldBg);

            Rect valRect = new Rect(rect.x + 2f, rect.y + 2f, halfW - 3f, rect.height - 4f);
            Rect bindRect = new Rect(rect.x + halfW + 1f, rect.y + 2f, halfW - 3f, rect.height - 4f);

            if (!useBb)
                EditorGUI.DrawRect(valRect, UITheme.ButtonColor);
            else
                EditorGUI.DrawRect(bindRect, UITheme.ButtonColor);

            var active = new GUIStyle
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                normal = { textColor = UITheme.TextColor }
            };
            var inactive = new GUIStyle
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                normal = { textColor = UITheme.TextMuted }
            };

            GUI.Label(valRect, "Val", useBb ? inactive : active);
            GUI.Label(bindRect, "Bind", useBb ? active : inactive);

            if (GUI.Button(bgRect, GUIContent.none, GUIStyle.none))
            {
                useBbProp.boolValue = !useBb;
                useBbProp.serializedObject.ApplyModifiedProperties();
            }
        }

        private static void DrawBbDropdown(Rect rect, SerializedProperty varNameProp,
            SerializedProperty useBbProp, BlackboardVariableType bbType,
            List<BlackboardVariable> blackboardVariables)
        {
            string current = varNameProp.stringValue;
            string display = string.IsNullOrEmpty(current) ? "Select variable..." : current;

            var dropStyle = new GUIStyle(EditorStyles.popup)
            {
                fontSize = 11,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = UITheme.TextColor, background = null },
                hover = { textColor = UITheme.TextColor },
                focused = { textColor = UITheme.TextColor }
            };

            if (GUI.Button(rect, new GUIContent(display), dropStyle))
            {
                var menu = new GenericMenu();
                menu.AddItem(new GUIContent("None (direct)"), string.IsNullOrEmpty(current), () =>
                {
                    varNameProp.stringValue = "";
                    useBbProp.boolValue = false;
                    varNameProp.serializedObject.ApplyModifiedProperties();
                });
                menu.AddSeparator("");
                bool hasMatch = false;
                for (int i = 0; i < blackboardVariables.Count; i++)
                {
                    var bv = blackboardVariables[i];
                    if (bv.Type == bbType)
                    {
                        hasMatch = true;
                        string varName = bv.Name;
                        bool selected = varName == current;
                        string captured = varName;
                        menu.AddItem(new GUIContent(varName), selected, (object n) =>
                        {
                            varNameProp.stringValue = (string)n;
                            varNameProp.serializedObject.ApplyModifiedProperties();
                        }, captured);
                    }
                }
                if (!hasMatch)
                {
                    menu.AddDisabledItem(new GUIContent("No matching variables"));
                }
                menu.DropDown(rect);
            }
        }

        private static void DrawValueField(Rect rect, BlackboardVariableType bbType,
            SerializedProperty defaultValueProp)
        {
            switch (bbType)
            {
                case BlackboardVariableType.Bool:
                {
                    bool val = bool.TryParse(defaultValueProp.stringValue, out var v) && v;
                    bool result = EditorGUI.Toggle(rect, val);
                    if (result != val)
                        defaultValueProp.stringValue = result.ToString();
                    break;
                }
                case BlackboardVariableType.Int:
                {
                    int val = int.TryParse(defaultValueProp.stringValue, out var v) ? v : 0;
                    int result = EditorGUI.IntField(rect, val);
                    if (result != val)
                        defaultValueProp.stringValue = result.ToString();
                    break;
                }
                case BlackboardVariableType.Float:
                {
                    float val = float.TryParse(defaultValueProp.stringValue, out var v) ? v : 0f;
                    float result = EditorGUI.FloatField(rect, val);
                    if (Mathf.Abs(result - val) > 1e-6f)
                        defaultValueProp.stringValue = result.ToString("G");
                    break;
                }
                case BlackboardVariableType.String:
                {
                    string result = EditorGUI.TextField(rect, defaultValueProp.stringValue);
                    if (result != defaultValueProp.stringValue)
                        defaultValueProp.stringValue = result;
                    break;
                }
                case BlackboardVariableType.Vector2:
                case BlackboardVariableType.Vector3:
                {
                    string result = EditorGUI.TextField(rect, defaultValueProp.stringValue);
                    if (result != defaultValueProp.stringValue)
                        defaultValueProp.stringValue = result;
                    break;
                }
            }
        }
    }
}

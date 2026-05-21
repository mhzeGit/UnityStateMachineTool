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
                DrawStateContent(rect, state, connections);
            }
            else if (item is ConnectionView conn)
            {
                DrawConnectionContent(rect, conn);
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

        private void DrawStateContent(Rect rect, StateView state, List<ConnectionView> connections)
        {
            float w = rect.width;
            float totalHeight = UITheme.RowHeight * 5f + 100f;
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

            y += 12f;
            UITheme.DrawSectionDivider(y, iw);
            y += 12f;

            Rect scriptTitleRect = new Rect(8f, y, iw - 16f, 24f);
            GUI.Label(scriptTitleRect, "State Behaviour", UITheme.LargeTitleStyle);
            y += 28f;

            Rect labelRect = new Rect(12f, y, 80f, UITheme.RowHeight);
            GUI.Label(labelRect, "Script", UITheme.LabelStyle);

            Rect fieldRect = new Rect(96f, y + 4f, iw - 112f, UITheme.RowHeight - 8f);
            var newScript = (MonoScript)EditorGUI.ObjectField(fieldRect, state.BehaviourScript, typeof(MonoScript), false);

            if (newScript != state.BehaviourScript)
            {
                if (newScript != null && !IsValidStateBehaviour(newScript))
                {
                    EditorUtility.DisplayDialog("Invalid Script",
                        "The selected script must inherit from StateBehaviour.", "OK");
                    newScript = state.BehaviourScript;
                }
                else
                {
                    if (newScript != state.BehaviourScript && state.BehaviourInstance != null)
                    {
                        Object.DestroyImmediate(state.BehaviourInstance, true);
                        state.BehaviourInstance = null;
                    }
                }
                state.BehaviourScript = newScript;
                if (newScript != null)
                {
                    var type = newScript.GetClass();
                    if (type != null)
                    {
                        state.BehaviourInstance = (StateBehaviour)ScriptableObject.CreateInstance(type);
                        state.BehaviourInstance.name = $"{state.Name}_Behaviour";
                        state.BehaviourInstance.hideFlags = HideFlags.HideInHierarchy;
                    }
                }
                Changed?.Invoke();
            }
            y += UITheme.RowHeight + 4f;

            if (state.BehaviourScript != null)
            {
                Rect typeRect = new Rect(12f, y, iw - 24f, UITheme.RowHeight);
                var typeStyle = new GUIStyle(UITheme.SecondaryStyle)
                {
                    normal = { textColor = UITheme.TextMuted },
                    fontSize = 11,
                    fontStyle = FontStyle.Italic
                };
                var scriptType = state.BehaviourScript.GetClass();
                string typeName = scriptType != null ? scriptType.Name : state.BehaviourScript.name;
                GUI.Label(typeRect, typeName, typeStyle);
                y += UITheme.RowHeight;

                Rect openBtnRect = new Rect(12f, y, 100f, 24f);
                if (GUI.Button(openBtnRect, "Open Script"))
                {
                    AssetDatabase.OpenAsset(state.BehaviourScript);
                }
                y += 32f;
            }
            else
            {
                Rect hintRect = new Rect(12f, y, iw - 24f, UITheme.RowHeight);
                var hintStyle = new GUIStyle(UITheme.SecondaryStyle)
                {
                    normal = { textColor = UITheme.TextMuted },
                    fontSize = 11,
                    fontStyle = FontStyle.Italic
                };
                GUI.Label(hintRect, "Assign a StateBehaviour script to define state logic", hintStyle);
                y += UITheme.RowHeight;
            }

            GUI.EndScrollView();
        }

        private void DrawConnectionContent(Rect rect, ConnectionView conn)
        {
            float w = rect.width;
            float totalHeight = UITheme.RowHeight * 3f + 80f;
            Rect viewRect = new Rect(0f, 0f, w - 14f, Mathf.Max(totalHeight, rect.height));
            _scrollPos = GUI.BeginScrollView(rect, _scrollPos, viewRect);

            float y = 12f;
            float iw = viewRect.width;

            Rect titleRect = new Rect(8f, y, iw - 16f, 24f);
            GUI.Label(titleRect, "Connection Information", UITheme.LargeTitleStyle);
            y += 28f;

            DrawInfoRow(ref y, iw, "From", conn.From?.Name ?? "—");
            DrawInfoRow(ref y, iw, "To", conn.To?.Name ?? "—");

            y += 12f;
            UITheme.DrawSectionDivider(y, iw);
            y += 12f;

            Rect condTitleRect = new Rect(8f, y, iw - 16f, 24f);
            GUI.Label(condTitleRect, "Transition Condition", UITheme.LargeTitleStyle);
            y += 28f;

            Rect labelRect = new Rect(12f, y, 80f, UITheme.RowHeight);
            GUI.Label(labelRect, "Condition", UITheme.LabelStyle);

            Rect fieldRect = new Rect(96f, y + 4f, iw - 112f, UITheme.RowHeight - 8f);
            var newScript = (MonoScript)EditorGUI.ObjectField(fieldRect, conn.ConditionScript, typeof(MonoScript), false);

            if (newScript != conn.ConditionScript)
            {
                if (newScript != null && !IsValidConditionScript(newScript))
                {
                    EditorUtility.DisplayDialog("Invalid Script",
                        "The selected script must inherit from ConditionScript.", "OK");
                    newScript = conn.ConditionScript;
                }
                else
                {
                    if (newScript != conn.ConditionScript && conn.ConditionInstance != null)
                    {
                        Object.DestroyImmediate(conn.ConditionInstance, true);
                        conn.ConditionInstance = null;
                    }
                }
                conn.ConditionScript = newScript;
                if (newScript != null)
                {
                    var type = newScript.GetClass();
                    if (type != null)
                    {
                        conn.ConditionInstance = (ConditionScript)ScriptableObject.CreateInstance(type);
                        conn.ConditionInstance.name = $"{conn.From?.Name ?? "?"}->{conn.To?.Name ?? "?"}_Condition";
                        conn.ConditionInstance.hideFlags = HideFlags.HideInHierarchy;
                    }
                }
                Changed?.Invoke();
            }
            y += UITheme.RowHeight + 4f;

            if (conn.ConditionScript != null)
            {
                Rect typeRect = new Rect(12f, y, iw - 24f, UITheme.RowHeight);
                var typeStyle = new GUIStyle(UITheme.SecondaryStyle)
                {
                    normal = { textColor = UITheme.TextMuted },
                    fontSize = 11,
                    fontStyle = FontStyle.Italic
                };
                var scriptType = conn.ConditionScript.GetClass();
                string typeName = scriptType != null ? scriptType.Name : conn.ConditionScript.name;
                GUI.Label(typeRect, typeName, typeStyle);
                y += UITheme.RowHeight;

                Rect openBtnRect = new Rect(12f, y, 100f, 24f);
                if (GUI.Button(openBtnRect, "Open Script"))
                {
                    AssetDatabase.OpenAsset(conn.ConditionScript);
                }
                y += 32f;
            }
            else
            {
                Rect hintRect = new Rect(12f, y, iw - 24f, UITheme.RowHeight);
                var hintStyle = new GUIStyle(UITheme.SecondaryStyle)
                {
                    normal = { textColor = UITheme.TextMuted },
                    fontSize = 11,
                    fontStyle = FontStyle.Italic
                };
                GUI.Label(hintRect, "Assign a ConditionScript to control this transition", hintStyle);
                y += UITheme.RowHeight;
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
            EditorGUI.DrawRect(rect, UITheme.RowEven);

            float labelWidth = 100f;
            Rect labelRect = new Rect(12f, rect.y, labelWidth, rect.height);
            Rect valueRect = new Rect(12f + labelWidth, rect.y, width - labelWidth - 24f, rect.height);

            GUI.Label(labelRect, label, UITheme.LabelStyle);
            GUI.Label(valueRect, value, UITheme.SecondaryStyle);

            y += UITheme.RowHeight;
        }
    }
}

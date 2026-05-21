using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CleanStateMachine
{
    public class DetailsPanelView
    {
        private Vector2 _scrollPos;
        private readonly StateClassEditor _stateClassEditor;
        private readonly TransitionConditionEditor _conditionEditor;
        private Vector2 _stateClassScroll;
        private Vector2 _conditionScroll;

        public event Action Changed;

        public DetailsPanelView()
        {
            _stateClassEditor = new StateClassEditor();
            _conditionEditor = new TransitionConditionEditor();
            _stateClassEditor.Changed += () => Changed?.Invoke();
            _conditionEditor.Changed += () => Changed?.Invoke();
        }

        public void Draw(Rect rect, IReadOnlyList<ISelectable> selected,
            List<StateView> states, List<ConnectionView> connections,
            List<BlackboardVariable> blackboardVariables)
        {
            EditorGUI.DrawRect(rect, UITheme.PanelBg);

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
            GUI.Label(rect, "Details", UITheme.HeaderStyle);
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
            UITheme.DrawCard(infoRect);
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

        private void DrawStateContent(Rect rect, StateView state, List<ConnectionView> connections)
        {
            if (state.StateClass == null)
                state.StateClass = new StateClassData();

            float w = rect.width - 14f;
            float totalInfoHeight = UITheme.RowHeight * 4f + 20f;
            float editorHeight = rect.height - totalInfoHeight - 4f;
            if (editorHeight < 80f) editorHeight = 80f;

            Rect viewRect = new Rect(0f, 0f, w, totalInfoHeight + editorHeight);
            _scrollPos = GUI.BeginScrollView(rect, _scrollPos, viewRect);

            float y = 0f;

            // --- Info card ---
            Rect infoCardRect = new Rect(4f, y, w - 8f, UITheme.RowHeight * 4f + 12f);
            UITheme.DrawSmallCard(infoCardRect);
            UITheme.DrawGroupLabel(new Rect(4f, y, w - 8f, UITheme.RowHeight), "STATE INFO");

            y += UITheme.RowHeight + 2f;
            float iw = w - 24f;

            DrawInfoRow(ref y, iw, "Name", state.Name);
            DrawInfoRow(ref y, iw, "Position", $"({state.Position.x:F1}, {state.Position.y:F1})");
            DrawInfoRow(ref y, iw, "Size", $"({state.Size.x:F0} x {state.Size.y:F0})");

            int connectionCount = CountStateConnections(state, connections);
            DrawInfoRow(ref y, iw, "Connections", connectionCount.ToString());

            y += 8f;

            // --- Events card ---
            Rect eventsCardRect = new Rect(4f, y, w - 8f, editorHeight + UITheme.RowHeight + 8f);
            UITheme.DrawSmallCard(eventsCardRect);
            UITheme.DrawGroupLabel(new Rect(4f, y, w - 8f, UITheme.RowHeight), "STATE CLASS EVENTS");

            y += UITheme.RowHeight + 4f;
            float ey = y;

            GUI.EndScrollView();

            float availableHeight = rect.height - ey - 4f;
            if (availableHeight < 60f) availableHeight = 60f;
            Rect stateClassRect = new Rect(rect.x + 4f, rect.y + ey, rect.width - 4f, availableHeight);

            _stateClassEditor.Draw(stateClassRect, state.StateClass, ref _stateClassScroll);
        }

        private void DrawConnectionContent(Rect rect, ConnectionView conn,
            List<BlackboardVariable> blackboardVariables)
        {
            if (conn.Conditions == null)
                conn.Conditions = new List<TransitionCondition>();

            float w = rect.width - 14f;
            float totalInfoHeight = UITheme.RowHeight * 3f + 20f;
            float editorHeight = rect.height - totalInfoHeight - 4f;
            if (editorHeight < 80f) editorHeight = 80f;

            Rect viewRect = new Rect(0f, 0f, w, totalInfoHeight + editorHeight);
            _scrollPos = GUI.BeginScrollView(rect, _scrollPos, viewRect);

            float y = 0f;

            // --- Info card ---
            Rect infoCardRect = new Rect(4f, y, w - 8f, UITheme.RowHeight * 2f + 12f);
            UITheme.DrawSmallCard(infoCardRect);
            UITheme.DrawGroupLabel(new Rect(4f, y, w - 8f, UITheme.RowHeight), "CONNECTION INFO");

            y += UITheme.RowHeight + 2f;
            float iw = w - 24f;
            DrawInfoRow(ref y, iw, "From", conn.From?.Name ?? "—");
            DrawInfoRow(ref y, iw, "To", conn.To?.Name ?? "—");

            y += 8f;

            // --- Conditions card ---
            Rect condCardRect = new Rect(4f, y, w - 8f, editorHeight + UITheme.RowHeight + 8f);
            UITheme.DrawSmallCard(condCardRect);
            UITheme.DrawGroupLabel(new Rect(4f, y, w - 8f, UITheme.RowHeight), "CONDITIONS");

            y += UITheme.RowHeight + 4f;
            float cy = y;

            GUI.EndScrollView();

            float availableHeight = rect.height - cy - 4f;
            if (availableHeight < 60f) availableHeight = 60f;
            Rect conditionRect = new Rect(rect.x + 4f, rect.y + cy, rect.width - 4f, availableHeight);

            _conditionEditor.Draw(conditionRect, conn.Conditions, blackboardVariables, ref _conditionScroll);
        }

        private static void DrawGroupContent(Rect rect, CommentGroupView group)
        {
            float y = 0f;
            float w = rect.width - 14f;

            float listHeight = Mathf.Min(group.Members.Count, 20) * UITheme.RowHeight;
            float totalHeight = UITheme.RowHeight * 3f + 20f + listHeight;
            Rect viewRect = new Rect(0f, 0f, w, Mathf.Max(totalHeight, rect.height));

            var scrollPos = Vector2.zero;
            scrollPos = GUI.BeginScrollView(rect, scrollPos, viewRect);

            // --- Info card ---
            Rect infoCardRect = new Rect(4f, y, w - 8f, UITheme.RowHeight * 2f + 12f);
            UITheme.DrawSmallCard(infoCardRect);
            UITheme.DrawGroupLabel(new Rect(4f, y, w - 8f, UITheme.RowHeight), "GROUP INFO");

            y += UITheme.RowHeight + 2f;
            float iw = w - 24f;
            DrawInfoRow(ref y, iw, "Label", group.Label);
            DrawInfoRow(ref y, iw, "Members", group.Members.Count.ToString());

            y += 8f;

            // --- Members card ---
            float membersCardH = UITheme.RowHeight + 8f + listHeight;
            Rect membersCardRect = new Rect(4f, y, w - 8f, membersCardH);
            UITheme.DrawSmallCard(membersCardRect);
            UITheme.DrawGroupLabel(new Rect(4f, y, w - 8f, UITheme.RowHeight), "MEMBERS");

            y += UITheme.RowHeight + 4f;

            for (int i = 0; i < group.Members.Count; i++)
            {
                if (y + UITheme.RowHeight > viewRect.height)
                    break;

                Rect rowRect = new Rect(12f, y, w - 24f, UITheme.RowHeight);
                EditorGUI.DrawRect(rowRect, i % 2 == 0 ? UITheme.RowEven : UITheme.RowOdd);
                GUI.Label(new Rect(20f, rowRect.y, w - 28f, rowRect.height),
                    group.Members[i].Name, UITheme.SecondaryStyle);
                y += UITheme.RowHeight;
            }

            GUI.EndScrollView();
        }

        private static void DrawOtherContent(Rect rect, ISelectable item)
        {
            float y = 0f;
            float w = rect.width - 14f;

            Rect viewRect = new Rect(0f, 0f, w, UITheme.RowHeight * 4f);
            var scrollPos = Vector2.zero;
            scrollPos = GUI.BeginScrollView(rect, scrollPos, viewRect);

            Rect infoCardRect = new Rect(4f, y, w - 8f, UITheme.RowHeight + 12f);
            UITheme.DrawSmallCard(infoCardRect);
            UITheme.DrawGroupLabel(new Rect(4f, y, w - 8f, UITheme.RowHeight), "INSPECTOR");

            y += UITheme.RowHeight + 4f;
            DrawInfoRow(ref y, w - 16f, "Type", item.GetType().Name);

            GUI.EndScrollView();
        }

        private static void DrawMultiSelection(Rect rect, IReadOnlyList<ISelectable> selected)
        {
            float y = 0f;
            float w = rect.width - 14f;

            float totalHeight = UITheme.RowHeight * 2f + 20f + selected.Count * UITheme.RowHeight;
            Rect viewRect = new Rect(0f, 0f, w, totalHeight);

            var scrollPos = Vector2.zero;
            scrollPos = GUI.BeginScrollView(rect, scrollPos, viewRect);

            Rect cardRect = new Rect(4f, y, w - 8f, totalHeight - 8f);
            UITheme.DrawSmallCard(cardRect);
            UITheme.DrawGroupLabel(new Rect(4f, y, w - 8f, UITheme.RowHeight), $"SELECTED ({selected.Count})");

            y += UITheme.RowHeight + 4f;

            for (int i = 0; i < selected.Count; i++)
            {
                Rect rowRect = new Rect(12f, y, w - 24f, UITheme.RowHeight);
                EditorGUI.DrawRect(rowRect, i % 2 == 0 ? UITheme.RowEven : UITheme.RowOdd);

                string label = selected[i] switch
                {
                    StateView sv => sv.Name,
                    CommentGroupView gv => gv.Label,
                    ConnectionView cv => $"{cv.From?.Name ?? "?"} → {cv.To?.Name ?? "?"}",
                    _ => selected[i].GetType().Name
                };

                string typeLabel = selected[i] switch
                {
                    StateView => "STATE",
                    CommentGroupView => "GROUP",
                    ConnectionView => "CONNECTION",
                    _ => "ITEM"
                };

                Rect typeRect = new Rect(18f, rowRect.y + 3f, 56f, UITheme.RowHeight - 6f);
                EditorGUI.DrawRect(typeRect, UITheme.TypeBadgeBg);
                var typeStyle = new GUIStyle(UITheme.TypeBadgeStyle) { fontSize = 7 };
                GUI.Label(typeRect, typeLabel, typeStyle);

                Rect labelRect = new Rect(80f, rowRect.y, w - 92f, rowRect.height);
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
            Rect rect = new Rect(8f, y, width, UITheme.RowHeight);
            EditorGUI.DrawRect(rect, UITheme.RowEven);

            float labelWidth = 80f;
            Rect labelRect = new Rect(rect.x + 6f, rect.y, labelWidth, rect.height);
            Rect valueRect = new Rect(rect.x + labelWidth + 2f, rect.y, width - labelWidth - 8f, rect.height);

            GUI.Label(labelRect, label, UITheme.LabelStyle);
            GUI.Label(valueRect, value, UITheme.SecondaryStyle);

            y += UITheme.RowHeight;
        }
    }
}

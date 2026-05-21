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
            EditorGUI.DrawRect(rect, UITheme.PanelHeaderBg);
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
            GUI.Label(rect, "Select an item\nto inspect", UITheme.EmptyStyle);
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
            float headerHeight = UITheme.RowHeight * 5f + 4f;
            Rect headerArea = new Rect(0f, 0f, rect.width - 14f, headerHeight);

            float availableForEditor = rect.height - headerHeight;
            if (availableForEditor < 60f) availableForEditor = 60f;

            Rect editorRect = new Rect(0f, headerHeight, rect.width - 14f, availableForEditor);

            Rect viewRect = new Rect(0f, 0f, rect.width - 14f, rect.height);

            _scrollPos = GUI.BeginScrollView(rect, _scrollPos, viewRect);

            float y = 0f;
            float w = viewRect.width;

            DrawSectionHeader(ref y, w, "State");
            DrawLabelValueRow(ref y, w, "Name", state.Name);
            DrawLabelValueRow(ref y, w, "Position",
                $"({state.Position.x:F1}, {state.Position.y:F1})");
            DrawLabelValueRow(ref y, w, "Size",
                $"({state.Size.x:F0}, {state.Size.y:F0})");

            int connectionCount = CountStateConnections(state, connections);
            DrawLabelValueRow(ref y, w, "Connections", connectionCount.ToString());

            y += 4f;

            if (state.StateClass == null)
                state.StateClass = new StateClassData();

            DrawSectionHeader(ref y, w, "State Class Events");
            y += 0f;

            float editorY = y;
            GUI.EndScrollView();

            float editorHeight = rect.height - editorY;
            if (editorHeight < 60f) editorHeight = 60f;
            Rect stateClassRect = new Rect(rect.x, rect.y + editorY, rect.width, editorHeight);

            _stateClassEditor.Draw(stateClassRect, state.StateClass, ref _stateClassScroll);
        }

        private void DrawConnectionContent(Rect rect, ConnectionView conn,
            List<BlackboardVariable> blackboardVariables)
        {
            float infoHeight = UITheme.RowHeight * 3f + 4f;
            float availableForEditor = rect.height - infoHeight;
            if (availableForEditor < 60f) availableForEditor = 60f;

            Rect infoArea = new Rect(0f, 0f, rect.width - 14f, infoHeight);

            Rect viewRect = new Rect(0f, 0f, rect.width - 14f, rect.height);
            _scrollPos = GUI.BeginScrollView(rect, _scrollPos, viewRect);

            float y = 0f;
            float w = viewRect.width;

            DrawSectionHeader(ref y, w, "Connection");
            DrawLabelValueRow(ref y, w, "From", conn.From?.Name ?? "—");
            DrawLabelValueRow(ref y, w, "To", conn.To?.Name ?? "—");

            y += 4f;

            GUI.EndScrollView();

            float editorHeight = rect.height - y;
            if (editorHeight < 60f) editorHeight = 60f;
            Rect conditionRect = new Rect(rect.x, rect.y + y, rect.width, editorHeight);

            if (conn.Conditions == null)
                conn.Conditions = new List<TransitionCondition>();

            _conditionEditor.Draw(conditionRect, conn.Conditions, blackboardVariables, ref _conditionScroll);
        }

        private static void DrawGroupContent(Rect rect, CommentGroupView group)
        {
            float totalHeight = (3f + Mathf.Min(group.Members.Count, 20)) * UITheme.RowHeight + 20f;
            Rect viewRect = new Rect(0f, 0f, rect.width - 14f, totalHeight);

            var scrollPos = Vector2.zero;
            scrollPos = GUI.BeginScrollView(rect, scrollPos, viewRect);

            float y = 0f;
            float w = viewRect.width;

            DrawSectionHeader(ref y, w, "Group");
            DrawLabelValueRow(ref y, w, "Label", group.Label);
            DrawLabelValueRow(ref y, w, "Members", group.Members.Count.ToString());

            for (int i = 0; i < group.Members.Count; i++)
            {
                if (y + UITheme.RowHeight > viewRect.height)
                    break;

                string memberInfo = $"  - {group.Members[i].Name}";
                DrawLabelRow(ref y, w, memberInfo, UITheme.TextSecondary);
            }

            GUI.EndScrollView();
        }

        private static void DrawOtherContent(Rect rect, ISelectable item)
        {
            Rect viewRect = new Rect(0f, 0f, rect.width - 14f, UITheme.RowHeight * 4f);
            var scrollPos = Vector2.zero;
            scrollPos = GUI.BeginScrollView(rect, scrollPos, viewRect);

            float y = 0f;
            float w = viewRect.width;

            DrawSectionHeader(ref y, w, "Inspector");
            DrawLabelValueRow(ref y, w, "Type", item.GetType().Name);

            GUI.EndScrollView();
        }

        private static void DrawMultiSelection(Rect rect, IReadOnlyList<ISelectable> selected)
        {
            float totalHeight = (2f + selected.Count) * UITheme.RowHeight + 8f;
            Rect viewRect = new Rect(0f, 0f, rect.width - 14f, totalHeight);

            var scrollPos = Vector2.zero;
            scrollPos = GUI.BeginScrollView(rect, scrollPos, viewRect);

            float y = 0f;
            float w = viewRect.width;

            DrawSectionHeader(ref y, w, $"Selected ({selected.Count})");

            for (int i = 0; i < selected.Count; i++)
            {
                string label = selected[i] switch
                {
                    StateView sv => $"State: {sv.Name}",
                    CommentGroupView gv => $"Group: {gv.Label}",
                    ConnectionView cv => $"Connection: {cv.From?.Name ?? "?"} → {cv.To?.Name ?? "?"}",
                    _ => selected[i].GetType().Name
                };
                DrawLabelRow(ref y, w, label, UITheme.TextSecondary);
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

        private static void DrawSectionHeader(ref float y, float width, string label)
        {
            Rect rect = new Rect(0f, y, width, UITheme.RowHeight);
            EditorGUI.DrawRect(rect, UITheme.PanelHeaderBg);
            GUI.Label(rect, label, UITheme.SectionStyle);
            y += UITheme.RowHeight;
        }

        private static void DrawLabelValueRow(ref float y, float width, string label, string value)
        {
            Rect rect = new Rect(0f, y, width, UITheme.RowHeight);
            Color rowBg = ((int)(y / UITheme.RowHeight)) % 2 == 0 ? UITheme.RowEven : UITheme.RowOdd;
            EditorGUI.DrawRect(rect, rowBg);

            float labelWidth = width * 0.4f;
            Rect labelRect = new Rect(rect.x, rect.y, labelWidth, rect.height);
            Rect valueRect = new Rect(rect.x + labelWidth, rect.y, width - labelWidth, rect.height);

            GUI.Label(labelRect, label, UITheme.LabelStyle);
            GUI.Label(valueRect, value, UITheme.SecondaryStyle);

            y += UITheme.RowHeight;
        }

        private static void DrawLabelRow(ref float y, float width, string text, Color color)
        {
            Rect rect = new Rect(0f, y, width, UITheme.RowHeight);
            Color rowBg = ((int)(y / UITheme.RowHeight)) % 2 == 0 ? UITheme.RowEven : UITheme.RowOdd;
            EditorGUI.DrawRect(rect, rowBg);

            var style = new GUIStyle(UITheme.SecondaryStyle)
            {
                normal = { textColor = color }
            };
            GUI.Label(rect, text, style);

            y += UITheme.RowHeight;
        }
    }
}

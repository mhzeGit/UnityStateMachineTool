using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CleanStateMachine
{
    public class DetailsPanelView
    {
        private Vector2 _scrollPos;

        public void Draw(Rect rect, IReadOnlyList<ISelectable> selected,
            List<StateView> states, List<ConnectionView> connections)
        {
            var e = Event.current;

            UITheme.DrawPanelBackground(rect);

            Rect headerRect = new Rect(rect.x, rect.y, rect.width, UITheme.HeaderHeight);
            DrawHeader(headerRect);

            Rect contentRect = new Rect(
                rect.x,
                rect.y + UITheme.HeaderHeight,
                rect.width,
                rect.height - UITheme.HeaderHeight
            );
            DrawContent(contentRect, selected, states, connections);
        }

        private void DrawHeader(Rect rect)
        {
            EditorGUI.DrawRect(rect, UITheme.PanelHeaderBg);

            GUI.Label(rect, "Details", UITheme.HeaderStyle);
        }

        private void DrawContent(Rect rect, IReadOnlyList<ISelectable> selected,
            List<StateView> states, List<ConnectionView> connections)
        {
            if (selected.Count == 0)
            {
                DrawEmptyState(rect);
                return;
            }

            if (selected.Count == 1)
            {
                DrawSingleSelection(rect, selected[0], connections);
                return;
            }

            DrawMultiSelection(rect, selected, connections);
        }

        private static void DrawEmptyState(Rect rect)
        {
            GUI.Label(rect,
                "Select an item\nto inspect",
                UITheme.EmptyStyle);
        }

        private void DrawSingleSelection(Rect rect, ISelectable item, List<ConnectionView> connections)
        {
            float totalHeight = 0f;

            if (item is StateView state)
            {
                totalHeight = ComputeStateContentHeight() * UITheme.RowHeight;
            }
            else if (item is CommentGroupView group)
            {
                totalHeight = ComputeGroupContentHeight(group) * UITheme.RowHeight;
            }
            else
            {
                totalHeight = 3f * UITheme.RowHeight;
            }

            Rect viewRect = new Rect(0f, 0f, rect.width - 14f, totalHeight);

            _scrollPos = GUI.BeginScrollView(rect, _scrollPos, viewRect);

            float y = 0f;

            if (item is StateView s)
            {
                DrawSectionHeader(ref y, viewRect.width, "State");

                DrawLabelValueRow(ref y, viewRect.width, "Name", s.Name);
                DrawLabelValueRow(ref y, viewRect.width, "Position",
                    $"({s.Position.x:F1}, {s.Position.y:F1})");
                DrawLabelValueRow(ref y, viewRect.width, "Size",
                    $"({s.Size.x:F0}, {s.Size.y:F0})");

                int connectionCount = CountStateConnections(s, connections);
                DrawLabelValueRow(ref y, viewRect.width, "Connections", connectionCount.ToString());
            }
            else if (item is CommentGroupView g)
            {
                DrawSectionHeader(ref y, viewRect.width, "Group");

                DrawLabelValueRow(ref y, viewRect.width, "Label", g.Label);
                DrawLabelValueRow(ref y, viewRect.width, "Members", g.Members.Count.ToString());

                for (int i = 0; i < g.Members.Count; i++)
                {
                    if (y + UITheme.RowHeight > viewRect.height)
                        break;

                    string memberInfo = $"  - {g.Members[i].Name}";
                    DrawLabelRow(ref y, viewRect.width, memberInfo, UITheme.TextSecondary);
                }
            }
            else
            {
                DrawLabelValueRow(ref y, viewRect.width, "Type", item.GetType().Name);
            }

            GUI.EndScrollView();
        }

        private void DrawMultiSelection(Rect rect, IReadOnlyList<ISelectable> selected,
            List<ConnectionView> connections)
        {
            float totalHeight = (2f + selected.Count) * UITheme.RowHeight;

            Rect viewRect = new Rect(0f, 0f, rect.width - 14f, totalHeight);

            _scrollPos = GUI.BeginScrollView(rect, _scrollPos, viewRect);

            float y = 0f;

            DrawSectionHeader(ref y, viewRect.width, $"Selected ({selected.Count})");

            for (int i = 0; i < selected.Count; i++)
            {
                string label = selected[i] switch
                {
                    StateView sv => $"State: {sv.Name}",
                    CommentGroupView gv => $"Group: {gv.Label}",
                    _ => selected[i].GetType().Name
                };
                DrawLabelRow(ref y, viewRect.width, label, UITheme.TextSecondary);
            }

            GUI.EndScrollView();
        }

        private static float ComputeStateContentHeight()
        {
            return 5f;
        }

        private static float ComputeGroupContentHeight(CommentGroupView group)
        {
            return 3f + Mathf.Min(group.Members.Count, 20);
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

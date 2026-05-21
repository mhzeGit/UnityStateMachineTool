using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CleanStateMachine
{
    public class StateClassEditor
    {
        private readonly HashSet<string> _expandedSections = new HashSet<string>();
        private readonly HashSet<int> _expandedEvents = new HashSet<int>();
        private int _nextEventId = 1;

        public event Action Changed;

        public StateClassEditor()
        {
            _expandedSections.Add("OnStateEnter");
        }

        public void Draw(Rect rect, StateClassData stateClass, ref Vector2 scrollPos)
        {
            if (stateClass == null) return;

            EnsureEventIds(stateClass);

            float totalHeight = ComputeTotalHeight(stateClass);
            Rect viewRect = new Rect(0f, 0f, rect.width - 14f, Mathf.Max(totalHeight, rect.height));
            scrollPos = GUI.BeginScrollView(rect, scrollPos, viewRect);

            float y = 0f;
            float w = viewRect.width;

            DrawSectionHeader(ref y, w, "State Class");

            for (int i = 0; i < stateClass.Sections.Count; i++)
            {
                var section = stateClass.Sections[i];
                DrawSection(ref y, w, section, i);
            }

            if (stateClass.Sections.Count == 0)
            {
                GUI.Label(new Rect(0f, y, w, UITheme.RowHeight * 2f), "No sections defined", UITheme.EmptyStyle);
            }

            GUI.EndScrollView();
        }

        private void DrawSection(ref float y, float width,
            StateSectionData section, int sectionIndex)
        {
            bool isExpanded = _expandedSections.Contains(section.SectionName);

            DrawSectionFoldoutHeader(ref y, width, section.SectionName, isExpanded);
            DrawAddEventButton(ref y, width, section, sectionIndex);

            if (!isExpanded) return;

            if (section.Events.Count == 0)
            {
                Rect emptyRect = new Rect(UITheme.Padding * 2f, y, width - UITheme.Padding * 4f, UITheme.RowHeight);
                GUI.Label(emptyRect, "  No events. Press + to add.", UITheme.SecondaryStyle);
                y += UITheme.RowHeight;
                return;
            }

            for (int i = 0; i < section.Events.Count; i++)
            {
                var evt = section.Events[i];
                int eventId = evt.EditorId;
                bool eventExpanded = _expandedEvents.Contains(eventId);
                DrawEventRow(ref y, width, section, i, evt, eventId, eventExpanded);
            }
        }

        private void DrawSectionFoldoutHeader(ref float y, float width, string sectionName, bool isExpanded)
        {
            Rect headerRect = new Rect(0f, y, width, UITheme.RowHeight);
            Color rowBg = ((int)(y / UITheme.RowHeight)) % 2 == 0 ? UITheme.RowEven : UITheme.RowOdd;
            EditorGUI.DrawRect(headerRect, rowBg);

            string label = (isExpanded ? "▼ " : "▶ ") + sectionName;
            var style = new GUIStyle(UITheme.LabelStyle) { fontStyle = FontStyle.Bold };
            Rect foldoutRect = new Rect(headerRect.x + UITheme.Padding, headerRect.y,
                headerRect.width - UITheme.Padding * 2f, headerRect.height);

            if (GUI.Button(foldoutRect, label, style))
            {
                if (isExpanded)
                    _expandedSections.Remove(sectionName);
                else
                    _expandedSections.Add(sectionName);
            }

            y += UITheme.RowHeight;
        }

        private void DrawAddEventButton(ref float y, float width, StateSectionData section, int sectionIndex)
        {
            Rect addRect = new Rect(0f, y, width, UITheme.RowHeight);
            Color rowBg = ((int)(y / UITheme.RowHeight)) % 2 == 0 ? UITheme.RowEven : UITheme.RowOdd;
            EditorGUI.DrawRect(addRect, rowBg);

            Rect btnRect = new Rect(addRect.x + UITheme.Padding * 2f, addRect.y + 2f, 22f, addRect.height - 4f);
            Color btnColor = btnRect.Contains(Event.current.mousePosition) ? UITheme.ButtonHover : UITheme.ButtonColor;
            EditorGUI.DrawRect(btnRect, btnColor);

            var btnStyle = new GUIStyle
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 14,
                normal = { textColor = UITheme.Accent }
            };

            if (GUI.Button(btnRect, "+", btnStyle))
            {
                ShowAddEventMenu(section, btnRect);
            }

            y += UITheme.RowHeight;
        }

        private void ShowAddEventMenu(StateSectionData section, Rect buttonRect)
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("Debug Log"), false,
                () => { var evt = CreateEvent(StateMachineEventType.DebugLog); AssignEventId(evt); section.Events.Add(evt); Changed?.Invoke(); });
            menu.AddItem(new GUIContent("Wait"), false,
                () => { var evt = CreateEvent(StateMachineEventType.Wait); AssignEventId(evt); section.Events.Add(evt); Changed?.Invoke(); });
            menu.AddItem(new GUIContent("Unity Event"), false,
                () => { var evt = CreateEvent(StateMachineEventType.UnityEvent); AssignEventId(evt); section.Events.Add(evt); Changed?.Invoke(); });
            menu.AddItem(new GUIContent("Custom"), false,
                () => { var evt = CreateEvent(StateMachineEventType.Custom); AssignEventId(evt); section.Events.Add(evt); Changed?.Invoke(); });
            menu.DropDown(buttonRect);
        }

        private static StateMachineEventData CreateEvent(StateMachineEventType type)
        {
            var evt = new StateMachineEventData
            {
                Type = type,
                DebugMessage = "",
                WaitDuration = 0.5f,
                UnityEventCallbacks = new List<UnityEventCallbackData>(),
                CustomText = ""
            };
            return evt;
        }

        private int AssignEventId(StateMachineEventData evt)
        {
            evt.EditorId = _nextEventId++;
            return evt.EditorId;
        }

        private void EnsureEventIds(StateClassData stateClass)
        {
            for (int i = 0; i < stateClass.Sections.Count; i++)
            {
                for (int j = 0; j < stateClass.Sections[i].Events.Count; j++)
                {
                    var evt = stateClass.Sections[i].Events[j];
                    if (evt.EditorId == 0)
                        evt.EditorId = _nextEventId++;
                }
            }
        }

        private void DrawEventRow(ref float y, float width, StateSectionData section,
            int index, StateMachineEventData evt, int eventId, bool isExpanded)
        {
            float rowHeight = isExpanded ? GetExpandedEventHeight(evt) : UITheme.RowHeight;
            Rect rowRect = new Rect(0f, y, width, rowHeight);
            Color rowBg = ((int)(y / UITheme.RowHeight)) % 2 == 0 ? UITheme.RowEven : UITheme.RowOdd;
            EditorGUI.DrawRect(rowRect, rowBg);

            Rect summaryRect = new Rect(rowRect.x + UITheme.Padding * 3f, rowRect.y,
                rowRect.width - UITheme.Padding * 6f - 24f, UITheme.RowHeight);
            string arrow = isExpanded ? " ▲" : " ▼";

            if (GUI.Button(summaryRect, GetEventSummary(evt) + arrow, UITheme.SecondaryStyle))
            {
                if (isExpanded)
                    _expandedEvents.Remove(eventId);
                else
                    _expandedEvents.Add(eventId);
            }

            Rect deleteRect = new Rect(rowRect.xMax - 22f, rowRect.y + 2f, 18f, UITheme.RowHeight - 4f);
            if (GUI.Button(deleteRect, "✕", UITheme.CloseButtonStyle))
            {
                section.Events.RemoveAt(index);
                _expandedEvents.Remove(eventId);
                Changed?.Invoke();
            }

            y += UITheme.RowHeight;

            if (isExpanded)
            {
                DrawEventFields(ref y, width, evt);
            }
        }

        private void DrawEventFields(ref float y, float width, StateMachineEventData evt)
        {
            float indent = UITheme.Padding * 4f;
            float fieldWidth = width - indent - UITheme.Padding * 2f;

            switch (evt.Type)
            {
                case StateMachineEventType.DebugLog:
                    DrawTextField(ref y, width, indent, fieldWidth, "Message", ref evt.DebugMessage);
                    y += 4f;
                    break;

                case StateMachineEventType.Wait:
                    DrawFloatField(ref y, width, indent, fieldWidth, "Duration (s)", ref evt.WaitDuration);
                    y += 4f;
                    break;

                case StateMachineEventType.UnityEvent:
                    DrawUnityEventFields(ref y, width, indent, fieldWidth, evt);
                    y += 4f;
                    break;

                case StateMachineEventType.Custom:
                    DrawTextField(ref y, width, indent, fieldWidth, "Custom Text", ref evt.CustomText);
                    y += 4f;
                    break;
            }
        }

        private void DrawTextField(ref float y, float width, float indent, float fieldWidth,
            string label, ref string value)
        {
            Rect labelRect = new Rect(indent, y, fieldWidth * 0.3f, UITheme.RowHeight);
            GUI.Label(labelRect, label, UITheme.LabelStyle);

            Rect fieldRect = new Rect(indent + fieldWidth * 0.3f, y + 2f, fieldWidth * 0.7f, UITheme.RowHeight - 4f);
            string newValue = GUI.TextField(fieldRect, value ?? "");
            if (newValue != value)
            {
                value = newValue;
                Changed?.Invoke();
            }
            y += UITheme.RowHeight;
        }

        private void DrawFloatField(ref float y, float width, float indent, float fieldWidth,
            string label, ref float value)
        {
            Rect labelRect = new Rect(indent, y, fieldWidth * 0.3f, UITheme.RowHeight);
            GUI.Label(labelRect, label, UITheme.LabelStyle);

            Rect fieldRect = new Rect(indent + fieldWidth * 0.3f, y + 2f, fieldWidth * 0.7f, UITheme.RowHeight - 4f);
            float newValue = EditorGUI.FloatField(fieldRect, value);
            if (!Mathf.Approximately(newValue, value))
            {
                value = newValue;
                Changed?.Invoke();
            }
            y += UITheme.RowHeight;
        }

        private void DrawUnityEventFields(ref float y, float width, float indent, float fieldWidth,
            StateMachineEventData evt)
        {
            if (evt.UnityEventCallbacks == null)
                evt.UnityEventCallbacks = new List<UnityEventCallbackData>();

            Rect addRect = new Rect(indent, y, fieldWidth, UITheme.RowHeight);
            Rect addBtnRect = new Rect(addRect.x, addRect.y + 2f, 20f, addRect.height - 4f);
            Color btnColor = addBtnRect.Contains(Event.current.mousePosition) ? UITheme.ButtonHover : UITheme.ButtonColor;
            EditorGUI.DrawRect(addBtnRect, btnColor);
            var btnStyle = new GUIStyle
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 12,
                normal = { textColor = UITheme.Accent }
            };

            if (GUI.Button(addBtnRect, "+", btnStyle))
            {
                evt.UnityEventCallbacks.Add(new UnityEventCallbackData());
                Changed?.Invoke();
            }

            GUI.Label(new Rect(addRect.x + 24f, addRect.y, addRect.width - 24f, addRect.height),
                "Callbacks", UITheme.SecondaryStyle);
            y += UITheme.RowHeight;

            for (int i = 0; i < evt.UnityEventCallbacks.Count; i++)
            {
                var cb = evt.UnityEventCallbacks[i];
                DrawUnityCallbackRow(ref y, width, indent + UITheme.Padding,
                    fieldWidth - UITheme.Padding, evt.UnityEventCallbacks, i, cb);
            }
        }

        private void DrawUnityCallbackRow(ref float y, float width, float indent, float fieldWidth,
            List<UnityEventCallbackData> callbacks, int index, UnityEventCallbackData cb)
        {
            Color rowBg = ((int)(y / UITheme.RowHeight)) % 2 == 0 ? UITheme.RowEven : UITheme.RowOdd;
            EditorGUI.DrawRect(new Rect(0f, y, width + indent, UITheme.RowHeight), rowBg);

            Rect targetRect = new Rect(indent, y + 2f, fieldWidth * 0.5f - 2f, UITheme.RowHeight - 4f);
            var newTarget = EditorGUI.ObjectField(targetRect, cb.Target, typeof(UnityEngine.Object), true);
            if (newTarget != cb.Target)
            {
                cb.Target = newTarget;
                Changed?.Invoke();
            }

            Rect methodRect = new Rect(indent + fieldWidth * 0.5f, y + 2f, fieldWidth * 0.4f - 2f, UITheme.RowHeight - 4f);
            string newMethod = GUI.TextField(methodRect, cb.MethodName ?? "");
            if (newMethod != cb.MethodName)
            {
                cb.MethodName = newMethod;
                Changed?.Invoke();
            }

            Rect delRect = new Rect(indent + fieldWidth * 0.9f, y + 2f, 18f, UITheme.RowHeight - 4f);
            if (GUI.Button(delRect, "✕", UITheme.CloseButtonStyle))
            {
                callbacks.RemoveAt(index);
                Changed?.Invoke();
            }

            y += UITheme.RowHeight;
        }

        private static string GetEventSummary(StateMachineEventData evt)
        {
            switch (evt.Type)
            {
                case StateMachineEventType.DebugLog:
                    return $"  Debug Log: \"{Truncate(evt.DebugMessage, 30)}\"";
                case StateMachineEventType.Wait:
                    return $"  Wait: {evt.WaitDuration:F1}s";
                case StateMachineEventType.UnityEvent:
                    int count = evt.UnityEventCallbacks?.Count ?? 0;
                    return $"  Unity Event ({count} callback{(count != 1 ? "s" : "")})";
                case StateMachineEventType.Custom:
                    return $"  Custom: \"{Truncate(evt.CustomText, 28)}\"";
                default:
                    return "  Unknown";
            }
        }

        private float GetExpandedEventHeight(StateMachineEventData evt)
        {
            float h = UITheme.RowHeight;
            switch (evt.Type)
            {
                case StateMachineEventType.DebugLog:
                    h += UITheme.RowHeight + 4f;
                    break;
                case StateMachineEventType.Wait:
                    h += UITheme.RowHeight + 4f;
                    break;
                case StateMachineEventType.UnityEvent:
                    h += UITheme.RowHeight;
                    if (evt.UnityEventCallbacks != null)
                        h += evt.UnityEventCallbacks.Count * UITheme.RowHeight;
                    h += 4f;
                    break;
                case StateMachineEventType.Custom:
                    h += UITheme.RowHeight + 4f;
                    break;
            }
            return h;
        }

        private float ComputeTotalHeight(StateClassData stateClass)
        {
            float h = UITheme.RowHeight;
            for (int i = 0; i < stateClass.Sections.Count; i++)
            {
                var section = stateClass.Sections[i];
                h += UITheme.RowHeight * 2f;

                if (_expandedSections.Contains(section.SectionName))
                {
                    if (section.Events.Count == 0)
                    {
                        h += UITheme.RowHeight;
                    }
                    else
                    {
                        for (int j = 0; j < section.Events.Count; j++)
                        {
                            int eventId = section.Events[j].EditorId;
                            h += _expandedEvents.Contains(eventId)
                                ? GetExpandedEventHeight(section.Events[j])
                                : UITheme.RowHeight;
                        }
                    }
                }
            }
            return h + 20f;
        }

        private static string Truncate(string s, int maxLen)
        {
            if (string.IsNullOrEmpty(s)) return "";
            if (s.Length <= maxLen) return s;
            return s.Substring(0, maxLen) + "...";
        }

        private static void DrawSectionHeader(ref float y, float width, string label)
        {
            Rect rect = new Rect(0f, y, width, UITheme.RowHeight);
            EditorGUI.DrawRect(rect, UITheme.PanelHeaderBg);
            GUI.Label(rect, label, UITheme.SectionStyle);
            y += UITheme.RowHeight;
        }
    }
}

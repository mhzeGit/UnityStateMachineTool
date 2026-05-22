using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace CleanStateMachine
{
    public class DetailsPanel : VisualElement
    {
        private readonly CleanStateMachineWindow _window;
        private readonly ScrollView _scrollView;
        private IReadOnlyList<ISelectable> _selected;
        private List<StateView> _states;
        private List<ConnectionView> _connections;
        private List<BlackboardVariable> _blackboardVariables;

        private ScriptableObject _currentSO;
        private readonly Label _emptyLabel;

        public DetailsPanel(CleanStateMachineWindow window)
        {
            _window = window;
            AddToClassList("details-panel");

            var header = new VisualElement();
            header.AddToClassList("panel-header");

            var title = new Label("Inspector");
            title.AddToClassList("panel-title");
            header.Add(title);
            Add(header);

            _scrollView = new ScrollView();
            _scrollView.AddToClassList("details-scroll");
            Add(_scrollView);

            _emptyLabel = new Label("Select an item to inspect");
            _emptyLabel.AddToClassList("empty-state-label");
        }

        public void UpdateSelection(
            IReadOnlyList<ISelectable> selected,
            List<StateView> states,
            List<ConnectionView> connections,
            List<BlackboardVariable> blackboardVariables)
        {
            _selected = selected;
            _states = states;
            _connections = connections;
            _blackboardVariables = blackboardVariables;
            _currentSO = null;

            _scrollView.Clear();
            _emptyLabel.RemoveFromHierarchy();

            if (selected == null || selected.Count == 0)
            {
                var empty = new VisualElement();
                empty.AddToClassList("empty-state");
                empty.Add(_emptyLabel);
                _scrollView.Add(empty);
                return;
            }

            if (selected.Count == 1)
            {
                BuildSingleSelection(selected[0]);
            }
            else
            {
                BuildMultiSelection(selected);
            }
        }

        private void BuildSingleSelection(ISelectable item)
        {
            if (item is StateView state)
                BuildStateContent(state);
            else if (item is ConnectionView conn)
                BuildConnectionContent(conn);
            else if (item is CommentGroupView group)
                BuildGroupContent(group);
            else
                BuildOtherContent(item);
        }

        // ─── STATE CONTENT ──────────────────────────────────────────────

        private void BuildStateContent(StateView state)
        {
            AddSectionTitle("State Information");
            AddInfoRow("Name", state.Name);
            AddInfoRow("Position", $"({state.Position.x:F1}, {state.Position.y:F1})");
            AddInfoRow("Size", $"({state.Size.x:F0} x {state.Size.y:F0})");
            AddInfoRow("Connections", CountStateConnections(state).ToString());

            AddDivider();
            AddSectionTitle("State Behaviour");

            AddScriptRow(
                state.BehaviourScript,
                IsValidStateBehaviour,
                (prev, next) => OnStateScriptChanged(state, prev, next));

            if (state.BehaviourInstance != null)
            {
                _currentSO = state.BehaviourInstance;
                AddDivider();
                AddSectionTitle("Properties");
                AddSOProperties();
            }
        }

        private void OnStateScriptChanged(StateView state, MonoScript prev, MonoScript next)
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
            _window.NotifySidePanelChanged();
            UpdateSelection(_selected, _states, _connections, _blackboardVariables);
        }

        // ─── CONNECTION CONTENT ─────────────────────────────────────────

        private void BuildConnectionContent(ConnectionView conn)
        {
            AddSectionTitle("Connection Information");
            AddInfoRow("From", conn.From?.Name ?? "\u2014");
            AddInfoRow("To", conn.To?.Name ?? "\u2014");

            AddDivider();
            AddSectionTitle("Transition Condition");

            AddScriptRow(
                conn.ConditionScript,
                IsValidConditionScript,
                (prev, next) => OnConditionScriptChanged(conn, prev, next));

            if (conn.ConditionInstance != null)
            {
                _currentSO = conn.ConditionInstance;
                AddDivider();
                AddSectionTitle("Properties");
                AddSOProperties();
            }
        }

        private void OnConditionScriptChanged(ConnectionView conn, MonoScript prev, MonoScript next)
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
                    string fromName = conn.From?.Name ?? "?";
                    string toName = conn.To?.Name ?? "?";
                    conn.ConditionInstance.name = $"{fromName}->{toName}_Condition";
                    conn.ConditionInstance.hideFlags = HideFlags.HideInHierarchy;
                }
            }
            _window.NotifySidePanelChanged();
            UpdateSelection(_selected, _states, _connections, _blackboardVariables);
        }

        // ─── GROUP CONTENT ─────────────────────────────────────────────

        private void BuildGroupContent(CommentGroupView group)
        {
            AddSectionTitle("Group Information");
            AddInfoRow("Label", group.Label);
            AddInfoRow("Members", group.Members.Count.ToString());

            AddDivider();
            AddSectionTitle("Members");

            for (int i = 0; i < group.Members.Count; i++)
            {
                var row = new VisualElement();
                row.AddToClassList("member-row");
                row.AddToClassList(i % 2 == 0 ? "member-row-even" : "member-row-odd");

                var nameLabel = new Label(group.Members[i].Name);
                nameLabel.AddToClassList("member-name");
                row.Add(nameLabel);
                _scrollView.Add(row);
            }
        }

        // ─── OTHER CONTENT ─────────────────────────────────────────────

        private void BuildOtherContent(ISelectable item)
        {
            AddSectionTitle("Inspector");
            AddInfoRow("Type", item.GetType().Name);
        }

        // ─── MULTI SELECTION ──────────────────────────────────────────

        private void BuildMultiSelection(IReadOnlyList<ISelectable> selected)
        {
            AddSectionTitle($"Selected ({selected.Count})");

            for (int i = 0; i < selected.Count; i++)
            {
                var row = new VisualElement();
                row.AddToClassList("multi-select-row");
                if (i % 2 == 0)
                    row.style.backgroundColor = new StyleColor(new Color(0.14f, 0.14f, 0.14f));
                else
                    row.style.backgroundColor = new StyleColor(new Color(0.12f, 0.12f, 0.12f));

                string typeLabel = selected[i] switch
                {
                    StateView => "STATE",
                    CommentGroupView => "GROUP",
                    ConnectionView => "CONNECTION",
                    _ => "ITEM"
                };

                var badge = new Label(typeLabel);
                badge.AddToClassList("multi-select-badge");
                row.Add(badge);

                string nameLabel = selected[i] switch
                {
                    StateView sv => sv.Name,
                    CommentGroupView gv => gv.Label,
                    ConnectionView cv => $"{cv.From?.Name ?? "?"} \u2192 {cv.To?.Name ?? "?"}",
                    _ => selected[i].GetType().Name
                };

                var label = new Label(nameLabel);
                label.AddToClassList("multi-select-label");
                row.Add(label);

                _scrollView.Add(row);
            }
        }

        // ─── HELPER BUILDERS ──────────────────────────────────────────

        private void AddSectionTitle(string text)
        {
            var title = new Label(text);
            title.AddToClassList("section-title");
            _scrollView.Add(title);
        }

        private void AddInfoRow(string label, string value)
        {
            var row = new VisualElement();
            row.AddToClassList("info-row");

            var labelEl = new Label(label);
            labelEl.AddToClassList("info-row-label");
            row.Add(labelEl);

            var valueEl = new Label(value);
            valueEl.AddToClassList("info-row-value");
            row.Add(valueEl);

            _scrollView.Add(row);
        }

        private void AddDivider()
        {
            var div = new VisualElement();
            div.AddToClassList("section-divider");
            _scrollView.Add(div);
        }

        private void AddScriptRow(MonoScript currentScript,
            Func<MonoScript, bool> isValid,
            Action<MonoScript, MonoScript> onAssign)
        {
            var row = new VisualElement();
            row.AddToClassList("script-row");

            var contentRow = new VisualElement();
            contentRow.AddToClassList("script-row-content");

            var label = new Label("Script");
            label.AddToClassList("script-row-label");
            contentRow.Add(label);

            var field = new ObjectField();
            field.AddToClassList("script-field");
            field.objectType = typeof(MonoScript);
            field.value = currentScript;
            field.RegisterValueChangedCallback(e =>
            {
                var newScript = e.newValue as MonoScript;
                if (newScript != null && !isValid(newScript))
                {
                    field.SetValueWithoutNotify(e.previousValue);
                    EditorUtility.DisplayDialog("Invalid Script",
                        "The selected script must inherit from the required base class.", "OK");
                    return;
                }
                onAssign(e.previousValue as MonoScript, newScript);
            });
            contentRow.Add(field);

            if (currentScript != null)
            {
                var openBtn = new Button(() => AssetDatabase.OpenAsset(currentScript));
                openBtn.text = "Open";
                openBtn.AddToClassList("script-open-button");
                contentRow.Add(openBtn);
            }

            row.Add(contentRow);

            // Type name / hint below
            if (currentScript != null)
            {
                var scriptType = currentScript.GetClass();
                string typeName = scriptType != null ? scriptType.Name : currentScript.name;
                var typeLabel = new Label(typeName);
                typeLabel.AddToClassList("script-type-name");
                row.Add(typeLabel);
            }
            else
            {
                var hint = new Label("Assign a script to define behaviour");
                hint.AddToClassList("script-type-name");
                row.Add(hint);
            }

            _scrollView.Add(row);
        }

        private void AddSOProperties()
        {
            if (_currentSO == null) return;

            float pad = 4f;
            float labelH = 18f;
            float fieldH = 22f;
            float gap = 4f;
            float border = 1f;

            float totalHeight = 2f;
            var so = new SerializedObject(_currentSO);
            var prop = so.GetIterator();
            bool enterChildren = true;
            while (prop.NextVisible(enterChildren))
            {
                enterChildren = false;
                if (prop.name == "m_Script") continue;
                bool isBbRef = prop.type == "BlackboardVariableReference";
                float contentH = isBbRef ? labelH + gap + fieldH : labelH + gap + fieldH;
                totalHeight += pad + contentH + pad + border * 2f;
            }

            IMGUIContainer imContainer = null;
            imContainer = new IMGUIContainer(() =>
            {
                if (_currentSO == null) return;

                float w = imContainer.layout.width;
                if (w < 1f) w = 200f;

                var so2 = new SerializedObject(_currentSO);
                so2.Update();

                float y = 0f;

                var prop2 = so2.GetIterator();
                bool enter = true;
                while (prop2.NextVisible(enter))
                {
                    enter = false;
                    if (prop2.name == "m_Script") continue;

                    bool isBbRef = prop2.type == "BlackboardVariableReference";
                    float contentH = isBbRef ? labelH + gap + fieldH : labelH + gap + fieldH;
                    float cardH = pad + contentH + pad;
                    float rowH = cardH + border * 2f;

                    EditorGUI.DrawRect(new Rect(0f, y, w, rowH), UITheme.PanelBorder);
                    EditorGUI.DrawRect(new Rect(border, y + border, w - border * 2f, cardH), UITheme.PanelBg);

                    float innerX = 8f;
                    float innerW = w - 16f;
                    float cy = y + border + pad;

                    EditorGUI.LabelField(new Rect(innerX, cy, innerW, labelH),
                        prop2.displayName, UITheme.VariableLabelStyle);
                    cy += labelH + gap;

                    if (isBbRef)
                    {
                        var useBbProp = prop2.FindPropertyRelative("UseBlackboard");
                        var varNameProp = prop2.FindPropertyRelative("BlackboardVariableName");
                        var valueTypeProp = prop2.FindPropertyRelative("ValueType");
                        var defaultValueProp = prop2.FindPropertyRelative("DefaultValue");
                        var bbType = (BlackboardVariableType)valueTypeProp.enumValueIndex;
                        bool useBb = useBbProp.boolValue;

                        float modeW = Mathf.Min(130f, innerW * 0.4f);
                        DrawModeToggle(new Rect(innerX, cy, modeW, fieldH), useBb, useBbProp);

                        float fieldX = innerX + modeW + 4f;
                        float fieldW = innerW - modeW - 4f;
                        var fRect = new Rect(fieldX, cy, fieldW, fieldH);
                        if (useBb)
                            DrawBbDropdown(fRect, varNameProp, useBbProp, bbType, _blackboardVariables);
                        else
                            DrawValueField(fRect, bbType, defaultValueProp);
                    }
                    else
                    {
                        EditorGUI.PropertyField(new Rect(innerX, cy, innerW, fieldH),
                            prop2, GUIContent.none, true);
                    }

                    y += rowH;
                }

                if (so2.ApplyModifiedProperties())
                    EditorUtility.SetDirty(_currentSO);
            });

            imContainer.style.height = totalHeight;
            _scrollView.Add(imContainer);
        }

        // ─── VALIDATION ────────────────────────────────────────────────

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

        // ─── UTILITY ───────────────────────────────────────────────────

        private int CountStateConnections(StateView state)
        {
            if (_connections == null) return 0;
            int count = 0;
            for (int i = 0; i < _connections.Count; i++)
            {
                if (_connections[i].From == state || _connections[i].To == state)
                    count++;
            }
            return count;
        }

        // ─── STATIC HELPERS (Blackboard Variable Ref) ──────────────────

        internal static void DrawBlackboardVariableRefField(Rect rect, SerializedProperty prop,
            List<BlackboardVariable> blackboardVariables, float labelWidth = 72f)
        {
            var useBbProp = prop.FindPropertyRelative("UseBlackboard");
            var varNameProp = prop.FindPropertyRelative("BlackboardVariableName");
            var valueTypeProp = prop.FindPropertyRelative("ValueType");
            var defaultValueProp = prop.FindPropertyRelative("DefaultValue");

            var bbType = (BlackboardVariableType)valueTypeProp.enumValueIndex;
            bool useBb = useBbProp.boolValue;

            float gap = 4f;

            Rect labelRect = new Rect(8f, rect.y + 1f, labelWidth, rect.height - 2f);
            GUI.Label(labelRect, prop.displayName, UITheme.VariableLabelStyle);

            float modeX = 8f + labelWidth + 8f;
            float modeWidth = rect.width - modeX - 8f;
            if (modeWidth > 120f) modeWidth = 120f;
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
            string display = useBb ? "Blackboard" : "Direct";

            var popupStyle = new GUIStyle(EditorStyles.popup)
            {
                fontSize = 11,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = UITheme.TextColor, background = null },
                hover = { textColor = UITheme.TextColor },
                focused = { textColor = UITheme.TextColor }
            };

            if (GUI.Button(rect, new GUIContent(display), popupStyle))
            {
                var menu = new GenericMenu();
                menu.AddItem(new GUIContent("Direct"), !useBb, () =>
                {
                    useBbProp.boolValue = false;
                    useBbProp.serializedObject.ApplyModifiedProperties();
                });
                menu.AddItem(new GUIContent("Blackboard"), useBb, () =>
                {
                    useBbProp.boolValue = true;
                    useBbProp.serializedObject.ApplyModifiedProperties();
                });
                menu.DropDown(rect);
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

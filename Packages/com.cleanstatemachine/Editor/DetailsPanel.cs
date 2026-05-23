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

            if (!state.IsEntry)
            {
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
            AddSectionTitle("Transition Conditions");

            for (int i = 0; i < conn.ConditionEntries.Count; i++)
            {
                BuildConditionEntry(conn, i);
            }

            var addBtn = new Button(() =>
            {
                conn.ConditionEntries.Add(new ConditionEntryView());
                _window.NotifySidePanelChanged();
                UpdateSelection(_selected, _states, _connections, _blackboardVariables);
            });
            addBtn.text = "+ Add Condition";
            addBtn.AddToClassList("add-condition-button");
            _scrollView.Add(addBtn);
        }

        private void BuildConditionEntry(ConnectionView conn, int index)
        {
            var entry = conn.ConditionEntries[index];

            var container = new VisualElement();
            container.AddToClassList("condition-entry");

            var header = new VisualElement();
            header.AddToClassList("condition-entry-header");

            var headerLabel = new Label($"Condition {index + 1}");
            headerLabel.AddToClassList("condition-entry-label");
            header.Add(headerLabel);

            var removeBtn = new Button(() =>
            {
                if (entry.Instance != null)
                {
                    Object.DestroyImmediate(entry.Instance, true);
                    entry.Instance = null;
                }
                conn.ConditionEntries.RemoveAt(index);
                _window.NotifySidePanelChanged();
                UpdateSelection(_selected, _states, _connections, _blackboardVariables);
            });
            removeBtn.text = "X";
            removeBtn.AddToClassList("condition-remove-button");
            header.Add(removeBtn);

            container.Add(header);

            var scriptRow = new VisualElement();
            scriptRow.AddToClassList("condition-script-row");

            var pickerBtn = new Button();
            pickerBtn.AddToClassList("script-picker-button");
            pickerBtn.text = entry.Script != null ? GetConditionDisplayName(entry.Script) : "None (Select...)";
            var capturedIndex = index;
            pickerBtn.clicked += () =>
            {
                var filtered = FindFilteredScripts(IsValidConditionScript);
                var pos = _window.rootVisualElement.WorldToLocal(
                    new Vector2(pickerBtn.worldBound.x, pickerBtn.worldBound.y + pickerBtn.worldBound.height));
                MenuDropdown.Show(_window.rootVisualElement, pos, menu =>
                {
                    menu.AddItem("None", () => OnConditionEntryScriptChanged(conn, capturedIndex, null));
                    menu.AddSeparator();
                    foreach (var script in filtered)
                    {
                        var captured = script;
                        menu.AddItem(GetConditionDisplayName(script), () => OnConditionEntryScriptChanged(conn, capturedIndex, captured));
                    }
                    if (filtered.Count == 0)
                        menu.AddDisabledItem("No matching scripts found");
                });
            };
            scriptRow.Add(pickerBtn);

            if (entry.Script != null)
            {
                var openBtn = new Button(() => AssetDatabase.OpenAsset(entry.Script));
                openBtn.text = "Open";
                openBtn.AddToClassList("script-field-open-button");
                scriptRow.Add(openBtn);
            }

            container.Add(scriptRow);

            if (entry.Script != null)
            {
                string displayName = GetConditionDisplayName(entry.Script);
                var typeLabel = new Label(displayName);
                typeLabel.AddToClassList("script-type-name");
                container.Add(typeLabel);

                if (entry.Instance != null)
                {
                    _currentSO = entry.Instance;
                    var propsContainer = new VisualElement();
                    propsContainer.AddToClassList("condition-properties");
                    var so = new SerializedObject(_currentSO);
                    var prop = so.GetIterator();
                    bool enterChildren = true;
                    while (prop.NextVisible(enterChildren))
                    {
                        enterChildren = false;
                        if (prop.name == "m_Script") continue;

                        var card = new VisualElement();
                        card.AddToClassList("property-card");

                        var label = new Label(prop.displayName);
                        label.AddToClassList("property-card-label");
                        card.Add(label);

                        VisualElement content;
                        if (prop.type == "BlackboardVariableReference")
                        {
                            content = BuildBbVarRefField(so, prop.Copy());
                        }
                        else
                        {
                            var pf = new PropertyField(prop.Copy(), "");
                            content = pf;
                        }
                        content.AddToClassList("property-card-content");
                        card.Add(content);

                        propsContainer.Add(card);
                    }
                    if (propsContainer.childCount > 0)
                    {
                        propsContainer.Bind(so);
                        container.Add(propsContainer);
                    }
                    _currentSO = null;
                }
            }
            else
            {
                var hint = new Label("Assign a script to define behaviour");
                hint.AddToClassList("script-type-name");
                container.Add(hint);
            }

            _scrollView.Add(container);
        }

        private void OnConditionEntryScriptChanged(ConnectionView conn, int index, MonoScript next)
        {
            var entry = conn.ConditionEntries[index];
            if (next == entry.Script) return;
            if (entry.Instance != null)
            {
                Object.DestroyImmediate(entry.Instance, true);
                entry.Instance = null;
            }
            entry.Script = next;
            if (next != null)
            {
                var type = next.GetClass();
                if (type != null)
                {
                    entry.Instance = (ConditionScript)ScriptableObject.CreateInstance(type);
                    string fromName = conn.From?.Name ?? "?";
                    string toName = conn.To?.Name ?? "?";
                    entry.Instance.name = $"{fromName}->{toName}_Condition_{index}";
                    entry.Instance.hideFlags = HideFlags.HideInHierarchy;
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
            AddSectionTitle("Color");

            var colorRow = new VisualElement();
            colorRow.AddToClassList("info-row");

            var colorLabel = new Label("Color");
            colorLabel.AddToClassList("info-row-label");
            colorRow.Add(colorLabel);

            var colorField = new ColorField();
            colorField.value = group.GroupColor;
            colorField.showAlpha = true;
            colorField.AddToClassList("info-row-value");
            colorField.RegisterValueChangedCallback(evt =>
            {
                var cmd = new ModifyGroupColorCommand(group, evt.newValue);
                _window.UndoRedoSystem.Execute(cmd);
                _window.NotifySidePanelChanged();
            });
            colorRow.Add(colorField);
            _scrollView.Add(colorRow);

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

            var pickerBtn = new Button();
            pickerBtn.AddToClassList("script-picker-button");
            pickerBtn.text = currentScript != null ? currentScript.name : "None (Select...)";
            pickerBtn.clicked += () =>
            {
                var filtered = FindFilteredScripts(isValid);
                var pos = _window.rootVisualElement.WorldToLocal(
                    new Vector2(pickerBtn.worldBound.x, pickerBtn.worldBound.y + pickerBtn.worldBound.height));
                MenuDropdown.Show(_window.rootVisualElement, pos, menu =>
                {
                    menu.AddItem("None", () => onAssign(currentScript, null));
                    menu.AddSeparator();
                    foreach (var script in filtered)
                    {
                        var captured = script;
                        menu.AddItem(script.name, () => onAssign(currentScript, captured));
                    }
                    if (filtered.Count == 0)
                        menu.AddDisabledItem("No matching scripts found");
                });
            };
            contentRow.Add(pickerBtn);

            row.Add(contentRow);

            // Open button row (below the picker)
            if (currentScript != null)
            {
                var openRow = new VisualElement();
                openRow.AddToClassList("script-open-row");
                var openBtn = new Button(() => AssetDatabase.OpenAsset(currentScript));
                openBtn.text = "Open Script";
                openBtn.AddToClassList("script-field-open-button");
                openRow.Add(openBtn);
                row.Add(openRow);
            }

            // Type name / hint
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

        private static List<MonoScript> FindFilteredScripts(Func<MonoScript, bool> isValid)
        {
            var results = new List<MonoScript>();
            var guids = AssetDatabase.FindAssets("t:MonoScript");
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
                if (script != null && isValid(script))
                    results.Add(script);
            }
            results.Sort((a, b) => a.name.CompareTo(b.name));
            return results;
        }

        private void AddSOProperties()
        {
            if (_currentSO == null) return;

            var so = new SerializedObject(_currentSO);
            var container = new VisualElement();
            container.AddToClassList("so-properties-container");

            var prop = so.GetIterator();
            bool enterChildren = true;
            while (prop.NextVisible(enterChildren))
            {
                enterChildren = false;
                if (prop.name == "m_Script") continue;

                var card = new VisualElement();
                card.AddToClassList("property-card");

                var label = new Label(prop.displayName);
                label.AddToClassList("property-card-label");
                card.Add(label);

                VisualElement content;
                if (prop.type == "BlackboardVariableReference")
                {
                    content = BuildBbVarRefField(so, prop.Copy());
                }
                else
                {
                    var pf = new PropertyField(prop.Copy(), "");
                    content = pf;
                }
                content.AddToClassList("property-card-content");
                card.Add(content);

                container.Add(card);
            }

            if (container.childCount > 0)
            {
                container.Bind(so);
                _scrollView.Add(container);
            }
        }

        private VisualElement BuildBbVarRefField(SerializedObject so, SerializedProperty prop)
        {
            var row = new VisualElement();
            row.AddToClassList("bb-varref-row");

            var useBbProp = prop.FindPropertyRelative("UseBlackboard");
            var varNameProp = prop.FindPropertyRelative("BlackboardVariableName");
            var valueTypeProp = prop.FindPropertyRelative("ValueType");
            var defaultValueProp = prop.FindPropertyRelative("DefaultValue");

            var modeBtn = new Button();
            modeBtn.AddToClassList("bb-mode-button");
            row.Add(modeBtn);

            var valueArea = new VisualElement();
            valueArea.AddToClassList("bb-value-area");
            row.Add(valueArea);

            Action rebuild = null;
            rebuild = () =>
            {
                bool useBb = useBbProp.boolValue;
                var bbType = (BlackboardVariableType)valueTypeProp.enumValueIndex;

                modeBtn.text = useBb ? "Blackboard" : "Direct";
                valueArea.Clear();

                if (useBb)
                {
                    string currentVarName = varNameProp.stringValue;
                    var dropdownBtn = new Button();
                    dropdownBtn.AddToClassList("bb-dropdown-button");
                    dropdownBtn.text = string.IsNullOrEmpty(currentVarName)
                        ? "Select variable..." : currentVarName;

                    dropdownBtn.clicked += () =>
                    {
                        var pos = _window.rootVisualElement.WorldToLocal(
                            new Vector2(dropdownBtn.worldBound.x, dropdownBtn.worldBound.y + dropdownBtn.worldBound.height));
                        MenuDropdown.Show(_window.rootVisualElement, pos, menu =>
                        {
                            menu.AddItem("None (direct)", () =>
                            {
                                varNameProp.stringValue = "";
                                useBbProp.boolValue = false;
                                so.ApplyModifiedProperties();
                                EditorUtility.SetDirty(so.targetObject);
                                rebuild();
                            });
                            menu.AddSeparator();
                            bool hasMatch = false;
                            for (int i = 0; i < _blackboardVariables.Count; i++)
                            {
                                var bv = _blackboardVariables[i];
                                if (bv.Type == bbType)
                                {
                                    hasMatch = true;
                                    string varName = bv.Name;
                                    string captured = varName;
                                    menu.AddItem(varName, () =>
                                    {
                                        varNameProp.stringValue = captured;
                                        so.ApplyModifiedProperties();
                                        EditorUtility.SetDirty(so.targetObject);
                                        rebuild();
                                    });
                                }
                            }
                            if (!hasMatch)
                                menu.AddDisabledItem("No matching variables");
                        });
                    };

                    valueArea.Add(dropdownBtn);
                }
                else
                {
                    switch (bbType)
                    {
                        case BlackboardVariableType.Bool:
                        {
                            var toggle = new Toggle();
                            toggle.value = bool.TryParse(defaultValueProp.stringValue, out var v) && v;
                            toggle.RegisterValueChangedCallback(e =>
                            {
                                defaultValueProp.stringValue = e.newValue.ToString();
                                so.ApplyModifiedProperties();
                                EditorUtility.SetDirty(so.targetObject);
                            });
                            valueArea.Add(toggle);
                            break;
                        }
                        case BlackboardVariableType.Int:
                        {
                            var field = new IntegerField();
                            field.value = int.TryParse(defaultValueProp.stringValue, out var v) ? v : 0;
                            field.RegisterValueChangedCallback(e =>
                            {
                                defaultValueProp.stringValue = e.newValue.ToString();
                                so.ApplyModifiedProperties();
                                EditorUtility.SetDirty(so.targetObject);
                            });
                            valueArea.Add(field);
                            break;
                        }
                        case BlackboardVariableType.Float:
                        {
                            var field = new FloatField();
                            field.value = float.TryParse(defaultValueProp.stringValue, out var v) ? v : 0f;
                            field.RegisterValueChangedCallback(e =>
                            {
                                defaultValueProp.stringValue = e.newValue.ToString("G");
                                so.ApplyModifiedProperties();
                                EditorUtility.SetDirty(so.targetObject);
                            });
                            valueArea.Add(field);
                            break;
                        }
                        case BlackboardVariableType.String:
                        {
                            var field = new TextField();
                            field.value = defaultValueProp.stringValue;
                            field.RegisterValueChangedCallback(e =>
                            {
                                defaultValueProp.stringValue = e.newValue;
                                so.ApplyModifiedProperties();
                                EditorUtility.SetDirty(so.targetObject);
                            });
                            valueArea.Add(field);
                            break;
                        }
                        case BlackboardVariableType.Vector2:
                        case BlackboardVariableType.Vector3:
                        {
                            var field = new TextField();
                            field.value = defaultValueProp.stringValue;
                            field.RegisterValueChangedCallback(e =>
                            {
                                defaultValueProp.stringValue = e.newValue;
                                so.ApplyModifiedProperties();
                                EditorUtility.SetDirty(so.targetObject);
                            });
                            valueArea.Add(field);
                            break;
                        }
                    }
                }
            };

            modeBtn.clicked += () =>
            {
                var pos = _window.rootVisualElement.WorldToLocal(
                    new Vector2(modeBtn.worldBound.x, modeBtn.worldBound.y + modeBtn.worldBound.height));
                MenuDropdown.Show(_window.rootVisualElement, pos, menu =>
                {
                    menu.AddItem("Direct", () =>
                    {
                        useBbProp.boolValue = false;
                        so.ApplyModifiedProperties();
                        EditorUtility.SetDirty(so.targetObject);
                        rebuild();
                    });
                    menu.AddItem("Blackboard", () =>
                    {
                        useBbProp.boolValue = true;
                        so.ApplyModifiedProperties();
                        EditorUtility.SetDirty(so.targetObject);
                        rebuild();
                    });
                });
            };

            rebuild();
            return row;
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

        private static string GetConditionDisplayName(MonoScript script)
        {
            var type = script.GetClass();
            if (type == null) return script.name;
            if (type.IsSubclassOf(typeof(ConditionScript)))
            {
                var instance = (ConditionScript)ScriptableObject.CreateInstance(type);
                string name = instance.DisplayName;
                Object.DestroyImmediate(instance);
                return name;
            }
            return type.Name;
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
    }
}

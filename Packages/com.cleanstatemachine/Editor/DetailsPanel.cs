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

        private static ConditionEntry _conditionClipboard;

        private int _hoveredConditionIndex = -1;
        private bool _hoveringAddButton;
        private ConnectionView _activeConnection;

        // Reorderable list state
        private VisualElement _conditionListContainer;
        private VisualElement _behaviourListContainer;
        private readonly List<VisualElement> _conditionEntryElements = new();
        private readonly List<VisualElement> _behaviourEntryElements = new();
        private StateView _activeBehaviourState;

        private enum ReorderTarget { None, Condition, Behaviour }
        private ReorderTarget _reorderTarget;
        private int _reorderDragStartIndex;
        private int _reorderDragIndex;
        private bool _isReordering;
        private Vector2 _reorderDragStartPos;
        private bool _reorderPastThreshold;
        private const float ReorderThreshold = 5f;
        private const float ReorderAutoScrollEdgeThreshold = 20f;
        private const float ReorderAutoScrollSpeed = 30f;

        public DetailsPanel(CleanStateMachineWindow window)
        {
            _window = window;
            AddToClassList("details-panel");

            var header = new VisualElement();
            header.AddToClassList("panel-header");

            var title = new Label("Inspector");
            title.AddToClassList("panel-title");
            header.Add(title);

            var helpBtn = new Button(() => _window.ShortcutGuide.Show());
            helpBtn.text = "?";
            helpBtn.AddToClassList("details-help-button");
            header.Add(helpBtn);

            Add(header);

            _scrollView = new ScrollView();
            _scrollView.AddToClassList("details-scroll");
            _scrollView.focusable = true;
            _scrollView.RegisterCallback<KeyDownEvent>(OnConditionKeyDown);
            _scrollView.RegisterCallback<MouseEnterEvent>(_ => _scrollView.Focus());
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
            _hoveredConditionIndex = -1;
            _hoveringAddButton = false;
            _activeConnection = null;

            _conditionEntryElements.Clear();
            _behaviourEntryElements.Clear();
            _conditionListContainer = null;
            _behaviourListContainer = null;
            _activeBehaviourState = null;
            _reorderTarget = ReorderTarget.None;
            _isReordering = false;
            _reorderPastThreshold = false;
            _reorderDragStartIndex = -1;
            _reorderDragIndex = -1;
            this.UnregisterCallback<MouseMoveEvent>(OnReorderDragMove);
            this.UnregisterCallback<MouseUpEvent>(OnReorderDragUp);

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
            BuildValidationMessages(state);

            AddSectionTitle("State Information");

            var nameRow = new VisualElement();
            nameRow.AddToClassList("info-row");

            var nameLabelEl = new Label("Name");
            nameLabelEl.AddToClassList("info-row-label");
            nameRow.Add(nameLabelEl);

            var nameField = new TextField();
            nameField.value = state.Name;
            nameField.AddToClassList("info-row-value");
            nameField.AddToClassList("details-name-field");

            string oldName = state.Name;
            nameField.RegisterValueChangedCallback(evt =>
            {
                if (!string.IsNullOrEmpty(evt.newValue))
                    state.Name = evt.newValue;
                else
                    nameField.SetValueWithoutNotify(state.Name);
            });

            nameField.RegisterCallback<FocusOutEvent>(evt =>
            {
                string newName = nameField.value;
                if (newName != oldName && !string.IsNullOrEmpty(newName))
                {
                    var cmd = new RenameStateCommand(state, oldName, newName);
                    _window.UndoRedoSystem.Execute(cmd);
                    _window.NotifySidePanelChanged();
                    oldName = newName;
                }
            });

            nameField.RegisterCallback<KeyDownEvent>(e =>
            {
                if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)
                {
                    nameField.Blur();
                    e.StopPropagation();
                }
                else if (e.keyCode == KeyCode.Escape)
                {
                    nameField.value = oldName;
                    nameField.Blur();
                    e.StopPropagation();
                }
            });

            nameRow.Add(nameField);
            _scrollView.Add(nameRow);

            BuildStateConnectionsList(state);

            if (state.IsEntry)
            {
                AddDivider();
                AddSectionTitle("Entry Settings");

                var autoRunRow = new VisualElement();
                autoRunRow.AddToClassList("info-row");

                var autoRunLabel = new Label("Auto Run State Machine");
                autoRunLabel.AddToClassList("info-row-label");
                autoRunRow.Add(autoRunLabel);

                var autoRunToggle = new Toggle();
                autoRunToggle.value = state.AutoRun;
                autoRunToggle.AddToClassList("info-row-value");
                autoRunToggle.RegisterValueChangedCallback(evt =>
                {
                    state.AutoRun = evt.newValue;
                    _window.NotifySidePanelChanged();
                });
                autoRunRow.Add(autoRunToggle);
                _scrollView.Add(autoRunRow);

                return;
            }

            if (state.IsAnyState)
            {
                AddDivider();
                AddSectionTitle("Any State");

                var infoRow = new VisualElement();
                infoRow.AddToClassList("info-row");
                var infoLabel = new Label("Transitions from this node are\nevaluated from any active state.");
                infoLabel.AddToClassList("info-row-value");
                infoLabel.style.whiteSpace = WhiteSpace.Normal;
                infoRow.Add(infoLabel);
                _scrollView.Add(infoRow);

                return;
            }

            if (state.IsSubStateMachine)
            {
                AddDivider();
                AddSectionTitle("Sub State Machine");
                AddInfoRow("Child States", state.ChildIndices.Count.ToString());

                if (_window.IsCurrentExpandedSubState(state))
                {
                    var closeBtn = new Button(() =>
                    {
                        _window.ExitExpandedSubState();
                    });
                    closeBtn.text = "Close Sub State";
                    closeBtn.AddToClassList("enter-sub-button");
                    closeBtn.style.backgroundColor = new StyleColor(new Color(0.5f, 0.15f, 0.15f));
                    _scrollView.Add(closeBtn);
                }
                else
                {
                    var openBtn = new Button(() =>
                    {
                        _window.EnterExpandSubState(state);
                    });
                    openBtn.text = "Open Sub State";
                    openBtn.AddToClassList("enter-sub-button");
                    openBtn.style.backgroundColor = new StyleColor(new Color(0.2f, 0.3f, 0.6f));
                    _scrollView.Add(openBtn);
                }
            }
            else if (state.IsExternalReference)
            {
                BuildExternalReferenceContent(state);
            }
            else
            {
                AddDivider();
                AddSectionTitle("State Behaviours");

                var behaviourList = new VisualElement();
                behaviourList.AddToClassList("reorderable-list");
                _behaviourListContainer = behaviourList;
                _behaviourEntryElements.Clear();
                _scrollView.Add(behaviourList);
                _activeBehaviourState = state;

                for (int i = 0; i < state.BehaviourEntries.Count; i++)
                {
                    BuildBehaviourEntry(state, i);
                }

                    var addBtn = new Button(() =>
                    {
                        state.BehaviourEntries.Add(new BehaviourEntry());
                        _window.NotifySidePanelChanged();
                        UpdateSelection(_selected, _states, _connections, _blackboardVariables);
                    });
                addBtn.text = "+ Add Behaviour";
                addBtn.AddToClassList("add-behaviour-button");
                _scrollView.Add(addBtn);
            }
        }

        private void BuildBehaviourEntry(StateView state, int index)
        {
            var entry = state.BehaviourEntries[index];

            var container = new VisualElement();
            container.AddToClassList("behaviour-entry");
            container.userData = index;

            var header = new VisualElement();
            header.AddToClassList("behaviour-entry-header");

            var dragHandle = new Label("\u2807");
            dragHandle.AddToClassList("reorder-handle");
            dragHandle.RegisterCallback<MouseDownEvent>(OnBehaviourReorderHandleDown);
            header.Add(dragHandle);

            var headerLabel = new Label(entry.GetScript() != null ? GetBehaviourDisplayName(entry.GetScript()) : $"Behaviour {index + 1}");
            headerLabel.AddToClassList("behaviour-entry-label");
            header.Add(headerLabel);

            var removeBtn = new Button(() =>
            {
                if (entry.Instance != null)
                {
                    Object.DestroyImmediate(entry.Instance, true);
                    entry.Instance = null;
                }
                state.BehaviourEntries.RemoveAt((int)container.userData);
                _window.NotifySidePanelChanged();
                UpdateSelection(_selected, _states, _connections, _blackboardVariables);
            });
            removeBtn.text = "X";
            removeBtn.AddToClassList("behaviour-remove-button");
            header.Add(removeBtn);

            container.Add(header);

            var scriptRow = new VisualElement();
            scriptRow.AddToClassList("behaviour-script-row");

            var pickerBtn = new Button();
            pickerBtn.AddToClassList("script-picker-button");
            pickerBtn.text = entry.GetScript() != null ? GetBehaviourDisplayName(entry.GetScript()) : "None (Select...)";
            pickerBtn.clicked += () =>
            {
                var filtered = MonoScriptCache.GetScriptsByBaseType<StateBehaviour>();
                var pos = _window.rootVisualElement.WorldToLocal(
                    new Vector2(pickerBtn.worldBound.x, pickerBtn.worldBound.y + pickerBtn.worldBound.height));
                MenuDropdown.Show(_window.rootVisualElement, pos, menu =>
                {
                    menu.AddItem("None", () => OnBehaviourEntryScriptChanged(state, (int)container.userData, null));
                    menu.AddSeparator();
                    foreach (var script in filtered)
                    {
                        var captured = script;
                        menu.AddItem(GetBehaviourDisplayName(script), () => OnBehaviourEntryScriptChanged(state, (int)container.userData, captured));
                    }
                    if (filtered.Count == 0)
                        menu.AddDisabledItem("No matching scripts found");
                });
            };
            scriptRow.Add(pickerBtn);

            if (entry.GetScript() != null)
            {
                var openBtn = new Button(() => AssetDatabase.OpenAsset(entry.GetScript()));
                openBtn.text = "Open";
                openBtn.AddToClassList("script-field-open-button");
                scriptRow.Add(openBtn);
            }

            container.Add(scriptRow);

            if (entry.GetScript() != null)
            {
                var typeLabel = new Label(GetBehaviourDisplayName(entry.GetScript()));
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
                        else if (prop.type == "BlackboardVariableSelector")
                        {
                            content = BuildBbVarSelectorField(so, prop.Copy());
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

            _behaviourEntryElements.Add(container);
            _behaviourListContainer.Add(container);
        }

        private void OnBehaviourEntryScriptChanged(StateView state, int index, MonoScript next)
        {
            var entry = state.BehaviourEntries[index];
            if (next == entry.GetScript()) return;
            if (entry.Instance != null)
            {
                Object.DestroyImmediate(entry.Instance, true);
                entry.Instance = null;
            }
            entry.SetScript(next);
            if (next != null)
            {
                var type = next.GetClass();
                if (type != null)
                {
                    entry.Instance = (StateBehaviour)ScriptableObject.CreateInstance(type);
                    entry.Instance.name = $"{state.Name}_Behaviour_{index}";
                    entry.Instance.hideFlags = HideFlags.HideInHierarchy;
                }
            }
            _window.NotifySidePanelChanged();
            UpdateSelection(_selected, _states, _connections, _blackboardVariables);
        }

        // ─── CONNECTION CONTENT ─────────────────────────────────────────

        private void BuildConnectionContent(ConnectionView conn)
        {
            _activeConnection = conn;
            BuildConnectionValidationMessages(conn);
            AddSectionTitle("Connection");

            var statesRow = new VisualElement();
            statesRow.AddToClassList("conn-states-row");

            if (conn.From != null)
            {
                var fromBtn = new Button();
                fromBtn.AddToClassList("conn-state-button");
                fromBtn.text = conn.From.Name;
                var fromState = conn.From;
                fromBtn.clicked += () =>
                {
                    _window.SelectionController.Clear();
                    _window.SelectionController.Select(fromState);
                };
                statesRow.Add(fromBtn);
            }

            var arrowLabel = new Label("\u2192");
            arrowLabel.AddToClassList("conn-states-arrow");
            statesRow.Add(arrowLabel);

            if (conn.To != null)
            {
                var toBtn = new Button();
                toBtn.AddToClassList("conn-state-button");
                toBtn.text = conn.To.Name;
                var toState = conn.To;
                toBtn.clicked += () =>
                {
                    _window.SelectionController.Clear();
                    _window.SelectionController.Select(toState);
                };
                statesRow.Add(toBtn);
            }

            _scrollView.Add(statesRow);

            var minTimeRow = new VisualElement();
            minTimeRow.AddToClassList("info-row");

            var minTimeLabel = new Label("Min State Time");
            minTimeLabel.AddToClassList("info-row-label");
            minTimeRow.Add(minTimeLabel);

            var minTimeField = new FloatField();
            minTimeField.value = conn.MinStateTime;
            minTimeField.AddToClassList("info-row-value");
            minTimeField.RegisterValueChangedCallback(evt =>
            {
                conn.MinStateTime = evt.newValue >= 0f ? evt.newValue : 0f;
                _window.NotifySidePanelChanged();
            });
            minTimeRow.Add(minTimeField);
            _scrollView.Add(minTimeRow);

            AddDivider();
            AddSectionTitle("Transition Conditions");

            var conditionList = new VisualElement();
            conditionList.AddToClassList("reorderable-list");
            _conditionListContainer = conditionList;
            _conditionEntryElements.Clear();
            _scrollView.Add(conditionList);

            for (int i = 0; i < conn.ConditionEntries.Count; i++)
            {
                BuildConditionEntry(conn, i);
            }

            var addBtn = new Button(() =>
            {
                conn.ConditionEntries.Add(new ConditionEntry());
                _window.NotifySidePanelChanged();
                UpdateSelection(_selected, _states, _connections, _blackboardVariables);
            });
            addBtn.text = "+ Add Condition";
            addBtn.AddToClassList("add-condition-button");
            addBtn.focusable = true;

            addBtn.RegisterCallback<MouseEnterEvent>(_ =>
            {
                _hoveringAddButton = true;
                _hoveredConditionIndex = -1;
            });
            addBtn.RegisterCallback<MouseLeaveEvent>(_ => _hoveringAddButton = false);

            addBtn.RegisterCallback<ContextClickEvent>(evt =>
            {
                if (_conditionClipboard?.GetScript() == null || _conditionClipboard.Instance == null) return;
                var pos = _window.rootVisualElement.WorldToLocal(
                    new Vector2(evt.mousePosition.x, evt.mousePosition.y));
                MenuDropdown.Show(_window.rootVisualElement, pos, menu =>
                {
                    menu.AddItem("Paste Condition", () => AppendConditionFromClipboard(conn));
                });
            });

            addBtn.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.ctrlKey && evt.keyCode == KeyCode.V && _conditionClipboard?.GetScript() != null)
                {
                    AppendConditionFromClipboard(conn);
                    evt.StopPropagation();
                }
            });

            _scrollView.Add(addBtn);
        }

        private void BuildConditionEntry(ConnectionView conn, int index)
        {
            var entry = conn.ConditionEntries[index];

            var container = new VisualElement();
            container.AddToClassList("condition-entry");

            var header = new VisualElement();
            header.AddToClassList("condition-entry-header");
            container.userData = index;

            var dragHandle = new Label("\u2807");
            dragHandle.AddToClassList("reorder-handle");
            dragHandle.RegisterCallback<MouseDownEvent>(OnConditionReorderHandleDown);
            header.Add(dragHandle);

            string condName = entry.GetScript() != null ? GetConditionDisplayName(entry.GetScript()) : $"Condition {index + 1}";
            var headerLabel = new Label(condName);
            headerLabel.AddToClassList("condition-entry-label");
            header.Add(headerLabel);

            var removeBtn = new Button(() =>
            {
                if (entry.Instance != null)
                {
                    Object.DestroyImmediate(entry.Instance, true);
                    entry.Instance = null;
                }
                conn.ConditionEntries.RemoveAt((int)container.userData);
                _window.NotifySidePanelChanged();
                UpdateSelection(_selected, _states, _connections, _blackboardVariables);
            });
            removeBtn.text = "X";
            removeBtn.AddToClassList("condition-remove-button");
            header.Add(removeBtn);

            container.RegisterCallback<MouseEnterEvent>(_ =>
            {
                _hoveredConditionIndex = (int)container.userData;
                _hoveringAddButton = false;
            });
            container.RegisterCallback<MouseLeaveEvent>(_ =>
            {
                if (_hoveredConditionIndex == (int)container.userData)
                    _hoveredConditionIndex = -1;
            });

            container.RegisterCallback<ContextClickEvent>(evt =>
            {
                var pos = _window.rootVisualElement.WorldToLocal(
                    new Vector2(evt.mousePosition.x, evt.mousePosition.y));
                MenuDropdown.Show(_window.rootVisualElement, pos, menu =>
                {
                    if (entry.GetScript() != null)
                        menu.AddItem("Copy Condition", () => CopyCondition(entry));
                    else
                        menu.AddDisabledItem("Copy Condition");

                    if (_conditionClipboard != null && _conditionClipboard.GetScript() != null)
                        menu.AddItem("Paste Condition", () => PasteCondition(conn, (int)container.userData));
                    else
                        menu.AddDisabledItem("Paste Condition");
                });
            });

            container.Add(header);

            var scriptRow = new VisualElement();
            scriptRow.AddToClassList("condition-script-row");

            var pickerBtn = new Button();
            pickerBtn.AddToClassList("script-picker-button");
            pickerBtn.text = entry.GetScript() != null ? GetConditionDisplayName(entry.GetScript()) : "None (Select...)";
            pickerBtn.clicked += () =>
            {
                var filtered = MonoScriptCache.GetScriptsByBaseType<ConditionScript>();
                var pos = _window.rootVisualElement.WorldToLocal(
                    new Vector2(pickerBtn.worldBound.x, pickerBtn.worldBound.y + pickerBtn.worldBound.height));
                MenuDropdown.Show(_window.rootVisualElement, pos, menu =>
                {
                    menu.AddItem("None", () => OnConditionEntryScriptChanged(conn, (int)container.userData, null));
                    menu.AddSeparator();
                    foreach (var script in filtered)
                    {
                        var captured = script;
                        menu.AddItem(GetConditionDisplayName(script), () => OnConditionEntryScriptChanged(conn, (int)container.userData, captured));
                    }
                    if (filtered.Count == 0)
                        menu.AddDisabledItem("No matching scripts found");
                });
            };
            scriptRow.Add(pickerBtn);

            if (entry.GetScript() != null)
            {
                var openBtn = new Button(() => AssetDatabase.OpenAsset(entry.GetScript()));
                openBtn.text = "Open";
                openBtn.AddToClassList("script-field-open-button");
                scriptRow.Add(openBtn);
            }

            container.Add(scriptRow);

            if (entry.GetScript() != null)
            {
                string displayName = GetConditionDisplayName(entry.GetScript());
                var typeLabel = new Label(displayName);
                typeLabel.AddToClassList("script-type-name");
                container.Add(typeLabel);

                if (entry.Instance != null)
                {
                    _currentSO = entry.Instance;
                    var propsContainer = new VisualElement();
                    propsContainer.AddToClassList("condition-properties");
                    var so = new SerializedObject(_currentSO);

                    Action rebuildConditionProperties = null;
                    rebuildConditionProperties = () =>
                    {
                        propsContainer.Clear();
                        so.Update();
                        var prop = so.GetIterator();
                        bool enterChildren = true;
                        while (prop.NextVisible(enterChildren))
                        {
                            enterChildren = false;
                            if (prop.name == "m_Script") continue;
                            if (so.targetObject is ConditionScript condition && !condition.ShouldShowProperty(prop.name)) continue;

                            var card = new VisualElement();
                            card.AddToClassList("property-card");

                            var label = new Label(prop.displayName);
                            label.AddToClassList("property-card-label");
                            card.Add(label);

                            VisualElement content;
                            var propCopy = prop.Copy();
                            if (prop.type == "BlackboardVariableReference")
                            {
                                content = BuildBbVarRefField(so, propCopy);
                            }
                            else if (prop.propertyType == SerializedPropertyType.Enum)
                            {
                                content = BuildEnumField(so, propCopy, prop.name == "variableType"
                                    ? () => propsContainer.schedule.Execute(() => rebuildConditionProperties()).StartingIn(0)
                                    : null);
                            }
                            else if (prop.propertyType == SerializedPropertyType.Boolean)
                            {
                                content = BuildBoolField(so, propCopy);
                            }
                            else
                            {
                                content = new PropertyField(propCopy, "");
                            }
                            content.AddToClassList("property-card-content");
                            card.Add(content);

                            propsContainer.Add(card);
                        }
                        if (propsContainer.childCount > 0)
                            propsContainer.Bind(so);
                    };

                    rebuildConditionProperties();

                    if (propsContainer.childCount > 0)
                        container.Add(propsContainer);
                    _currentSO = null;
                }
            }
            else
            {
                var hint = new Label("Assign a script to define behaviour");
                hint.AddToClassList("script-type-name");
                container.Add(hint);
            }

            _conditionEntryElements.Add(container);
            _conditionListContainer.Add(container);
        }

        private void CopyCondition(ConditionEntry entry)
        {
            var script = entry.GetScript();
            if (script == null || entry.Instance == null) return;

            var type = script.GetClass();
            if (type == null) return;

            _conditionClipboard = new ConditionEntry
            {
                TypeName = entry.TypeName,
                Instance = null
            };

            var clone = (ConditionScript)ScriptableObject.CreateInstance(type);
            EditorUtility.CopySerialized(entry.Instance, clone);
            clone.name = $"{type.Name}_Clipboard";
            clone.hideFlags = HideFlags.HideInHierarchy;
            _conditionClipboard.Instance = clone;
        }

        private void PasteCondition(ConnectionView conn, int afterIndex)
        {
            if (_conditionClipboard?.GetScript() == null || _conditionClipboard.Instance == null) return;

            var type = _conditionClipboard.GetScript().GetClass();
            if (type == null) return;

            var instance = (ConditionScript)ScriptableObject.CreateInstance(type);
            EditorUtility.CopySerialized(_conditionClipboard.Instance, instance);
            instance.hideFlags = HideFlags.HideInHierarchy;

            conn.ConditionEntries.Insert(afterIndex + 1, new ConditionEntry
            {
                TypeName = _conditionClipboard.TypeName,
                Instance = instance
            });

            _window.NotifySidePanelChanged();
            UpdateSelection(_selected, _states, _connections, _blackboardVariables);
        }

        private void AppendConditionFromClipboard(ConnectionView conn)
        {
            if (_conditionClipboard?.GetScript() == null || _conditionClipboard.Instance == null) return;
            var type = _conditionClipboard.GetScript().GetClass();
            if (type == null) return;
            var instance = (ConditionScript)ScriptableObject.CreateInstance(type);
            EditorUtility.CopySerialized(_conditionClipboard.Instance, instance);
            instance.hideFlags = HideFlags.HideInHierarchy;
            conn.ConditionEntries.Add(new ConditionEntry
            {
                TypeName = _conditionClipboard.TypeName,
                Instance = instance
            });
            _window.NotifySidePanelChanged();
            UpdateSelection(_selected, _states, _connections, _blackboardVariables);
        }

        private void OnConditionKeyDown(KeyDownEvent evt)
        {
            if (!evt.ctrlKey) return;

            if (evt.keyCode == KeyCode.C && _hoveredConditionIndex >= 0 && _activeConnection != null)
            {
                var entries = _activeConnection.ConditionEntries;
                if (_hoveredConditionIndex < entries.Count)
                {
                    var entry = entries[_hoveredConditionIndex];
                    if (entry.GetScript() != null && entry.Instance != null)
                        CopyCondition(entry);
                    evt.StopPropagation();
                }
                return;
            }

            if (evt.keyCode == KeyCode.V && _conditionClipboard?.GetScript() != null)
            {
                if (_hoveredConditionIndex >= 0 && _activeConnection != null)
                {
                    PasteCondition(_activeConnection, _hoveredConditionIndex);
                    evt.StopPropagation();
                }
                else if (_hoveringAddButton && _activeConnection != null)
                {
                    AppendConditionFromClipboard(_activeConnection);
                    evt.StopPropagation();
                }
            }
        }

        // ─── REORDER DRAG HANDLERS ────────────────────────────────────

        private void OnConditionReorderHandleDown(MouseDownEvent evt)
        {
            if (_conditionEntryElements.Count <= 1) return;

            var handle = evt.currentTarget as VisualElement;
            var container = handle.parent.parent;
            int index = (int)container.userData;

            _reorderTarget = ReorderTarget.Condition;
            _reorderDragStartIndex = index;
            _reorderDragIndex = index;
            _isReordering = true;
            _reorderPastThreshold = false;
            _reorderDragStartPos = evt.mousePosition;
            container.AddToClassList("reorder-entry-drag");

            this.RegisterCallback<MouseMoveEvent>(OnReorderDragMove);
            this.RegisterCallback<MouseUpEvent>(OnReorderDragUp);
            evt.StopPropagation();
        }

        private void OnBehaviourReorderHandleDown(MouseDownEvent evt)
        {
            if (_behaviourEntryElements.Count <= 1) return;

            var handle = evt.currentTarget as VisualElement;
            var container = handle.parent.parent;
            int index = (int)container.userData;

            _reorderTarget = ReorderTarget.Behaviour;
            _reorderDragStartIndex = index;
            _reorderDragIndex = index;
            _isReordering = true;
            _reorderPastThreshold = false;
            _reorderDragStartPos = evt.mousePosition;
            container.AddToClassList("reorder-entry-drag");

            this.RegisterCallback<MouseMoveEvent>(OnReorderDragMove);
            this.RegisterCallback<MouseUpEvent>(OnReorderDragUp);
            evt.StopPropagation();
        }

        private void OnReorderDragMove(MouseMoveEvent evt)
        {
            if (!_isReordering || _reorderTarget == ReorderTarget.None) return;

            if (!_reorderPastThreshold)
            {
                if (Vector2.Distance(evt.mousePosition, _reorderDragStartPos) < ReorderThreshold)
                    return;
                _reorderPastThreshold = true;
            }

            var entries = _reorderTarget == ReorderTarget.Condition ? _conditionEntryElements : _behaviourEntryElements;
            var listContainer = _reorderTarget == ReorderTarget.Condition ? _conditionListContainer : _behaviourListContainer;

            if (entries == null || entries.Count <= 1) return;
            if (_reorderDragIndex < 0 || _reorderDragIndex >= entries.Count) return;

            // Auto-scroll when near edges of the scroll view
            Vector2 scrollViewLocal = _scrollView.WorldToLocal(evt.mousePosition);
            float viewHeight = _scrollView.resolvedStyle.height;
            if (scrollViewLocal.y < ReorderAutoScrollEdgeThreshold)
                _scrollView.scrollOffset = new Vector2(0, Mathf.Max(0, _scrollView.scrollOffset.y - ReorderAutoScrollSpeed));
            else if (scrollViewLocal.y > viewHeight - ReorderAutoScrollEdgeThreshold)
                _scrollView.scrollOffset = new Vector2(0, _scrollView.scrollOffset.y + ReorderAutoScrollSpeed);

            int targetIndex = DetermineReorderTargetIndex(entries, evt.mousePosition);

            if (targetIndex == _reorderDragIndex) return;

            var element = entries[_reorderDragIndex];
            listContainer.Remove(element);
            listContainer.Insert(targetIndex, element);

            entries.RemoveAt(_reorderDragIndex);
            entries.Insert(targetIndex, element);

            for (int i = 0; i < entries.Count; i++)
                entries[i].userData = i;

            _reorderDragIndex = targetIndex;
            evt.StopPropagation();
        }

        private void OnReorderDragUp(MouseUpEvent evt)
        {
            _isReordering = false;

            this.UnregisterCallback<MouseMoveEvent>(OnReorderDragMove);
            this.UnregisterCallback<MouseUpEvent>(OnReorderDragUp);

            var entries = _reorderTarget == ReorderTarget.Condition ? _conditionEntryElements : _behaviourEntryElements;

            if (_reorderDragIndex >= 0 && _reorderDragIndex < entries.Count)
                entries[_reorderDragIndex].RemoveFromClassList("reorder-entry-drag");

            if (_reorderDragStartIndex >= 0 && _reorderDragStartIndex != _reorderDragIndex)
            {
                if (_reorderTarget == ReorderTarget.Condition && _activeConnection != null)
                {
                    var entry = _activeConnection.ConditionEntries[_reorderDragStartIndex];
                    _activeConnection.ConditionEntries.RemoveAt(_reorderDragStartIndex);
                    _activeConnection.ConditionEntries.Insert(_reorderDragIndex, entry);
                }
                else if (_reorderTarget == ReorderTarget.Behaviour && _activeBehaviourState != null)
                {
                    var entry = _activeBehaviourState.BehaviourEntries[_reorderDragStartIndex];
                    _activeBehaviourState.BehaviourEntries.RemoveAt(_reorderDragStartIndex);
                    _activeBehaviourState.BehaviourEntries.Insert(_reorderDragIndex, entry);
                }

                for (int i = 0; i < entries.Count; i++)
                    entries[i].userData = i;

                _window.NotifySidePanelChanged();
            }

            _reorderTarget = ReorderTarget.None;
            _reorderDragStartIndex = -1;
            _reorderDragIndex = -1;
            evt.StopPropagation();
        }

        private static int DetermineReorderTargetIndex(List<VisualElement> entries, Vector2 mousePos)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                var bounds = entries[i].worldBound;
                float midY = bounds.y + bounds.height * 0.5f;
                if (mousePos.y < midY)
                    return i;
            }
            return entries.Count - 1;
        }

        // ─── CONDITION ENTRY SCRIPT HANDLING ──────────────────────────

        private void OnConditionEntryScriptChanged(ConnectionView conn, int index, MonoScript next)
        {
            var entry = conn.ConditionEntries[index];
            if (next == entry.GetScript()) return;
            if (entry.Instance != null)
            {
                Object.DestroyImmediate(entry.Instance, true);
                entry.Instance = null;
            }
            entry.SetScript(next);
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

        private void BuildStateConnectionsList(StateView state)
        {
            if (_connections == null) return;

            var stateConnections = new List<ConnectionView>();
            for (int i = 0; i < _connections.Count; i++)
            {
                if (_connections[i].From == state || _connections[i].To == state)
                    stateConnections.Add(_connections[i]);
            }

            if (stateConnections.Count == 0) return;

            AddDivider();
            AddSectionTitle(stateConnections.Count == 1 ? "Connection" : $"Connections ({stateConnections.Count})");

            for (int i = 0; i < stateConnections.Count; i++)
            {
                var conn = stateConnections[i];
                bool isFrom = conn.From == state;

                var connRow = new VisualElement();
                connRow.AddToClassList("state-conn-row");

                bool isSelected = conn.IsSelected;
                if (isSelected)
                    connRow.AddToClassList("state-conn-row-selected");

                connRow.RegisterCallback<MouseDownEvent>(evt =>
                {
                    _window.SelectionController.Clear();
                    _window.SelectionController.Select(conn);
                });

                string dir = isFrom ? "\u2192" : "\u2190";
                string otherName = isFrom ? (conn.To?.Name ?? "?") : (conn.From?.Name ?? "?");

                var connLabel = new Label($"{dir}  {otherName}");
                connLabel.AddToClassList("state-conn-row-label");
                connRow.Add(connLabel);

                if (conn.ConditionEntries.Count > 0)
                {
                    var parts = new List<string>();
                    for (int j = 0; j < conn.ConditionEntries.Count; j++)
                    {
                        var ce = conn.ConditionEntries[j];
                        if (ce.GetScript() != null)
                            parts.Add(GetConditionDisplayName(ce.GetScript()));
                        else
                            parts.Add("?");
                    }
                    var condLabel = new Label(string.Join(", ", parts));
                    condLabel.AddToClassList("state-conn-row-conditions");
                    connRow.Add(condLabel);
                }
                else
                {
                    var noCondLabel = new Label("no conditions");
                    noCondLabel.AddToClassList("state-conn-row-conditions");
                    noCondLabel.AddToClassList("state-conn-row-conditions--empty");
                    connRow.Add(noCondLabel);
                }

                _scrollView.Add(connRow);
            }
        }

        // ─── EXTERNAL REFERENCE CONTENT ────────────────────────────────

        private void BuildExternalReferenceContent(StateView state)
        {
            AddDivider();
            AddSectionTitle("External State Machine");

            var targetRow = new VisualElement();
            targetRow.AddToClassList("info-row");

            var targetLabel = new Label("Target");
            targetLabel.AddToClassList("info-row-label");
            targetRow.Add(targetLabel);

            var objectField = new ObjectField();
            objectField.objectType = typeof(GameObject);
            objectField.value = state.ExternalStateMachine;
            objectField.AddToClassList("info-row-value");
            objectField.RegisterValueChangedCallback(evt =>
            {
                state.ExternalStateMachine = evt.newValue as GameObject;
                _window.NotifySidePanelChanged();
            });
            targetRow.Add(objectField);
            _scrollView.Add(targetRow);

            AddDivider();
            AddSectionTitle("Action");

            var actionRow = new VisualElement();
            actionRow.AddToClassList("info-row");

            var actionLabel = new Label("Action");
            actionLabel.AddToClassList("info-row-label");
            actionRow.Add(actionLabel);

            var currentAction = state.ExternalAction;
            var actionBtn = new Button();
            actionBtn.AddToClassList("script-picker-button");
            actionBtn.text = currentAction switch
            {
                ExternalStateMachineAction.StartStateMachine => "Start State Machine",
                ExternalStateMachineAction.SetStateByName => "Set State By Name",
                ExternalStateMachineAction.SetBlackboardParameter => "Set Blackboard Parameter",
                _ => "Start State Machine"
            };
            actionBtn.clicked += () =>
            {
                var pos = _window.rootVisualElement.WorldToLocal(
                    new Vector2(actionBtn.worldBound.x, actionBtn.worldBound.y + actionBtn.worldBound.height));
                MenuDropdown.Show(_window.rootVisualElement, pos, menu =>
                {
                    menu.AddItem("Start State Machine", () =>
                    {
                        state.ExternalAction = ExternalStateMachineAction.StartStateMachine;
                        _window.NotifySidePanelChanged();
                        UpdateSelection(_selected, _states, _connections, _blackboardVariables);
                    });
                    menu.AddItem("Set State By Name", () =>
                    {
                        state.ExternalAction = ExternalStateMachineAction.SetStateByName;
                        _window.NotifySidePanelChanged();
                        UpdateSelection(_selected, _states, _connections, _blackboardVariables);
                    });
                    menu.AddItem("Set Blackboard Parameter", () =>
                    {
                        state.ExternalAction = ExternalStateMachineAction.SetBlackboardParameter;
                        _window.NotifySidePanelChanged();
                        UpdateSelection(_selected, _states, _connections, _blackboardVariables);
                    });
                });
            };
            actionRow.Add(actionBtn);
            _scrollView.Add(actionRow);

            if (state.ExternalAction == ExternalStateMachineAction.SetStateByName)
            {
                var targetStates = GetTargetStateNames(state);

                var nameRow = new VisualElement();
                nameRow.AddToClassList("info-row");

                var nameLabel = new Label("State");
                nameLabel.AddToClassList("info-row-label");
                nameRow.Add(nameLabel);

                var currentStateName = state.ExternalTargetStateName;
                var nameBtn = new Button();
                nameBtn.AddToClassList("script-picker-button");
                nameBtn.text = string.IsNullOrEmpty(currentStateName) ? "Select state..." : currentStateName;
                nameBtn.clicked += () =>
                {
                    var pos = _window.rootVisualElement.WorldToLocal(
                        new Vector2(nameBtn.worldBound.x, nameBtn.worldBound.y + nameBtn.worldBound.height));
                    MenuDropdown.Show(_window.rootVisualElement, pos, menu =>
                    {
                        if (targetStates != null && targetStates.Count > 0)
                        {
                            for (int i = 0; i < targetStates.Count; i++)
                            {
                                var captured = targetStates[i];
                                menu.AddItem(captured, () =>
                                {
                                    state.ExternalTargetStateName = captured;
                                    _window.NotifySidePanelChanged();
                                    UpdateSelection(_selected, _states, _connections, _blackboardVariables);
                                });
                            }
                        }
                        else
                        {
                            menu.AddDisabledItem("No states on target");
                        }
                    });
                };
                nameRow.Add(nameBtn);
                _scrollView.Add(nameRow);
            }
            else if (state.ExternalAction == ExternalStateMachineAction.SetBlackboardParameter)
            {
                var bbVars = GetTargetBlackboardVariables(state);

                var parmRow = new VisualElement();
                parmRow.AddToClassList("info-row");

                var parmLabel = new Label("Parameter");
                parmLabel.AddToClassList("info-row-label");
                parmRow.Add(parmLabel);

                var currentParmName = state.ExternalBlackboardParmName;
                var parmBtn = new Button();
                parmBtn.AddToClassList("script-picker-button");
                parmBtn.text = string.IsNullOrEmpty(currentParmName) ? "Select variable..." : currentParmName;
                parmBtn.clicked += () =>
                {
                    var pos = _window.rootVisualElement.WorldToLocal(
                        new Vector2(parmBtn.worldBound.x, parmBtn.worldBound.y + parmBtn.worldBound.height));
                    MenuDropdown.Show(_window.rootVisualElement, pos, menu =>
                    {
                        menu.AddItem("None (manual)", () =>
                        {
                            state.ExternalBlackboardParmName = "";
                            _window.NotifySidePanelChanged();
                            UpdateSelection(_selected, _states, _connections, _blackboardVariables);
                        });
                        if (bbVars != null && bbVars.Count > 0)
                        {
                            menu.AddSeparator();
                            for (int i = 0; i < bbVars.Count; i++)
                            {
                                var captured = bbVars[i];
                                menu.AddItem(captured.Name + " (" + captured.Type + ")", () =>
                                {
                                    state.ExternalBlackboardParmName = captured.Name;
                                    state.ExternalBlackboardParmType = captured.Type;
                                    _window.NotifySidePanelChanged();
                                    UpdateSelection(_selected, _states, _connections, _blackboardVariables);
                                });
                            }
                        }
                        else if (state.ExternalStateMachine != null)
                        {
                            menu.AddSeparator();
                            menu.AddDisabledItem("No variables on target");
                        }
                    });
                };
                parmRow.Add(parmBtn);
                _scrollView.Add(parmRow);

                var hasSelectedVar = !string.IsNullOrEmpty(state.ExternalBlackboardParmName);

                var typeRow = new VisualElement();
                typeRow.AddToClassList("info-row");

                var typeLabel = new Label("Type");
                typeLabel.AddToClassList("info-row-label");
                typeRow.Add(typeLabel);

                var bbType = state.ExternalBlackboardParmType;
                var typeBtn = new Button();
                typeBtn.AddToClassList("script-picker-button");
                typeBtn.text = bbType.ToString();
                typeBtn.SetEnabled(!hasSelectedVar);
                typeBtn.clicked += () =>
                {
                    var pos = _window.rootVisualElement.WorldToLocal(
                        new Vector2(typeBtn.worldBound.x, typeBtn.worldBound.y + typeBtn.worldBound.height));
                    MenuDropdown.Show(_window.rootVisualElement, pos, menu =>
                    {
                        foreach (BlackboardVariableType t in System.Enum.GetValues(typeof(BlackboardVariableType)))
                        {
                            var captured = t;
                            menu.AddItem(t.ToString(), () =>
                            {
                                state.ExternalBlackboardParmType = captured;
                                _window.NotifySidePanelChanged();
                                UpdateSelection(_selected, _states, _connections, _blackboardVariables);
                            });
                        }
                    });
                };
                typeRow.Add(typeBtn);
                _scrollView.Add(typeRow);

                var valueRow = new VisualElement();
                valueRow.AddToClassList("info-row");
                valueRow.style.height = StyleKeyword.Auto;
                valueRow.style.minHeight = 32;

                var valueLabel = new Label("Value");
                valueLabel.AddToClassList("info-row-label");
                valueLabel.style.alignSelf = Align.FlexStart;
                valueLabel.style.marginTop = 6;
                valueRow.Add(valueLabel);

                VisualElement valueControl = BuildTypedBlackboardValueEditor(state);
                valueControl.AddToClassList("info-row-value");
                valueRow.Add(valueControl);
                _scrollView.Add(valueRow);
            }
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
                else if (prop.type == "BlackboardVariableSelector")
                {
                    content = BuildBbVarSelectorField(so, prop.Copy());
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
                so.Update();
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
                                if (bv.Type == bbType ||
                                    (bbType == BlackboardVariableType.Int && bv.Type == BlackboardVariableType.Float) ||
                                    (bbType == BlackboardVariableType.Float && bv.Type == BlackboardVariableType.Int))
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
                        case BlackboardVariableType.Trigger:
                        {
                            var triggerToggle = new TriggerToggle(
                                bool.TryParse(defaultValueProp.stringValue, out var tv) && tv);
                            triggerToggle.OnValueChanged += newValue =>
                            {
                                defaultValueProp.stringValue = newValue.ToString();
                                so.ApplyModifiedProperties();
                                EditorUtility.SetDirty(so.targetObject);
                            };
                            valueArea.Add(triggerToggle);
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

        private VisualElement BuildEnumField(SerializedObject so, SerializedProperty prop, Action onChanged = null)
        {
            var names = prop.enumNames;
            var btn = new Button();
            btn.AddToClassList("bb-mode-button");

            void UpdateText()
            {
                so.Update();
                int idx = prop.enumValueIndex;
                btn.text = idx >= 0 && idx < names.Length
                    ? ObjectNames.NicifyVariableName(names[idx])
                    : names[0];
            }

            UpdateText();

            btn.clicked += () =>
            {
                var pos = _window.rootVisualElement.WorldToLocal(
                    new Vector2(btn.worldBound.x, btn.worldBound.y + btn.worldBound.height));
                MenuDropdown.Show(_window.rootVisualElement, pos, menu =>
                {
                    so.Update();
                    for (int i = 0; i < names.Length; i++)
                    {
                        int capturedIndex = i;
                        string displayName = ObjectNames.NicifyVariableName(names[i]);
                        menu.AddItem(displayName, () =>
                        {
                            prop.enumValueIndex = capturedIndex;
                            so.ApplyModifiedProperties();
                            EditorUtility.SetDirty(so.targetObject);
                            UpdateText();
                            onChanged?.Invoke();
                        });
                    }
                });
            };

            return btn;
        }

        private static VisualElement BuildBoolField(SerializedObject so, SerializedProperty prop)
        {
            var toggle = new Toggle();
            toggle.value = prop.boolValue;
            toggle.RegisterValueChangedCallback(evt =>
            {
                prop.boolValue = evt.newValue;
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(so.targetObject);
            });
            return toggle;
        }

        private VisualElement BuildBbVarSelectorField(SerializedObject so, SerializedProperty prop)
        {
            var row = new VisualElement();
            row.AddToClassList("bb-varref-row");

            var varNameProp = prop.FindPropertyRelative("VariableName");
            var valueTypeProp = prop.FindPropertyRelative("ValueType");

            var dropdownBtn = new Button();
            dropdownBtn.AddToClassList("bb-dropdown-button");
            row.Add(dropdownBtn);

            Action rebuild = null;
            rebuild = () =>
            {
                so.Update();
                string currentVarName = varNameProp.stringValue;
                dropdownBtn.text = string.IsNullOrEmpty(currentVarName)
                    ? "Select variable..." : currentVarName;
            };

            dropdownBtn.clicked += () =>
            {
                var pos = _window.rootVisualElement.WorldToLocal(
                    new Vector2(dropdownBtn.worldBound.x, dropdownBtn.worldBound.y + dropdownBtn.worldBound.height));
                MenuDropdown.Show(_window.rootVisualElement, pos, menu =>
                {
                    menu.AddItem("None", () =>
                    {
                        varNameProp.stringValue = "";
                        so.ApplyModifiedProperties();
                        EditorUtility.SetDirty(so.targetObject);
                        rebuild();
                        schedule.Execute(() => UpdateSelection(_selected, _states, _connections, _blackboardVariables)).StartingIn(0);
                    });
                    menu.AddSeparator();
                    bool hasMatch = false;
                    for (int i = 0; i < _blackboardVariables.Count; i++)
                    {
                        hasMatch = true;
                        var bv = _blackboardVariables[i];
                        string varName = bv.Name;
                        string captured = varName;
                        BlackboardVariableType capturedType = bv.Type;
                        menu.AddItem($"{varName}  ({bv.Type})", () =>
                        {
                            varNameProp.stringValue = captured;
                            valueTypeProp.enumValueIndex = (int)capturedType;

                            var valueProp = so.FindProperty("value");
                            if (valueProp != null)
                            {
                                var valueValueTypeProp = valueProp.FindPropertyRelative("ValueType");
                                if (valueValueTypeProp != null)
                                    valueValueTypeProp.enumValueIndex = (int)capturedType;
                            }

                            so.ApplyModifiedProperties();
                            EditorUtility.SetDirty(so.targetObject);
                            rebuild();
                            schedule.Execute(() => UpdateSelection(_selected, _states, _connections, _blackboardVariables)).StartingIn(0);
                        });
                    }
                    if (!hasMatch)
                        menu.AddDisabledItem("No variables in blackboard");
                });
            };

            rebuild();
            return row;
        }

        // ─── VALIDATION ────────────────────────────────────────────────

        private void BuildValidationMessages(StateView state)
        {
            var messages = GraphValidation.GetStateMessages(state, _connections);

            for (int i = 0; i < state.BehaviourEntries.Count; i++)
            {
                var entry = state.BehaviourEntries[i];
                if (entry.Instance != null)
                    messages.AddRange(GetBbVarValidationMessages(entry.Instance));
            }

            if (messages.Count == 0) return;

            AddDivider();

            for (int i = 0; i < messages.Count; i++)
            {
                _scrollView.Add(BuildValidationMessageRow(messages[i]));
            }
        }

        private void BuildConnectionValidationMessages(ConnectionView conn)
        {
            var messages = GraphValidation.GetConnectionMessages(conn);

            for (int i = 0; i < conn.ConditionEntries.Count; i++)
            {
                var entry = conn.ConditionEntries[i];
                if (entry.Instance != null)
                    messages.AddRange(GetBbVarValidationMessages(entry.Instance));
            }

            if (messages.Count == 0) return;

            AddDivider();

            for (int i = 0; i < messages.Count; i++)
            {
                _scrollView.Add(BuildValidationMessageRow(messages[i]));
            }
        }

        private List<ValidationMessage> GetBbVarValidationMessages(ScriptableObject instance)
        {
            var messages = new List<ValidationMessage>();
            if (instance == null || _blackboardVariables == null) return messages;

            var so = new SerializedObject(instance);
            var prop = so.GetIterator();
            bool enterChildren = true;
            while (prop.NextVisible(enterChildren))
            {
                enterChildren = false;
                if (prop.type == "BlackboardVariableReference")
                {
                    var varNameProp = prop.FindPropertyRelative("BlackboardVariableName");
                    if (varNameProp != null && !string.IsNullOrEmpty(varNameProp.stringValue))
                    {
                        bool exists = false;
                        for (int i = 0; i < _blackboardVariables.Count; i++)
                        {
                            if (_blackboardVariables[i].Name == varNameProp.stringValue)
                            {
                                exists = true;
                                break;
                            }
                        }
                        if (!exists)
                            messages.Add(new ValidationMessage(ValidationMessageType.Error,
                                $"Blackboard variable \"{varNameProp.stringValue}\" not found"));
                    }
                }
                else if (prop.type == "BlackboardVariableSelector")
                {
                    var varNameProp = prop.FindPropertyRelative("VariableName");
                    if (varNameProp != null && !string.IsNullOrEmpty(varNameProp.stringValue))
                    {
                        bool exists = false;
                        for (int i = 0; i < _blackboardVariables.Count; i++)
                        {
                            if (_blackboardVariables[i].Name == varNameProp.stringValue)
                            {
                                exists = true;
                                break;
                            }
                        }
                        if (!exists)
                            messages.Add(new ValidationMessage(ValidationMessageType.Error,
                                $"Blackboard variable \"{varNameProp.stringValue}\" not found in blackboard"));
                    }
                }
            }

            return messages;
        }

        private static VisualElement BuildValidationMessageRow(ValidationMessage msg)
        {
            var row = new VisualElement();
            row.AddToClassList("validation-row");

            string className = msg.Type switch
            {
                ValidationMessageType.Error => "validation-row--error",
                ValidationMessageType.Warning => "validation-row--warning",
                _ => "validation-row--info"
            };
            row.AddToClassList(className);

            string icon = msg.Type switch
            {
                ValidationMessageType.Error => "\u2716",
                ValidationMessageType.Warning => "\u26A0",
                _ => "\u2139"
            };

            var iconLabel = new Label(icon);
            iconLabel.AddToClassList("validation-icon");
            row.Add(iconLabel);

            var textLabel = new Label(msg.Text);
            textLabel.AddToClassList("validation-text");
            row.Add(textLabel);

            return row;
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

        private static string GetBehaviourDisplayName(MonoScript script)
        {
            var type = script.GetClass();
            if (type == null) return script.name;
            if (type.IsSubclassOf(typeof(StateBehaviour)))
            {
                var instance = (StateBehaviour)ScriptableObject.CreateInstance(type);
                string name = instance.DisplayName;
                Object.DestroyImmediate(instance);
                return name;
            }
            return type.Name;
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

        private static List<BlackboardVariable> GetTargetBlackboardVariables(StateView state)
        {
            if (state.ExternalStateMachine == null) return null;

            var sm = state.ExternalStateMachine.GetComponent<StateMachineComponent>();
            if (sm == null || sm.Controller == null || sm.Controller.Data == null)
                return null;

            return sm.Controller.Data.BlackboardVariables;
        }

        private static List<string> GetTargetStateNames(StateView state)
        {
            if (state.ExternalStateMachine == null) return null;

            var sm = state.ExternalStateMachine.GetComponent<StateMachineComponent>();
            if (sm == null || sm.Controller == null || sm.Controller.Data == null)
                return null;

            var data = sm.Controller.Data;
            var names = new List<string>(data.States.Count);
            for (int i = 0; i < data.States.Count; i++)
                names.Add(data.States[i].Name);

            return names;
        }

        private VisualElement BuildTypedBlackboardValueEditor(StateView state)
        {
            var bbType = state.ExternalBlackboardParmType;
            var currentValue = state.ExternalBlackboardParmValue ?? "";

            VisualElement MakeFlexField(VisualElement field)
            {
                field.style.flexGrow = 1;
                field.style.width = Length.Percent(100);
                return field;
            }

            switch (bbType)
            {
                case BlackboardVariableType.Bool:
                {
                    var toggle = new Toggle();
                    toggle.value = bool.TryParse(currentValue, out var bv) && bv;
                    toggle.RegisterValueChangedCallback(evt =>
                    {
                        state.ExternalBlackboardParmValue = evt.newValue.ToString();
                        _window.NotifySidePanelChanged();
                    });
                    return MakeFlexField(toggle);
                }
                case BlackboardVariableType.Int:
                {
                    var field = new IntegerField();
                    field.value = int.TryParse(currentValue, out var iv) ? iv : 0;
                    field.RegisterValueChangedCallback(evt =>
                    {
                        state.ExternalBlackboardParmValue = evt.newValue.ToString();
                        _window.NotifySidePanelChanged();
                    });
                    return MakeFlexField(field);
                }
                case BlackboardVariableType.Float:
                {
                    var field = new FloatField();
                    field.value = float.TryParse(currentValue,
                        System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out var fv) ? fv : 0f;
                    field.RegisterValueChangedCallback(evt =>
                    {
                        state.ExternalBlackboardParmValue = evt.newValue.ToString("G",
                            System.Globalization.CultureInfo.InvariantCulture);
                        _window.NotifySidePanelChanged();
                    });
                    return MakeFlexField(field);
                }
                case BlackboardVariableType.String:
                {
                    var field = new TextField();
                    field.value = currentValue;
                    field.multiline = true;
                    field.style.whiteSpace = WhiteSpace.Normal;
                    field.style.minHeight = 56;
                    field.RegisterValueChangedCallback(evt =>
                    {
                        state.ExternalBlackboardParmValue = evt.newValue;
                        _window.NotifySidePanelChanged();
                    });
                    return MakeFlexField(field);
                }
                case BlackboardVariableType.Trigger:
                {
                    var triggerToggle = new TriggerToggle(
                        bool.TryParse(currentValue, out var tv) && tv);
                    triggerToggle.OnValueChanged += newValue =>
                    {
                        state.ExternalBlackboardParmValue = newValue.ToString();
                        _window.NotifySidePanelChanged();
                    };
                    return triggerToggle;
                }
                default:
                {
                    var field = new TextField();
                    field.value = currentValue;
                    field.RegisterValueChangedCallback(evt =>
                    {
                        state.ExternalBlackboardParmValue = evt.newValue;
                        _window.NotifySidePanelChanged();
                    });
                    return MakeFlexField(field);
                }
            }
        }
    }
}

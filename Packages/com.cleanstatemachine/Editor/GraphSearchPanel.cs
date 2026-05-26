using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace CleanStateMachine
{
    internal enum SearchResultType
    {
        State,
        Connection,
        Behaviour,
        Condition,
        BlackboardVariable
    }

    internal struct SearchResult
    {
        public SearchResultType Type;
        public string DisplayText;
        public string DetailText;
        public string ContextPath;
        public StateView State;
        public ConnectionView Connection;
        public int EntryIndex;
        public BlackboardVariable Variable;
        public List<int> SubStatePath;
    }

    internal class GraphSearchPanel
    {
        private readonly CleanStateMachineWindow _window;
        private VisualElement _overlay;
        private VisualElement _panel;
        private TextField _searchField;
        private ScrollView _resultsScrollView;
        private VisualElement _resultsContainer;
        private Label _noResultsLabel;
        private bool _isVisible;
        private string _lastQuery = "";
        private readonly List<SearchResult> _results = new List<SearchResult>();
        private int _selectedIndex = -1;
        private const float PanelHeight = 380f;
        private Label _placeholder;

        private static readonly Color AccentColor = new Color(0.3f, 0.85f, 1f);

        public bool IsVisible => _isVisible;

        public GraphSearchPanel(CleanStateMachineWindow window)
        {
            _window = window;
        }

        public void Show()
        {
            if (_isVisible) return;

            BuildOverlay();
            _window.rootVisualElement.Add(_overlay);
            _isVisible = true;
            _lastQuery = "";
            _results.Clear();
            _selectedIndex = -1;

            UpdatePlaceholderVisibility();

            _overlay.schedule.Execute(() =>
            {
                _searchField?.Focus();
            }).StartingIn(10);
        }

        public void Hide()
        {
            if (!_isVisible) return;
            _isVisible = false;
            if (_overlay?.parent != null)
                _overlay.RemoveFromHierarchy();
            _overlay = null;
            _panel = null;
            _searchField = null;
            _resultsScrollView = null;
            _resultsContainer = null;
            _noResultsLabel = null;
        }

        private void UpdatePlaceholderVisibility()
        {
            if (_placeholder == null || _searchField == null) return;
            _placeholder.style.display = string.IsNullOrEmpty(_searchField.value)
                ? DisplayStyle.Flex
                : DisplayStyle.None;
        }

        private void BuildOverlay()
        {
            _overlay = new VisualElement();
            _overlay.style.position = Position.Absolute;
            _overlay.style.left = 0;
            _overlay.style.top = 0;
            _overlay.style.right = 0;
            _overlay.style.bottom = 0;
            _overlay.pickingMode = PickingMode.Position;
            _overlay.focusable = true;

            _overlay.RegisterCallback<ClickEvent>(evt =>
            {
                if (evt.target == _overlay)
                    Hide();
            });

            _overlay.RegisterCallback<KeyDownEvent>(OnOverlayKeyDown);

            _panel = new VisualElement();
            _panel.AddToClassList("search-panel");
            _panel.style.position = Position.Absolute;
            _panel.style.left = Length.Percent(25);
            _panel.style.right = Length.Percent(25);
            _panel.style.top = 40;
            _panel.style.height = PanelHeight;
            _panel.style.flexDirection = FlexDirection.Column;
            _panel.style.backgroundColor = new Color(0.16f, 0.16f, 0.16f);
            _panel.style.borderTopLeftRadius = 8;
            _panel.style.borderTopRightRadius = 8;
            _panel.style.borderBottomLeftRadius = 8;
            _panel.style.borderBottomRightRadius = 8;
            _panel.style.borderLeftColor = new Color(0.25f, 0.25f, 0.25f);
            _panel.style.borderRightColor = new Color(0.25f, 0.25f, 0.25f);
            _panel.style.borderTopColor = new Color(0.25f, 0.25f, 0.25f);
            _panel.style.borderBottomColor = new Color(0.25f, 0.25f, 0.25f);
            _panel.style.borderLeftWidth = 1f;
            _panel.style.borderRightWidth = 1f;
            _panel.style.borderTopWidth = 1f;
            _panel.style.borderBottomWidth = 1f;

            var inputRow = new VisualElement();
            inputRow.style.flexDirection = FlexDirection.Row;
            inputRow.style.alignItems = Align.Center;
            inputRow.style.paddingLeft = 12;
            inputRow.style.paddingRight = 8;
            inputRow.style.paddingTop = 8;
            inputRow.style.paddingBottom = 8;
            inputRow.style.flexShrink = 0;
            inputRow.style.borderBottomColor = new Color(0.2f, 0.2f, 0.2f);
            inputRow.style.borderBottomWidth = 1f;

            var searchIcon = new Label("\u2315");
            searchIcon.style.fontSize = 16;
            searchIcon.style.color = new Color(0.5f, 0.5f, 0.5f);
            searchIcon.style.marginRight = 8;
            searchIcon.style.flexShrink = 0;
            inputRow.Add(searchIcon);

            _placeholder = new Label("Search states, behaviours, conditions...");
            _placeholder.style.position = Position.Absolute;
            _placeholder.style.left = 44f;
            _placeholder.style.right = 44f;
            _placeholder.style.top = 0f;
            _placeholder.style.bottom = 0f;
            _placeholder.style.fontSize = 14;
            _placeholder.style.color = new Color(0.4f, 0.4f, 0.4f);
            _placeholder.style.unityTextAlign = TextAnchor.MiddleLeft;
            _placeholder.pickingMode = PickingMode.Ignore;
            inputRow.Add(_placeholder);

            _searchField = new TextField();
            _searchField.style.flexGrow = 1;
            _searchField.style.flexShrink = 1;
            _searchField.style.backgroundColor = new Color(0, 0, 0, 0);
            _searchField.style.borderLeftWidth = 0;
            _searchField.style.borderRightWidth = 0;
            _searchField.style.borderTopWidth = 0;
            _searchField.style.borderBottomWidth = 0;
            _searchField.RegisterValueChangedCallback(OnSearchTextChanged);
            _searchField.RegisterValueChangedCallback(_ => UpdatePlaceholderVisibility());

            var searchInput = _searchField.Q(className: "unity-base-text-field__input");
            if (searchInput != null)
            {
                searchInput.style.fontSize = 14;
                searchInput.style.color = new Color(0.9f, 0.9f, 0.9f);
                searchInput.style.backgroundColor = new Color(0, 0, 0, 0);
                searchInput.style.borderLeftWidth = 0;
                searchInput.style.borderRightWidth = 0;
                searchInput.style.borderTopWidth = 0;
                searchInput.style.borderBottomWidth = 0;
            }

            inputRow.Add(_searchField);

            var closeBtn = new Button(() => Hide());
            closeBtn.text = "\u2715";
            closeBtn.style.width = 24;
            closeBtn.style.height = 24;
            closeBtn.style.fontSize = 14;
            closeBtn.style.backgroundColor = new Color(0.25f, 0.25f, 0.25f);
            closeBtn.style.color = new Color(0.7f, 0.7f, 0.7f);
            closeBtn.style.borderTopLeftRadius = 4;
            closeBtn.style.borderTopRightRadius = 4;
            closeBtn.style.borderBottomLeftRadius = 4;
            closeBtn.style.borderBottomRightRadius = 4;
            closeBtn.style.borderLeftWidth = 0;
            closeBtn.style.borderRightWidth = 0;
            closeBtn.style.borderTopWidth = 0;
            closeBtn.style.borderBottomWidth = 0;
            closeBtn.style.paddingLeft = 0;
            closeBtn.style.paddingRight = 0;
            closeBtn.style.flexShrink = 0;
            closeBtn.style.justifyContent = Justify.Center;
            closeBtn.style.alignItems = Align.Center;
            inputRow.Add(closeBtn);

            _panel.Add(inputRow);

            _resultsScrollView = new ScrollView();
            _resultsScrollView.style.flexGrow = 1;
            _resultsScrollView.style.flexShrink = 1;
            _resultsScrollView.style.paddingLeft = 4;
            _resultsScrollView.style.paddingRight = 4;
            _resultsScrollView.style.paddingBottom = 4;

            _resultsContainer = new VisualElement();
            _resultsContainer.style.flexDirection = FlexDirection.Column;
            _resultsScrollView.Add(_resultsContainer);

            _noResultsLabel = new Label("No results found");
            _noResultsLabel.style.fontSize = 12;
            _noResultsLabel.style.color = new Color(0.5f, 0.5f, 0.5f);
            _noResultsLabel.style.paddingLeft = 16;
            _noResultsLabel.style.paddingTop = 16;
            _noResultsLabel.style.paddingBottom = 16;
            _noResultsLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            _noResultsLabel.style.display = DisplayStyle.None;

            _panel.Add(_resultsScrollView);
            _panel.Add(_noResultsLabel);

            var ss = ScriptReferenceUtility.LoadStyleSheet("GraphSearchPanel");
            if (ss != null)
                _panel.styleSheets.Add(ss);

            _overlay.Add(_panel);
        }

        private void OnOverlayKeyDown(KeyDownEvent e)
        {
            if (e.keyCode == KeyCode.Escape)
            {
                Hide();
                e.StopPropagation();
                return;
            }

            if (e.keyCode == KeyCode.DownArrow)
            {
                if (_results.Count == 0) return;
                _selectedIndex = (_selectedIndex + 1) % _results.Count;
                UpdateSelectionHighlight();
                ScrollToSelected();
                e.StopPropagation();
                return;
            }

            if (e.keyCode == KeyCode.UpArrow)
            {
                if (_results.Count == 0) return;
                _selectedIndex = (_selectedIndex - 1 + _results.Count) % _results.Count;
                UpdateSelectionHighlight();
                ScrollToSelected();
                e.StopPropagation();
                return;
            }

            if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)
            {
                if (_selectedIndex >= 0 && _selectedIndex < _results.Count)
                    NavigateTo(_results[_selectedIndex]);
                e.StopPropagation();
            }
        }

        private void OnSearchTextChanged(ChangeEvent<string> evt)
        {
            PerformSearch(evt.newValue);
        }

        private void PerformSearch(string query)
        {
            _lastQuery = query;
            _results.Clear();
            _resultsContainer.Clear();
            _selectedIndex = -1;

            if (string.IsNullOrWhiteSpace(query))
            {
                _noResultsLabel.style.display = DisplayStyle.None;
                return;
            }

            string lowerQuery = query.ToLowerInvariant();
            var seenConnections = new HashSet<ConnectionView>();
            var seenBehaviourResults = new HashSet<(StateView, int)>();

            SearchStates(lowerQuery);
            SearchBehaviours(lowerQuery, seenBehaviourResults);
            SearchConditions(lowerQuery, seenConnections);
            SearchConnections(lowerQuery, seenConnections);
            SearchBlackboardVariables(lowerQuery);

            if (_results.Count == 0)
            {
                _noResultsLabel.style.display = DisplayStyle.Flex;
            }
            else
            {
                _noResultsLabel.style.display = DisplayStyle.None;
                for (int i = 0; i < _results.Count; i++)
                {
                    var row = BuildResultRow(_results[i], i);
                    _resultsContainer.Add(row);
                }
            }
        }

        private void SearchStates(string lowerQuery)
        {
            for (int i = 0; i < _window.States.Count; i++)
            {
                var state = _window.States[i];
                if (string.IsNullOrEmpty(state.Name)) continue;

                if (!state.Name.ToLowerInvariant().Contains(lowerQuery)) continue;

                var parentPath = new List<int>();
                GetParentPath(state, parentPath);

                _results.Add(new SearchResult
                {
                    Type = SearchResultType.State,
                    DisplayText = state.Name,
                    DetailText = GetStateTypeLabel(state),
                    ContextPath = BuildContextPath(parentPath),
                    State = state,
                    SubStatePath = parentPath
                });
            }
        }

        private void SearchBehaviours(string lowerQuery, HashSet<(StateView, int)> seen)
        {
            for (int i = 0; i < _window.States.Count; i++)
            {
                var state = _window.States[i];
                if (state.BehaviourEntries == null) continue;

                for (int j = 0; j < state.BehaviourEntries.Count; j++)
                {
                    var entry = state.BehaviourEntries[j];
                    string typeName = (entry.TypeName ?? "").ToLowerInvariant();
                    string displayName = "";

                    if (typeName.Contains(lowerQuery))
                        displayName = GetShortTypeName(entry.TypeName);
                    else if (entry.Instance != null)
                    {
                        var sb = entry.Instance as StateBehaviour;
                        if (sb != null)
                            displayName = sb.DisplayName ?? "";
                        if (!displayName.ToLowerInvariant().Contains(lowerQuery))
                            continue;
                    }
                    else
                    {
                        continue;
                    }

                    if (seen.Contains((state, j))) continue;
                    seen.Add((state, j));

                    var parentPath = new List<int>();
                    GetParentPath(state, parentPath);

                    _results.Add(new SearchResult
                    {
                        Type = SearchResultType.Behaviour,
                        DisplayText = displayName,
                        DetailText = "on " + state.Name,
                        ContextPath = BuildContextPath(parentPath),
                        State = state,
                        EntryIndex = j,
                        SubStatePath = parentPath
                    });
                }
            }
        }

        private void SearchConditions(string lowerQuery, HashSet<ConnectionView> seen)
        {
            for (int i = 0; i < _window.Connections.Count; i++)
            {
                var conn = _window.Connections[i];
                if (conn.ConditionEntries == null) continue;

                for (int j = 0; j < conn.ConditionEntries.Count; j++)
                {
                    var entry = conn.ConditionEntries[j];
                    string typeName = (entry.TypeName ?? "").ToLowerInvariant();
                    string displayName = "";

                    if (typeName.Contains(lowerQuery))
                        displayName = GetShortTypeName(entry.TypeName);
                    else if (entry.Instance != null)
                    {
                        var cs = entry.Instance as ConditionScript;
                        if (cs != null)
                            displayName = cs.DisplayName ?? "";
                        if (!displayName.ToLowerInvariant().Contains(lowerQuery))
                            continue;
                    }
                    else
                    {
                        continue;
                    }

                    var parentPath = new List<int>();
                    GetParentPath(conn.From, parentPath);

                    _results.Add(new SearchResult
                    {
                        Type = SearchResultType.Condition,
                        DisplayText = displayName,
                        DetailText = conn.From.Name + " \u2192 " + conn.To.Name,
                        ContextPath = BuildContextPath(parentPath),
                        Connection = conn,
                        EntryIndex = j,
                        SubStatePath = parentPath
                    });
                    seen.Add(conn);
                }
            }
        }

        private void SearchConnections(string lowerQuery, HashSet<ConnectionView> seen)
        {
            for (int i = 0; i < _window.Connections.Count; i++)
            {
                var conn = _window.Connections[i];
                if (seen.Contains(conn)) continue;

                if (!conn.From.Name.ToLowerInvariant().Contains(lowerQuery) &&
                    !conn.To.Name.ToLowerInvariant().Contains(lowerQuery))
                    continue;

                var parentPath = new List<int>();
                GetParentPath(conn.From, parentPath);

                _results.Add(new SearchResult
                {
                    Type = SearchResultType.Connection,
                    DisplayText = conn.From.Name + " \u2192 " + conn.To.Name,
                    DetailText = conn.ConditionEntries.Count > 0
                        ? conn.ConditionEntries.Count + " condition(s)"
                        : "no conditions",
                    ContextPath = BuildContextPath(parentPath),
                    Connection = conn,
                    SubStatePath = parentPath
                });
            }
        }

        private void SearchBlackboardVariables(string lowerQuery)
        {
            for (int i = 0; i < _window.BlackboardVariables.Count; i++)
            {
                var variable = _window.BlackboardVariables[i];
                if (!variable.Name.ToLowerInvariant().Contains(lowerQuery)) continue;

                _results.Add(new SearchResult
                {
                    Type = SearchResultType.BlackboardVariable,
                    DisplayText = variable.Name,
                    DetailText = variable.Type.ToString(),
                    ContextPath = "",
                    Variable = variable
                });
            }
        }

        private VisualElement BuildResultRow(SearchResult result, int index)
        {
            int capturedIndex = index;

            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.paddingLeft = 10;
            row.style.paddingRight = 10;
            row.style.paddingTop = 6;
            row.style.paddingBottom = 6;
            row.style.minHeight = 32;
            row.style.flexShrink = 0;
            row.style.borderBottomColor = new Color(0.1f, 0.1f, 0.1f);
            row.style.borderBottomWidth = 1f;

            row.RegisterCallback<ClickEvent>(evt =>
            {
                _selectedIndex = capturedIndex;
                NavigateTo(result);
            });

            row.RegisterCallback<MouseEnterEvent>(evt =>
            {
                _selectedIndex = capturedIndex;
                UpdateSelectionHighlight();
            });

            var icon = new Label(GetTypeIcon(result.Type));
            icon.style.fontSize = 14;
            icon.style.width = 24;
            icon.style.flexShrink = 0;
            icon.style.unityTextAlign = TextAnchor.MiddleCenter;
            icon.style.color = GetTypeColor(result.Type);
            row.Add(icon);

            var textCol = new VisualElement();
            textCol.style.flexDirection = FlexDirection.Column;
            textCol.style.flexGrow = 1;
            textCol.style.flexShrink = 1;
            textCol.style.overflow = Overflow.Hidden;

            var displayLabel = new Label(result.DisplayText);
            displayLabel.style.fontSize = 13;
            displayLabel.style.color = new Color(0.9f, 0.9f, 0.9f);
            displayLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
            displayLabel.style.overflow = Overflow.Hidden;
            displayLabel.style.textOverflow = TextOverflow.Ellipsis;
            textCol.Add(displayLabel);

            var detailLabel = new Label(result.DetailText);
            detailLabel.style.fontSize = 10;
            detailLabel.style.color = new Color(0.5f, 0.5f, 0.5f);
            detailLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
            detailLabel.style.overflow = Overflow.Hidden;
            detailLabel.style.textOverflow = TextOverflow.Ellipsis;
            textCol.Add(detailLabel);

            row.Add(textCol);

            if (!string.IsNullOrEmpty(result.ContextPath))
            {
                var contextLabel = new Label(result.ContextPath);
                contextLabel.style.fontSize = 9;
                contextLabel.style.color = new Color(0.4f, 0.4f, 0.4f);
                contextLabel.style.flexShrink = 0;
                contextLabel.style.marginLeft = 8;
                row.Add(contextLabel);
            }

            var typeBadge = new Label(result.Type switch
            {
                SearchResultType.State => "State",
                SearchResultType.Connection => "Transition",
                SearchResultType.Behaviour => "Behaviour",
                SearchResultType.Condition => "Condition",
                SearchResultType.BlackboardVariable => "Variable",
                _ => ""
            });
            typeBadge.style.fontSize = 8;
            typeBadge.style.color = new Color(0.5f, 0.5f, 0.5f);
            typeBadge.style.backgroundColor = new Color(0.2f, 0.2f, 0.2f);
            typeBadge.style.paddingLeft = 4;
            typeBadge.style.paddingRight = 4;
            typeBadge.style.paddingTop = 1;
            typeBadge.style.paddingBottom = 1;
            typeBadge.style.borderTopLeftRadius = 3;
            typeBadge.style.borderTopRightRadius = 3;
            typeBadge.style.borderBottomLeftRadius = 3;
            typeBadge.style.borderBottomRightRadius = 3;
            typeBadge.style.marginLeft = 8;
            typeBadge.style.flexShrink = 0;
            row.Add(typeBadge);

            return row;
        }

        private void UpdateSelectionHighlight()
        {
            for (int i = 0; i < _resultsContainer.childCount; i++)
            {
                var child = _resultsContainer[i];
                Color bg = i == _selectedIndex
                    ? new Color(0.25f, 0.35f, 0.5f)
                    : new Color(0, 0, 0, 0);
                child.style.backgroundColor = bg;
            }
        }

        private void ScrollToSelected()
        {
            if (_selectedIndex < 0 || _selectedIndex >= _resultsContainer.childCount) return;
            var target = _resultsContainer[_selectedIndex];
            _resultsScrollView.ScrollTo(target);
        }

        private void GetParentPath(StateView state, List<int> parentPath)
        {
            var rawChain = new List<int>();
            FindParentChain(state.DataIndex, rawChain);
            rawChain.Reverse();
            parentPath.AddRange(rawChain);
        }

        private bool FindParentChain(int childIndex, List<int> chain)
        {
            for (int i = 0; i < _window.States.Count; i++)
            {
                var container = _window.States[i];
                if (container.IsSubStateMachine && container.ChildIndices.Contains(childIndex))
                {
                    chain.Add(container.DataIndex);
                    FindParentChain(container.DataIndex, chain);
                    return true;
                }
            }
            return false;
        }

        private string BuildContextPath(List<int> path)
        {
            if (path.Count == 0) return "";
            var names = new List<string>(path.Count);
            for (int i = 0; i < path.Count; i++)
            {
                var sv = GetStateByIndex(path[i]);
                if (sv != null)
                    names.Add(sv.Name);
            }
            return string.Join(" / ", names);
        }

        private StateView GetStateByIndex(int index)
        {
            for (int i = 0; i < _window.States.Count; i++)
            {
                if (_window.States[i].DataIndex == index)
                    return _window.States[i];
            }
            return null;
        }

        private static string GetStateTypeLabel(StateView s)
        {
            if (s.IsEntry || s.IsSubEntry) return "Entry";
            if (s.IsAnyState) return "Any State";
            if (s.IsSubStateMachine) return "Sub State Machine";
            if (s.IsExternalReference) return "External Reference";
            return "State";
        }

        private static string GetTypeIcon(SearchResultType type)
        {
            switch (type)
            {
                case SearchResultType.State: return "\u25A0";
                case SearchResultType.Connection: return "\u2192";
                case SearchResultType.Behaviour: return "\u25B6";
                case SearchResultType.Condition: return "\u25C9";
                case SearchResultType.BlackboardVariable: return "\u25C8";
                default: return "\u25A0";
            }
        }

        private static Color GetTypeColor(SearchResultType type)
        {
            switch (type)
            {
                case SearchResultType.State: return new Color(0.6f, 0.85f, 0.6f);
                case SearchResultType.Connection: return new Color(0.5f, 0.7f, 0.9f);
                case SearchResultType.Behaviour: return new Color(0.9f, 0.7f, 0.4f);
                case SearchResultType.Condition: return new Color(0.8f, 0.6f, 0.9f);
                case SearchResultType.BlackboardVariable: return new Color(0.5f, 0.8f, 0.85f);
                default: return new Color(0.6f, 0.6f, 0.6f);
            }
        }

        private static string GetShortTypeName(string fullTypeName)
        {
            if (string.IsNullOrEmpty(fullTypeName)) return "";
            int lastDot = fullTypeName.LastIndexOf('.');
            return lastDot >= 0 ? fullTypeName.Substring(lastDot + 1) : fullTypeName;
        }

        private void NavigateTo(SearchResult result)
        {
            Hide();

            switch (result.Type)
            {
                case SearchResultType.State:
                case SearchResultType.Behaviour:
                    NavigateToState(result.State, result.SubStatePath);
                    break;
                case SearchResultType.Connection:
                case SearchResultType.Condition:
                    NavigateToConnection(result.Connection, result.SubStatePath);
                    break;
                case SearchResultType.BlackboardVariable:
                    NavigateToVariable(result.Variable);
                    break;
            }

            _window.Repaint();
        }

        private void NavigateToState(StateView state, List<int> subStatePath)
        {
            _window.ExpandedSubStateStack.Clear();
            if (subStatePath.Count > 0)
            {
                for (int i = 0; i < subStatePath.Count; i++)
                    _window.ExpandedSubStateStack.Add(subStatePath[i]);
            }
            _window.ExpandedView?.UpdateExpandedModeBar();

            _window.SelectionController.SelectOnly(state);

            state.TriggerSearchHighlight();

            Rect bounds = state.GetGraphBounds();
            bounds = new Rect(
                bounds.x - 40f,
                bounds.y - 40f,
                bounds.width + 80f,
                bounds.height + 80f
            );
            _window.ViewAnimator.StartSmoothFocusOnContent(bounds);

            if (_window.SidePanelElement != null)
                _window.SidePanelElement.UpdateSelection();
        }

        private void NavigateToConnection(ConnectionView conn, List<int> subStatePath)
        {
            _window.ExpandedSubStateStack.Clear();
            if (subStatePath.Count > 0)
            {
                for (int i = 0; i < subStatePath.Count; i++)
                    _window.ExpandedSubStateStack.Add(subStatePath[i]);
            }
            _window.ExpandedView?.UpdateExpandedModeBar();

            _window.SelectionController.SelectOnly(conn);

            conn.TriggerSearchHighlight();

            Rect bounds = conn.GetGraphBounds();
            bounds = new Rect(
                bounds.x - 60f,
                bounds.y - 60f,
                bounds.width + 120f,
                bounds.height + 120f
            );
            _window.ViewAnimator.StartSmoothFocusOnContent(bounds);

            if (_window.SidePanelElement != null)
                _window.SidePanelElement.UpdateSelection();
        }

        private void NavigateToVariable(BlackboardVariable variable)
        {
            if (!_window.ShowSidePanel)
            {
                _window.SetShowSidePanel(true);
                _window.SidePanelElement?.SetExpanded(true);
            }

            _window.SidePanelElement?.SelectBlackboardVariable(variable);
        }
    }
}

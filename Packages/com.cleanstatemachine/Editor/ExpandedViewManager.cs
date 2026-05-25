using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace CleanStateMachine
{
    internal class ExpandedViewManager
    {
        private readonly CleanStateMachineWindow _window;

        public ExpandedViewManager(CleanStateMachineWindow window)
        {
            _window = window;
        }

        public bool IsStateVisible(StateView state)
        {
            if (_window.ExpandedSubStateStack.Count > 0)
            {
                int topExpanded = _window.ExpandedSubStateStack[_window.ExpandedSubStateStack.Count - 1];
                if (state.DataIndex == topExpanded) return true;
                var expandedState = GetStateByIndex(topExpanded);
                if (expandedState != null && expandedState.ChildIndices.Contains(state.DataIndex))
                    return true;
                return false;
            }

            if (state.IsEntry) return true;

            for (int i = 0; i < _window.States.Count; i++)
            {
                var container = _window.States[i];
                if (container.IsSubStateMachine && container.ChildIndices.Contains(state.DataIndex))
                    return false;
            }
            return true;
        }

        public bool IsConnectionVisible(ConnectionView conn)
        {
            return IsStateVisible(conn.From) && IsStateVisible(conn.To);
        }

        public void EnterExpandSubState(StateView subStateView)
        {
            if (subStateView == null || !subStateView.IsSubStateMachine) return;
            if (_window.ExpandedSubStateStack.Contains(subStateView.DataIndex)) return;

            _window.ExpandedSubStateStack.Add(subStateView.DataIndex);
            UpdateExpandedModeBar();
            _window.StartSmoothFocusOnContentInternal();
            _window.SidePanelElement?.UpdateSelection();
        }

        public bool IsCurrentExpandedSubState(StateView state)
        {
            return _window.ExpandedSubStateStack.Count > 0 &&
                   _window.ExpandedSubStateStack[_window.ExpandedSubStateStack.Count - 1] == state.DataIndex;
        }

        public void ExitExpandedSubState()
        {
            if (_window.ExpandedSubStateStack.Count > 0)
                _window.ExpandedSubStateStack.RemoveAt(_window.ExpandedSubStateStack.Count - 1);
            UpdateExpandedModeBar();
            _window.StartSmoothFocusOnContentInternal();
            _window.SidePanelElement?.UpdateSelection();
        }

        public Rect ComputeVisibleContentBounds()
        {
            bool first = true;
            float minX = 0f, minY = 0f, maxX = 0f, maxY = 0f;

            for (int i = 0; i < _window.States.Count; i++)
            {
                if (!IsStateVisible(_window.States[i])) continue;

                Rect bounds = _window.States[i].GetGraphBounds();
                if (first)
                {
                    minX = bounds.xMin;
                    minY = bounds.yMin;
                    maxX = bounds.xMax;
                    maxY = bounds.yMax;
                    first = false;
                }
                else
                {
                    if (bounds.xMin < minX) minX = bounds.xMin;
                    if (bounds.yMin < minY) minY = bounds.yMin;
                    if (bounds.xMax > maxX) maxX = bounds.xMax;
                    if (bounds.yMax > maxY) maxY = bounds.yMax;
                }
            }

            if (first)
                return new Rect(0f, 0f, 0f, 0f);

            return Rect.MinMaxRect(minX, minY, maxX, maxY);
        }

        public void UpdateExpandedModeBar()
        {
            if (_window.ExpandedModeBar == null) return;
            if (_window.ExpandedSubStateStack.Count > 0)
            {
                _window.ExpandedModeBar.style.display = DisplayStyle.Flex;
                _window.BreadcrumbContainer.Clear();

                string baseName = _window.Controller != null ? _window.Controller.name : "StateMachine";
                var rootBtn = new Button(() =>
                {
                    _window.ExpandedSubStateStack.Clear();
                    UpdateExpandedModeBar();
                    _window.StartSmoothFocusOnContentInternal();
                    _window.Repaint();
                });
                rootBtn.text = baseName;
                rootBtn.style.fontSize = 11;
                rootBtn.style.backgroundColor = Color.clear;
                rootBtn.style.color = new Color(0.6f, 0.6f, 0.6f);
                rootBtn.style.borderLeftWidth = 0f;
                rootBtn.style.borderRightWidth = 0f;
                rootBtn.style.borderTopWidth = 0f;
                rootBtn.style.borderBottomWidth = 0f;
                rootBtn.style.unityFontStyleAndWeight = FontStyle.Normal;
                rootBtn.style.paddingLeft = 2f;
                rootBtn.style.paddingRight = 2f;
                rootBtn.style.marginLeft = 0f;
                rootBtn.style.marginRight = 4f;
                _window.BreadcrumbContainer.Add(rootBtn);

                for (int i = 0; i < _window.ExpandedSubStateStack.Count; i++)
                {
                    int idx = _window.ExpandedSubStateStack[i];
                    var state = GetStateByIndex(idx);
                    string name = state != null ? state.Name : "?";

                    var sep = new Label(" / ");
                    sep.style.fontSize = 11;
                    sep.style.color = new Color(0.4f, 0.4f, 0.4f);
                    _window.BreadcrumbContainer.Add(sep);

                    int capturedLevel = i;
                    var crumb = new Button(() =>
                    {
                        while (_window.ExpandedSubStateStack.Count > capturedLevel + 1)
                            _window.ExpandedSubStateStack.RemoveAt(_window.ExpandedSubStateStack.Count - 1);
                        UpdateExpandedModeBar();
                        _window.StartSmoothFocusOnContentInternal();
                        _window.Repaint();
                    });
                    crumb.text = name;
                    crumb.style.fontSize = 11;
                    crumb.style.backgroundColor = Color.clear;
                    crumb.style.color = new Color(0.8f, 0.8f, 0.8f);
                    crumb.style.borderLeftWidth = 0f;
                    crumb.style.borderRightWidth = 0f;
                    crumb.style.borderTopWidth = 0f;
                    crumb.style.borderBottomWidth = 0f;
                    crumb.style.unityFontStyleAndWeight = FontStyle.Normal;
                    crumb.style.paddingLeft = 2f;
                    crumb.style.paddingRight = 2f;
                    crumb.style.marginLeft = 0f;
                    crumb.style.marginRight = 0f;
                    _window.BreadcrumbContainer.Add(crumb);
                }
            }
            else
            {
                _window.ExpandedModeBar.style.display = DisplayStyle.None;
            }
        }

        public void FindActiveStateHierarchy(int leafIndex, List<int> result)
        {
            var leafState = GetStateByIndex(leafIndex);
            if (leafState != null && leafState.IsSubStateMachine)
            {
                result.Add(leafIndex);
                return;
            }
            FindParentChain(leafIndex, result);
            result.Reverse();
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

        public static bool AreListsEqual(List<int> a, List<int> b)
        {
            if (a.Count != b.Count) return false;
            for (int i = 0; i < a.Count; i++)
                if (a[i] != b[i]) return false;
            return true;
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
    }
}

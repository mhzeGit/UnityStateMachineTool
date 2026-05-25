using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CleanStateMachine
{
    internal class PlayModeTracker
    {
        private readonly CleanStateMachineWindow _window;
        private readonly ExpandedViewManager _expandedView;

        public PlayModeTracker(CleanStateMachineWindow window, ExpandedViewManager expandedView)
        {
            _window = window;
            _expandedView = expandedView;
        }

        public void OnEditorUpdate()
        {
            if (!Application.isPlaying)
            {
                if (_window.WasPlaying)
                {
                    _window.WasPlaying = false;
                    _window.ActiveStateIndex = -1;
                    _window.ExpandedSubStateStack.Clear();
                    _window.PendingExpandStack = null;
                    _window.LastTransitionFromIndex = -1;
                    _window.LastTransitionToIndex = -1;
                    _window.LastTransitionConnectionIndex = -1;
                    for (int i = 0; i < _window.States.Count; i++)
                        _window.States[i].IsActive = false;
                    for (int i = 0; i < _window.Connections.Count; i++)
                        _window.Connections[i].IsActive = false;
                    _expandedView.UpdateExpandedModeBar();
                    _window.Repaint();
                }
                return;
            }

            _window.WasPlaying = true;

            UpdateTrackedComponent();

            if (_window.TrackedComponent != null)
            {
                int newActiveIndex = _window.TrackedComponent.CurrentStateIndex;

                if (newActiveIndex != _window.ActiveStateIndex)
                {
                    _window.ActiveStateIndex = newActiveIndex;

                    for (int i = 0; i < _window.States.Count; i++)
                        _window.States[i].IsActive = (_window.States[i].DataIndex == _window.ActiveStateIndex);

                    _window.IsAutoNavigating = true;
                    var activeState = _window.GraphOperations.GetStateByIndex(_window.ActiveStateIndex);
                    if (activeState != null)
                    {
                        var newStack = new List<int>();
                        _expandedView.FindActiveStateHierarchy(_window.ActiveStateIndex, newStack);

                        if (!ExpandedViewManager.AreListsEqual(_window.ExpandedSubStateStack, newStack))
                        {
                            if (newStack.Count > _window.ExpandedSubStateStack.Count &&
                                _window.PendingExpandStack == null)
                            {
                                _window.PendingExpandStack = new List<int>(newStack);
                                _window.PendingExpandTime = Time.realtimeSinceStartup;
                            }
                            else
                            {
                                _window.PendingExpandStack = null;
                                _window.ExpandedSubStateStack.Clear();
                                _window.ExpandedSubStateStack.AddRange(newStack);
                                _expandedView.UpdateExpandedModeBar();
                                if (!_window.IsAnimatingView)
                                    _window.StartSmoothFocusOnContentInternal();
                            }
                        }
                    }
                    _window.IsAutoNavigating = false;

                    _window.Repaint();
                }

                if (_window.PendingExpandStack != null)
                {
                    var checkStack = new List<int>();
                    _expandedView.FindActiveStateHierarchy(_window.ActiveStateIndex, checkStack);
                    if (!ExpandedViewManager.AreListsEqual(checkStack, _window.PendingExpandStack))
                    {
                        _window.PendingExpandStack = null;
                    }
                    else if (Time.realtimeSinceStartup - _window.PendingExpandTime >= CleanStateMachineWindow.AutoExpandDelay)
                    {
                        _window.ExpandedSubStateStack.Clear();
                        _window.ExpandedSubStateStack.AddRange(_window.PendingExpandStack);
                        _window.PendingExpandStack = null;
                        _expandedView.UpdateExpandedModeBar();
                        if (!_window.IsAnimatingView)
                            _window.StartSmoothFocusOnContentInternal();

                        PlayDeferredTransitionEffects();

                        _window.Repaint();
                    }
                }

                var transitions = _window.TrackedComponent.RecentTransitions;
                if (transitions.Count > 0)
                {
                    for (int t = 0; t < transitions.Count; t++)
                    {
                        var record = transitions[t];
                        _window.LastTransitionFromIndex = record.FromIndex;
                        _window.LastTransitionToIndex = record.ToIndex;
                        _window.LastTransitionConnectionIndex = record.ConnectionIndex;
                        for (int c = 0; c < _window.Connections.Count; c++)
                        {
                            if (record.ConnectionIndex >= 0)
                            {
                                if (_window.Connections[c].DataIndex == record.ConnectionIndex)
                                {
                                    _window.Connections[c].IsActive = true;
                                    _window.Connections[c].ActivationTime = Time.realtimeSinceStartup;
                                }
                            }
                            else
                            {
                                if (_window.Connections[c].From.DataIndex == record.FromIndex &&
                                    _window.Connections[c].To.DataIndex == record.ToIndex)
                                {
                                    _window.Connections[c].IsActive = true;
                                    _window.Connections[c].ActivationTime = Time.realtimeSinceStartup;
                                }
                            }
                        }
                    }
                    transitions.Clear();
                    _window.Repaint();
                }

                _window.Repaint();
            }
            else if (_window.ActiveStateIndex >= 0)
            {
                _window.ActiveStateIndex = -1;
                _window.PendingExpandStack = null;
                _window.LastTransitionFromIndex = -1;
                _window.LastTransitionToIndex = -1;
                _window.LastTransitionConnectionIndex = -1;
                for (int i = 0; i < _window.States.Count; i++)
                    _window.States[i].IsActive = false;
                for (int i = 0; i < _window.Connections.Count; i++)
                    _window.Connections[i].IsActive = false;
                _window.Repaint();
            }
        }

        public void OnPlayModeStateChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.ExitingPlayMode)
            {
                if (_window.Controller != null)
                {
                    _window.GraphSerializer.SaveCurrentData();
                    _window.Controller.Data = _window.Controller.Data;
                }
                _window.ExpandedSubStateStack.Clear();
                _window.PendingExpandStack = null;
                _window.LastTransitionFromIndex = -1;
                _window.LastTransitionToIndex = -1;
                _window.LastTransitionConnectionIndex = -1;
            }
            else if (change == PlayModeStateChange.EnteredEditMode)
            {
                _window.WasPlaying = false;
                if (_window.Controller != null)
                {
                    _window.CurrentData = _window.Controller.Data;
                    _window.GraphSerializer.LoadFromCurrentData();
                    _window.StartSmoothFocusOnContentInternal();
                }
                _window.Repaint();
            }
        }

        public void PlayDeferredTransitionEffects()
        {
            if (_window.ActiveStateIndex < 0) return;

            for (int c = 0; c < _window.Connections.Count; c++)
            {
                if (_window.Connections[c].To.DataIndex == _window.ActiveStateIndex)
                {
                    _window.Connections[c].IsActive = true;
                    _window.Connections[c].ActivationTime = Time.realtimeSinceStartup;
                    break;
                }
            }

            for (int i = 0; i < _window.States.Count; i++)
            {
                if (_window.States[i].DataIndex == _window.ActiveStateIndex)
                {
                    _window.States[i].ReactivateFlash();
                    break;
                }
            }
        }

        private void UpdateTrackedComponent()
        {
            if (!Application.isPlaying)
            {
                _window.TrackedComponent = null;
                return;
            }

            if (_window.Controller == null)
            {
                _window.TrackedComponent = null;
                return;
            }

            GameObject selected = Selection.activeGameObject;
            if (selected == null)
            {
                _window.TrackedComponent = null;
                return;
            }

            var component = selected.GetComponent<StateMachineComponent>();
            if (component != null && component.Controller == _window.Controller)
            {
                _window.TrackedComponent = component;
            }
            else
            {
                _window.TrackedComponent = null;
            }
        }
    }
}

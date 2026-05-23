using System;
using System.Collections.Generic;
using UnityEngine;

namespace CleanStateMachine
{
    public class ConnectionController
    {
        public bool IsConnecting { get; private set; }
        public StateView SourceNode { get; private set; }
        public Vector2 CurrentMouseGraphPos => _currentMouseGraphPos;

        private Vector2 _currentMouseGraphPos;

        public event Action<StateView, StateView> ConnectionCompleted;

        public void StartConnection(StateView source)
        {
            SourceNode = source;
            IsConnecting = true;
            _currentMouseGraphPos = source.GetCenter();
        }

        public void UpdatePending(Vector2 graphMousePos)
        {
            _currentMouseGraphPos = graphMousePos;
        }

        public bool TryComplete(Vector2 graphMousePos, IReadOnlyList<StateView> allStates)
        {
            if (!IsConnecting)
                return false;

            for (int i = allStates.Count - 1; i >= 0; i--)
            {
                StateView target = allStates[i];
                if (target != SourceNode && !target.IsEntry && target.ContainsPoint(graphMousePos))
                {
                    ConnectionCompleted?.Invoke(SourceNode, target);
                    Cancel();
                    return true;
                }
            }

            return false;
        }

        public void Cancel()
        {
            IsConnecting = false;
            SourceNode = null;
        }
    }
}

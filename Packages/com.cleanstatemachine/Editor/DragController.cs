using System.Collections.Generic;
using UnityEngine;

namespace CleanStateMachine
{
    public class DragController
    {
        public bool IsActive { get; private set; }
        public bool IsMoving { get; private set; }

        private Vector2 _dragStartGraphPos;
        private readonly Dictionary<ISelectable, Vector2> _startPositions = new();

        private const float DragThreshold = 4f;

        public void StartDrag(Vector2 graphPos, IEnumerable<ISelectable> selectedItems)
        {
            IsActive = true;
            IsMoving = false;
            _dragStartGraphPos = graphPos;

            _startPositions.Clear();
            foreach (var item in selectedItems)
            {
                _startPositions[item] = item.Position;
            }
        }

        public void UpdateDrag(Vector2 graphPos, float zoom)
        {
            if (!IsActive)
                return;

            if (!IsMoving)
            {
                Vector2 screenDelta = (graphPos - _dragStartGraphPos) * zoom;
                if (screenDelta.magnitude < DragThreshold)
                    return;

                IsMoving = true;
            }

            Vector2 totalDelta = graphPos - _dragStartGraphPos;
            foreach (var kvp in _startPositions)
            {
                kvp.Key.Position = _startPositions[kvp.Key] + totalDelta;
            }
        }

        public void EndDrag()
        {
            IsActive = false;
            IsMoving = false;
            _startPositions.Clear();
        }
    }
}

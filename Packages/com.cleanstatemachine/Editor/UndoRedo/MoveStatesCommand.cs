using System;
using System.Collections.Generic;
using UnityEngine;

namespace CleanStateMachine
{
    public class MoveStatesCommand : IUndoableCommand
    {
        private readonly List<ISelectable> _items;
        private readonly List<Vector2> _startPositions;
        private readonly List<Vector2> _endPositions;

        public string Description
        {
            get
            {
                int stateCount = 0;
                for (int i = 0; i < _items.Count; i++)
                    if (_items[i] is StateView)
                        stateCount++;
                return $"Move {stateCount} State{(stateCount != 1 ? "s" : "")}";
            }
        }

        public MoveStatesCommand(
            List<ISelectable> items,
            List<Vector2> startPositions,
            List<Vector2> endPositions)
        {
            if (items == null) throw new ArgumentNullException(nameof(items));
            if (startPositions == null) throw new ArgumentNullException(nameof(startPositions));
            if (endPositions == null) throw new ArgumentNullException(nameof(endPositions));
            if (items.Count != startPositions.Count || items.Count != endPositions.Count)
                throw new ArgumentException("items, startPositions, and endPositions must have the same count.");

            _items = items;
            _startPositions = startPositions;
            _endPositions = endPositions;
        }

        public void Execute()
        {
            ApplyPositions(_endPositions);
        }

        public void Undo()
        {
            ApplyPositions(_startPositions);
        }

        public void Redo()
        {
            ApplyPositions(_endPositions);
        }

        private void ApplyPositions(List<Vector2> positions)
        {
            for (int i = 0; i < _items.Count; i++)
                _items[i].Position = positions[i];
        }
    }
}

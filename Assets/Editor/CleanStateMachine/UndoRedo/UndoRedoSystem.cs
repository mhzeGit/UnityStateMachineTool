using System;
using System.Collections.Generic;

namespace CleanStateMachine
{
    public class UndoRedoSystem
    {
        private readonly Stack<IUndoableCommand> _undoStack = new();
        private readonly Stack<IUndoableCommand> _redoStack = new();
        private int _maxHistory = 50;

        public int UndoCount => _undoStack.Count;
        public int RedoCount => _redoStack.Count;

        public int MaxHistory
        {
            get => _maxHistory;
            set => _maxHistory = Math.Max(1, value);
        }

        public event Action HistoryChanged;

        public void Execute(IUndoableCommand command)
        {
            command.Execute();
            _undoStack.Push(command);
            _redoStack.Clear();
            TrimStack();
            HistoryChanged?.Invoke();
        }

        public bool Undo()
        {
            if (_undoStack.Count == 0) return false;

            IUndoableCommand command = _undoStack.Pop();
            command.Undo();
            _redoStack.Push(command);
            HistoryChanged?.Invoke();
            return true;
        }

        public bool Redo()
        {
            if (_redoStack.Count == 0) return false;

            IUndoableCommand command = _redoStack.Pop();
            command.Redo();
            _undoStack.Push(command);
            TrimStack();
            HistoryChanged?.Invoke();
            return true;
        }

        public void Clear()
        {
            _undoStack.Clear();
            _redoStack.Clear();
            HistoryChanged?.Invoke();
        }

        public string GetUndoDescription()
        {
            return _undoStack.Count > 0 ? _undoStack.Peek().Description : null;
        }

        public string GetRedoDescription()
        {
            return _redoStack.Count > 0 ? _redoStack.Peek().Description : null;
        }

        private void TrimStack()
        {
            if (_undoStack.Count <= _maxHistory) return;

            var array = _undoStack.ToArray();
            _undoStack.Clear();
            int startIndex = array.Length - _maxHistory;
            for (int i = array.Length - 1; i >= startIndex; i--)
            {
                _undoStack.Push(array[i]);
            }
        }
    }
}

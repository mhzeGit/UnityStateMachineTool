using System;

namespace CleanStateMachine
{
    public class RenameStateCommand : IUndoableCommand
    {
        private readonly StateView _state;
        private readonly string _oldName;
        private readonly string _newName;

        public string Description => $"Rename state to \"{_newName}\"";

        public RenameStateCommand(StateView state, string oldName, string newName)
        {
            _state = state ?? throw new ArgumentNullException(nameof(state));
            _oldName = oldName ?? throw new ArgumentNullException(nameof(oldName));
            _newName = newName ?? throw new ArgumentNullException(nameof(newName));
        }

        public void Execute()
        {
            _state.Name = _newName;
        }

        public void Undo()
        {
            _state.Name = _oldName;
        }

        public void Redo()
        {
            _state.Name = _newName;
        }
    }
}

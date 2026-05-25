namespace CleanStateMachine
{
    internal class ToggleBreakpointCommand : IUndoableCommand
    {
        private readonly CleanStateMachineWindow _window;
        private readonly int _stateDataIndex;
        private readonly bool _adding;
        public string Description { get; }

        public ToggleBreakpointCommand(CleanStateMachineWindow window, int stateDataIndex, bool adding)
        {
            _window = window;
            _stateDataIndex = stateDataIndex;
            _adding = adding;
            Description = adding
                ? $"Add Breakpoint ({_stateDataIndex})"
                : $"Remove Breakpoint ({_stateDataIndex})";
        }

        public void Execute()
        {
            if (_adding)
                _window.BreakpointStateIndices.Add(_stateDataIndex);
            else
                _window.BreakpointStateIndices.Remove(_stateDataIndex);
            _window.SyncStateBreakpointVisuals();
        }

        public void Undo()
        {
            if (_adding)
                _window.BreakpointStateIndices.Remove(_stateDataIndex);
            else
                _window.BreakpointStateIndices.Add(_stateDataIndex);
            _window.SyncStateBreakpointVisuals();
        }

        public void Redo()
        {
            Execute();
        }
    }
}

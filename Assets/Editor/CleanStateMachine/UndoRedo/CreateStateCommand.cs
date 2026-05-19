using System;
using System.Collections.Generic;

namespace CleanStateMachine
{
    public class CreateStateCommand : IUndoableCommand
    {
        private readonly List<StateView> _states;
        private readonly StateView _state;

        public string Description => $"Create State '{_state.Name}'";

        public CreateStateCommand(List<StateView> states, StateView state)
        {
            _states = states ?? throw new ArgumentNullException(nameof(states));
            _state = state ?? throw new ArgumentNullException(nameof(state));
        }

        public void Execute()
        {
            if (!_states.Contains(_state))
                _states.Add(_state);
        }

        public void Undo()
        {
            _states.Remove(_state);
        }

        public void Redo()
        {
            Execute();
        }
    }
}

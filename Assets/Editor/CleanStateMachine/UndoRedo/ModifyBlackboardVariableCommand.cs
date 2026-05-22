using System;

namespace CleanStateMachine
{
    public class ModifyBlackboardVariableCommand : IUndoableCommand
    {
        private readonly BlackboardVariable _variable;
        private readonly string _oldValue;
        private readonly string _newValue;

        public string Description => "Modify Variable";

        public ModifyBlackboardVariableCommand(
            BlackboardVariable variable,
            string oldValue,
            string newValue)
        {
            _variable = variable ?? throw new ArgumentNullException(nameof(variable));
            _oldValue = oldValue;
            _newValue = newValue;
        }

        public void Execute()
        {
            _variable.StringValue = _newValue;
        }

        public void Undo()
        {
            _variable.StringValue = _oldValue;
        }

        public void Redo()
        {
            _variable.StringValue = _newValue;
        }
    }
}

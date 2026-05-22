using System;
using System.Collections.Generic;

namespace CleanStateMachine
{
    public class DeleteBlackboardVariableCommand : IUndoableCommand
    {
        private readonly List<BlackboardVariable> _variableList;
        private readonly BlackboardVariable _deletedVariable;
        private readonly int _deletedIndex;

        public string Description => "Delete Variable";

        public DeleteBlackboardVariableCommand(
            List<BlackboardVariable> variableList,
            int index)
        {
            _variableList = variableList ?? throw new ArgumentNullException(nameof(variableList));
            _deletedIndex = index;
            _deletedVariable = variableList[index].Clone();
        }

        public void Execute()
        {
            _variableList.RemoveAt(_deletedIndex);
        }

        public void Undo()
        {
            _variableList.Insert(_deletedIndex, _deletedVariable);
        }

        public void Redo()
        {
            Execute();
        }
    }
}

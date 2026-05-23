using System;

namespace CleanStateMachine
{
    public class RenameGroupCommand : IUndoableCommand
    {
        private readonly CommentGroupView _group;
        private readonly string _oldLabel;
        private readonly string _newLabel;

        public string Description => $"Rename Group '{_oldLabel}'";

        public RenameGroupCommand(CommentGroupView group, string oldLabel, string newLabel)
        {
            _group = group ?? throw new ArgumentNullException(nameof(group));
            _oldLabel = oldLabel ?? throw new ArgumentNullException(nameof(oldLabel));
            _newLabel = newLabel ?? throw new ArgumentNullException(nameof(newLabel));
        }

        public void Execute()
        {
            _group.Label = _newLabel;
        }

        public void Undo()
        {
            _group.Label = _oldLabel;
        }

        public void Redo()
        {
            Execute();
        }
    }
}

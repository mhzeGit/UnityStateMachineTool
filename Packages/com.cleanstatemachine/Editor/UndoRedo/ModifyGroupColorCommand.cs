using System;
using UnityEngine;

namespace CleanStateMachine
{
    public class ModifyGroupColorCommand : IUndoableCommand
    {
        private readonly CommentGroupView _group;
        private readonly Color _oldColor;
        private readonly Color _newColor;

        public string Description => "Change Group Color";

        public ModifyGroupColorCommand(CommentGroupView group, Color newColor)
        {
            _group = group ?? throw new ArgumentNullException(nameof(group));
            _oldColor = group.GroupColor;
            _newColor = newColor;
        }

        public void Execute()
        {
            _group.GroupColor = _newColor;
        }

        public void Undo()
        {
            _group.GroupColor = _oldColor;
        }

        public void Redo()
        {
            Execute();
        }
    }
}

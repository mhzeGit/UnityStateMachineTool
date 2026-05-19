using System;
using System.Collections.Generic;

namespace CleanStateMachine
{
    public class CreateGroupCommand : IUndoableCommand
    {
        private readonly List<CommentGroupView> _groups;
        private readonly CommentGroupView _group;

        public string Description => $"Create Group '{_group.Label}'";

        public CreateGroupCommand(List<CommentGroupView> groups, CommentGroupView group)
        {
            _groups = groups ?? throw new ArgumentNullException(nameof(groups));
            _group = group ?? throw new ArgumentNullException(nameof(group));
        }

        public void Execute()
        {
            if (!_groups.Contains(_group))
                _groups.Add(_group);
        }

        public void Undo()
        {
            _groups.Remove(_group);
        }

        public void Redo()
        {
            Execute();
        }
    }
}

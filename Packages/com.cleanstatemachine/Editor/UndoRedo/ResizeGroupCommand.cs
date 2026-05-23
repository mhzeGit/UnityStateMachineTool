using System;
using UnityEngine;

namespace CleanStateMachine
{
    public class ResizeGroupCommand : IUndoableCommand
    {
        private readonly CommentGroupView _group;
        private readonly Rect _oldRect;
        private readonly Rect _newRect;

        public string Description => $"Resize '{_group.Label}'";

        public ResizeGroupCommand(CommentGroupView group, Rect oldRect, Rect newRect)
        {
            _group = group ?? throw new ArgumentNullException(nameof(group));
            _oldRect = oldRect;
            _newRect = newRect;
        }

        public void Execute()
        {
            _group.SetRect(_newRect);
        }

        public void Undo()
        {
            _group.SetRect(_oldRect);
        }

        public void Redo()
        {
            Execute();
        }
    }
}

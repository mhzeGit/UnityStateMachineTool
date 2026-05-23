using System;
using System.Collections.Generic;

namespace CleanStateMachine
{
    public class DeleteStatesCommand : IUndoableCommand
    {
        private readonly List<StateView> _stateList;
        private readonly List<ConnectionView> _connectionList;
        private readonly List<CommentGroupView> _groupList;

        private readonly List<StateView> _deletedStates = new();
        private readonly List<ConnectionView> _deletedConnections = new();
        private readonly List<CommentGroupView> _deletedGroups = new();

        public string Description
        {
            get
            {
                int total = _deletedStates.Count + _deletedConnections.Count + _deletedGroups.Count;
                return $"Delete {total} Item{(total != 1 ? "s" : "")}";
            }
        }

        public DeleteStatesCommand(
            List<StateView> stateList,
            List<ConnectionView> connectionList,
            List<CommentGroupView> groupList,
            SelectionController selectionController)
        {
            _stateList = stateList ?? throw new ArgumentNullException(nameof(stateList));
            _connectionList = connectionList ?? throw new ArgumentNullException(nameof(connectionList));
            _groupList = groupList ?? throw new ArgumentNullException(nameof(groupList));

            if (selectionController == null)
                throw new ArgumentNullException(nameof(selectionController));

            CaptureSelection(selectionController);
        }

        private void CaptureSelection(SelectionController selectionController)
        {
            var selectedStates = new HashSet<StateView>();
            var selectedConnections = new HashSet<ConnectionView>();
            var selectedGroups = new HashSet<CommentGroupView>();

            for (int i = 0; i < selectionController.Count; i++)
            {
                if (selectionController.Selected[i] is StateView s)
                    selectedStates.Add(s);
                else if (selectionController.Selected[i] is ConnectionView c)
                    selectedConnections.Add(c);
                else if (selectionController.Selected[i] is CommentGroupView g)
                    selectedGroups.Add(g);
            }

            _deletedStates.AddRange(selectedStates);

            for (int i = 0; i < _connectionList.Count; i++)
            {
                var conn = _connectionList[i];
                if (selectedConnections.Contains(conn) ||
                    selectedStates.Contains(conn.From) ||
                    selectedStates.Contains(conn.To))
                    _deletedConnections.Add(conn);
            }

            _deletedGroups.AddRange(selectedGroups);
        }

        public void Execute()
        {
            for (int i = 0; i < _deletedConnections.Count; i++)
                _connectionList.Remove(_deletedConnections[i]);

            for (int i = 0; i < _deletedGroups.Count; i++)
                _groupList.Remove(_deletedGroups[i]);

            for (int i = 0; i < _deletedStates.Count; i++)
                _stateList.Remove(_deletedStates[i]);
        }

        public void Undo()
        {
            for (int i = 0; i < _deletedStates.Count; i++)
                if (!_stateList.Contains(_deletedStates[i]))
                    _stateList.Add(_deletedStates[i]);

            for (int i = 0; i < _deletedConnections.Count; i++)
                if (!_connectionList.Contains(_deletedConnections[i]))
                    _connectionList.Add(_deletedConnections[i]);

            for (int i = 0; i < _deletedGroups.Count; i++)
                if (!_groupList.Contains(_deletedGroups[i]))
                    _groupList.Add(_deletedGroups[i]);
        }

        public void Redo()
        {
            Execute();
        }
    }
}

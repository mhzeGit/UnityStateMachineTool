using System;
using System.Collections.Generic;

namespace CleanStateMachine
{
    public class DeleteConnectionCommand : IUndoableCommand
    {
        private readonly List<ConnectionView> _connections;
        private readonly ConnectionView _connection;

        public string Description => "Delete Connection";

        public DeleteConnectionCommand(List<ConnectionView> connections, ConnectionView connection)
        {
            _connections = connections ?? throw new ArgumentNullException(nameof(connections));
            _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        }

        public void Execute()
        {
            _connections.Remove(_connection);
        }

        public void Undo()
        {
            if (!_connections.Contains(_connection))
                _connections.Add(_connection);
        }

        public void Redo()
        {
            Execute();
        }
    }
}

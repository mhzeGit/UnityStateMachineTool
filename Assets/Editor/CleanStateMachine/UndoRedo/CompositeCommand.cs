using System;
using System.Collections.Generic;

namespace CleanStateMachine
{
    public class CompositeCommand : IUndoableCommand
    {
        private readonly List<IUndoableCommand> _commands = new();
        private readonly string _description;

        public string Description => _description;

        public CompositeCommand(string description)
        {
            _description = description ?? throw new ArgumentNullException(nameof(description));
        }

        public CompositeCommand(string description, IUndoableCommand[] commands) : this(description)
        {
            if (commands == null) throw new ArgumentNullException(nameof(commands));
            _commands.AddRange(commands);
        }

        public void Add(IUndoableCommand command)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));
            _commands.Add(command);
        }

        public void Execute()
        {
            for (int i = 0; i < _commands.Count; i++)
                _commands[i].Execute();
        }

        public void Undo()
        {
            for (int i = _commands.Count - 1; i >= 0; i--)
                _commands[i].Undo();
        }

        public void Redo()
        {
            for (int i = 0; i < _commands.Count; i++)
                _commands[i].Redo();
        }
    }
}

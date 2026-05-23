namespace CleanStateMachine
{
    public interface IUndoableCommand
    {
        void Execute();
        void Undo();
        void Redo();
        string Description { get; }
    }
}

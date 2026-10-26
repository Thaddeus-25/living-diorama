namespace LivingDiorama.SceneEditing.Commands
{
    /// <summary>
    /// An undoable editor action. Commands refer to objects by id (never by reference), so they
    /// keep working after the object has been deleted and recreated by undo/redo.
    /// </summary>
    public interface ICommand
    {
        string Description { get; }
        void Execute();
        void Undo();
    }
}

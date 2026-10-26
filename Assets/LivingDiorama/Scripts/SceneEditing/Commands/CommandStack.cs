using System;
using System.Collections.Generic;

namespace LivingDiorama.SceneEditing.Commands
{
    /// <summary>Undo/redo history with a size limit.</summary>
    public sealed class CommandStack
    {
        private readonly List<ICommand> undoHistory = new();
        private readonly Stack<ICommand> redoHistory = new();
        private readonly int capacity;

        public CommandStack(int capacity = 200)
        {
            this.capacity = Math.Max(1, capacity);
        }

        /// <summary>Raised after anything is executed, recorded, undone, redone or cleared.</summary>
        public event Action Changed;

        public bool CanUndo => undoHistory.Count > 0;
        public bool CanRedo => redoHistory.Count > 0;
        public int UndoCount => undoHistory.Count;
        public string NextUndoDescription => CanUndo ? undoHistory[^1].Description : null;
        public string NextRedoDescription => CanRedo ? redoHistory.Peek().Description : null;

        /// <summary>Runs the command and adds it to the history.</summary>
        public void Execute(ICommand command)
        {
            command.Execute();
            Record(command);
        }

        /// <summary>Adds a command whose effect has already been applied (e.g. a finished drag).</summary>
        public void Record(ICommand command)
        {
            undoHistory.Add(command);
            if (undoHistory.Count > capacity) undoHistory.RemoveAt(0);
            redoHistory.Clear();
            Changed?.Invoke();
        }

        public bool Undo()
        {
            if (!CanUndo) return false;
            var command = undoHistory[^1];
            undoHistory.RemoveAt(undoHistory.Count - 1);
            command.Undo();
            redoHistory.Push(command);
            Changed?.Invoke();
            return true;
        }

        public bool Redo()
        {
            if (!CanRedo) return false;
            var command = redoHistory.Pop();
            command.Execute();
            undoHistory.Add(command);
            Changed?.Invoke();
            return true;
        }

        public void Clear()
        {
            undoHistory.Clear();
            redoHistory.Clear();
            Changed?.Invoke();
        }
    }
}

using System.Collections.Generic;
using LivingDiorama.SceneEditing;
using LivingDiorama.SceneEditing.Commands;
using NUnit.Framework;
using UnityEngine;

namespace LivingDiorama.Tests
{
    public class CommandStackTests
    {
        private sealed class AppendCommand : ICommand
        {
            private readonly List<string> log;
            private readonly string value;

            public AppendCommand(List<string> log, string value)
            {
                this.log = log;
                this.value = value;
            }

            public string Description => value;
            public void Execute() => log.Add(value);
            public void Undo() => log.RemoveAt(log.Count - 1);
        }

        [Test]
        public void UndoRedo_RestoresStateInOrder()
        {
            var log = new List<string>();
            var stack = new CommandStack();
            stack.Execute(new AppendCommand(log, "a"));
            stack.Execute(new AppendCommand(log, "b"));

            Assert.IsTrue(stack.Undo());
            CollectionAssert.AreEqual(new[] { "a" }, log);
            Assert.AreEqual("b", stack.NextRedoDescription);

            Assert.IsTrue(stack.Redo());
            CollectionAssert.AreEqual(new[] { "a", "b" }, log);
            Assert.IsFalse(stack.CanRedo);
        }

        [Test]
        public void NewCommand_ClearsRedo()
        {
            var log = new List<string>();
            var stack = new CommandStack();
            stack.Execute(new AppendCommand(log, "a"));
            stack.Undo();
            stack.Execute(new AppendCommand(log, "c"));
            Assert.IsFalse(stack.CanRedo);
            CollectionAssert.AreEqual(new[] { "c" }, log);
        }

        [Test]
        public void Record_DoesNotExecuteButCanBeUndone()
        {
            var log = new List<string> { "already applied" };
            var stack = new CommandStack();
            stack.Record(new AppendCommand(log, "already applied"));
            Assert.AreEqual(1, log.Count);
            stack.Undo();
            Assert.AreEqual(0, log.Count);
        }

        [Test]
        public void Capacity_DropsOldestCommands()
        {
            var log = new List<string>();
            var stack = new CommandStack(capacity: 2);
            stack.Execute(new AppendCommand(log, "a"));
            stack.Execute(new AppendCommand(log, "b"));
            stack.Execute(new AppendCommand(log, "c"));
            Assert.AreEqual(2, stack.UndoCount);
        }

        [Test]
        public void EmptyStack_UndoAndRedoReturnFalse()
        {
            var stack = new CommandStack();
            Assert.IsFalse(stack.Undo());
            Assert.IsFalse(stack.Redo());
        }

        [TestCase(0.26f, 0.5f)]
        [TestCase(0.24f, 0f)]
        [TestCase(-1.3f, -1.5f)]
        public void GridSnap_RoundsXAndZ(float input, float expected)
        {
            var snapped = GridSnap.Snap(new Vector3(input, 3f, input), 0.5f);
            Assert.AreEqual(expected, snapped.x, 1e-5f);
            Assert.AreEqual(expected, snapped.z, 1e-5f);
            Assert.AreEqual(3f, snapped.y, "Y must not be snapped");
        }
    }
}

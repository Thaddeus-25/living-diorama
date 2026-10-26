using LivingDiorama.Data;
using LivingDiorama.EditorTools;
using LivingDiorama.Persistence;
using LivingDiorama.SceneEditing.Commands;
using LivingDiorama.World;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace LivingDiorama.Tests
{
    public class DioramaWorldTests
    {
        private GameObject root;
        private DioramaWorld world;

        [SetUp]
        public void CreateWorld()
        {
            var database = AssetDatabase.LoadAssetAtPath<ObjectDatabase>(ProjectBootstrap.DatabasePath);
            root = new GameObject("TestWorld");
            world = root.AddComponent<DioramaWorld>();
            world.Initialize(database, new Vector2(20f, 12f));
        }

        [TearDown]
        public void DestroyWorld() => Object.DestroyImmediate(root);

        private static PlacedObjectData Rock(string id = "rock-1") => new()
        {
            id = id,
            definitionId = "rock",
            position = new Vector3(1f, 0f, 2f),
            yaw = 90f,
            scale = 1.5f,
            flipX = true
        };

        [Test]
        public void SpawnThenCapture_RoundTripsAllFields()
        {
            var placed = world.Spawn(Rock());
            Assert.IsNotNull(placed);
            Assert.AreSame(placed, world.Find("rock-1"));
            Assert.IsTrue(world.Capture(placed).ApproximatelyEquals(Rock()));
        }

        [Test]
        public void Spawn_UnknownDefinition_ReturnsNullWithWarning()
        {
            LogAssert.Expect(LogType.Warning, "[DioramaWorld] Unknown object type 'dragon', skipped.");
            var data = Rock();
            data.definitionId = "dragon";
            Assert.IsNull(world.Spawn(data));
            Assert.AreEqual(0, world.Objects.Count);
        }

        [Test]
        public void Spawn_DuplicateId_GetsFreshId()
        {
            world.Spawn(Rock());
            var second = world.Spawn(Rock());
            Assert.AreNotEqual("rock-1", second.InstanceId);
            Assert.AreEqual(2, world.Objects.Count);
        }

        [Test]
        public void Positions_AreClampedInsideTheGarden()
        {
            var data = Rock();
            data.position = new Vector3(50f, 3f, -50f);
            var placed = world.Spawn(data);
            var position = placed.transform.position;
            Assert.LessOrEqual(position.x, 10f);
            Assert.GreaterOrEqual(position.z, -6f);
            Assert.AreEqual(0f, position.y, "Objects stand on the ground");
        }

        [Test]
        public void Facing_MirrorsSpriteWhenTurnedAway()
        {
            var data = Rock();
            data.flipX = false;
            data.yaw = 180f;
            var placed = world.Spawn(data);
            Assert.IsTrue(placed.Visual.FlipX);
            data.yaw = 0f;
            world.Apply(data);
            Assert.IsFalse(placed.Visual.FlipX);
        }

        [Test]
        public void PlaceCommand_UndoRemoves_RedoRestoresSameId()
        {
            var stack = new CommandStack();
            stack.Execute(new PlaceObjectCommand(world, Rock()));
            Assert.IsNotNull(world.Find("rock-1"));

            stack.Undo();
            Assert.IsNull(world.Find("rock-1"));

            stack.Redo();
            Assert.IsNotNull(world.Find("rock-1"));
        }

        [Test]
        public void DeleteCommand_UndoRestoresState()
        {
            var stack = new CommandStack();
            var placed = world.Spawn(Rock());
            stack.Execute(new DeleteObjectCommand(world, world.Capture(placed)));
            Assert.IsNull(world.Find("rock-1"));

            stack.Undo();
            var restored = world.Find("rock-1");
            Assert.IsNotNull(restored);
            Assert.IsTrue(world.Capture(restored).ApproximatelyEquals(Rock()));
        }

        [Test]
        public void ModifyCommand_UndoRedoSwitchesBetweenSnapshots()
        {
            var stack = new CommandStack();
            var placed = world.Spawn(Rock());
            var before = world.Capture(placed);
            var after = before.Clone();
            after.position = new Vector3(-3f, 0f, -1f);
            after.scale = 0.75f;

            stack.Execute(new ModifyObjectCommand(world, before, after));
            Assert.IsTrue(world.Capture(placed).ApproximatelyEquals(after));

            stack.Undo();
            Assert.IsTrue(world.Capture(placed).ApproximatelyEquals(before));

            stack.Redo();
            Assert.IsTrue(world.Capture(placed).ApproximatelyEquals(after));
        }

        [Test]
        public void Clear_RemovesEverything()
        {
            world.Spawn(Rock("a"));
            world.Spawn(Rock("b"));
            world.Clear();
            Assert.AreEqual(0, world.Objects.Count);
            Assert.AreEqual(0, root.transform.childCount);
        }
    }
}

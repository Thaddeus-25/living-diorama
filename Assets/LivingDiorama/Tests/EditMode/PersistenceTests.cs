using System;
using System.IO;
using LivingDiorama.Data;
using LivingDiorama.EditorTools;
using LivingDiorama.Persistence;
using LivingDiorama.World;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace LivingDiorama.Tests
{
    public class SceneSerializerTests
    {
        private static SceneSaveData Sample() => new()
        {
            sceneName = "Sunny garden",
            savedAtUtc = "2026-10-26T10:00:00.0000000Z",
            environment = new EnvironmentData { cameraZoom = 5.5f },
            objects =
            {
                new PlacedObjectData { id = "a", definitionId = "tree", position = new Vector3(1f, 0f, 2f), yaw = 45f, scale = 1.2f },
                new PlacedObjectData { id = "b", definitionId = "cat", position = new Vector3(-3f, 0f, -1f), scale = 1f, flipX = true },
            }
        };

        [Test]
        public void RoundTrip_PreservesEverything()
        {
            var json = SceneSerializer.ToJson(Sample());
            var result = SceneSerializer.FromJson(json);

            Assert.IsTrue(result.Success, result.Error);
            Assert.IsEmpty(result.Warnings);
            Assert.AreEqual(SceneSerializer.CurrentVersion, result.Data.version);
            Assert.AreEqual("Sunny garden", result.Data.sceneName);
            Assert.AreEqual(5.5f, result.Data.environment.cameraZoom);
            Assert.AreEqual(EnvironmentData.DefaultEnvironmentId, result.Data.environment.environmentId);
            Assert.AreEqual(2, result.Data.objects.Count);
            Assert.IsTrue(result.Data.objects[0].ApproximatelyEquals(Sample().objects[0]));
            Assert.IsTrue(result.Data.objects[1].ApproximatelyEquals(Sample().objects[1]));
        }

        [Test]
        public void Json_IsReadableAndVersioned()
        {
            var json = SceneSerializer.ToJson(Sample());
            StringAssert.Contains($"\"version\": {SceneSerializer.CurrentVersion}", json);
            StringAssert.Contains("\"definitionId\": \"tree\"", json);
        }

        [TestCase("")]
        [TestCase("   ")]
        [TestCase("this is not json")]
        [TestCase("{ \"version\": 1, \"objects\": [ ")]
        [TestCase("{}")]
        [TestCase("{ \"sceneName\": \"no version\" }")]
        public void BadFiles_FailWithAMessageInsteadOfThrowing(string json)
        {
            var result = SceneSerializer.FromJson(json);
            Assert.IsFalse(result.Success);
            Assert.IsNotEmpty(result.Error);
        }

        [Test]
        public void Version1File_IsUpgradedToMorningWithTimeRunning()
        {
            var result = SceneSerializer.FromJson("{ \"version\": 1, \"environment\": { \"cameraZoom\": 5 }, \"objects\": [] }");
            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual(SceneSerializer.CurrentVersion, result.Data.version);
            Assert.AreEqual(EnvironmentData.DefaultTimeOfDay, result.Data.environment.timeOfDay);
            Assert.IsTrue(result.Data.environment.timeAdvances);
            Assert.AreEqual(5f, result.Data.environment.cameraZoom, "Existing settings survive the upgrade");
        }

        [Test]
        public void TimeOfDay_RoundTrips()
        {
            var data = Sample();
            data.environment.timeOfDay = 22.5f;
            data.environment.timeAdvances = false;
            var result = SceneSerializer.FromJson(SceneSerializer.ToJson(data));
            Assert.AreEqual(22.5f, result.Data.environment.timeOfDay);
            Assert.IsFalse(result.Data.environment.timeAdvances);
        }

        [Test]
        public void NewerFormat_IsRejectedWithExplanation()
        {
            var result = SceneSerializer.FromJson("{ \"version\": 99, \"objects\": [] }");
            Assert.IsFalse(result.Success);
            StringAssert.Contains("newer version", result.Error);
        }

        [Test]
        public void BrokenObjects_AreSkippedWithWarnings_RestIsKept()
        {
            const string json = @"{
                ""version"": 1,
                ""objects"": [
                    { ""id"": ""ok"", ""definitionId"": ""rock"", ""position"": { ""x"": 1, ""y"": 0, ""z"": 1 }, ""scale"": 1 },
                    { ""id"": ""no-type"", ""definitionId"": """", ""scale"": 1 },
                    { ""id"": ""zero-size"", ""definitionId"": ""tree"", ""scale"": 0 }
                ]
            }";
            var result = SceneSerializer.FromJson(json);
            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual(1, result.Data.objects.Count);
            Assert.AreEqual("ok", result.Data.objects[0].id);
            Assert.AreEqual(2, result.Warnings.Count);
        }

        [Test]
        public void MissingSections_GetDefaults()
        {
            var result = SceneSerializer.FromJson("{ \"version\": 1 }");
            Assert.IsTrue(result.Success, result.Error);
            Assert.IsNotNull(result.Data.environment);
            Assert.IsNotNull(result.Data.objects);
        }
    }

    public class SaveFileStoreTests
    {
        private string directory;
        private SaveFileStore store;

        [SetUp]
        public void CreateTempFolder()
        {
            directory = Path.Combine(Path.GetTempPath(), "LivingDioramaTests", Guid.NewGuid().ToString("N"));
            store = new SaveFileStore(directory);
        }

        [TearDown]
        public void DeleteTempFolder()
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }

        [TestCase("My Garden", "My Garden")]
        [TestCase("  spaced  ", "spaced")]
        [TestCase("a/b:c?d", "a_b_c_d")]
        [TestCase("", "Untitled")]
        [TestCase("...", "Untitled")]
        [TestCase("CON", "_CON")]
        public void ToFileName_MakesSafeNames(string input, string expected)
        {
            Assert.AreEqual(expected, SaveFileStore.ToFileName(input));
            Assert.AreEqual(expected, SaveFileStore.ToFileName(expected), "Must be stable when applied twice");
        }

        [Test]
        public void WriteReadExists_AndOverwriteLeavesNoTempFile()
        {
            Assert.IsFalse(store.Exists("Garden"));
            store.Write("Garden", "first");
            store.Write("Garden", "second");
            Assert.IsTrue(store.Exists("Garden"));
            Assert.AreEqual("second", store.Read("Garden"));
            Assert.AreEqual(1, Directory.GetFiles(directory).Length, "Only the scene file should remain");
        }

        [Test]
        public void List_IsNewestFirst_AndEmptyWhenFolderMissing()
        {
            CollectionAssert.IsEmpty(store.List());
            store.Write("Older", "{}");
            store.Write("Newer", "{}");
            File.SetLastWriteTimeUtc(store.PathFor("Older"), DateTime.UtcNow.AddHours(-1));

            var files = store.List();
            Assert.AreEqual(2, files.Count);
            Assert.AreEqual("Newer", files[0].Name);
            Assert.AreEqual("Older", files[1].Name);
        }
    }

    /// <summary>Save, clear and load through the real definitions, without a scene.</summary>
    public class SceneSaveServiceTests
    {
        private GameObject root;
        private DioramaWorld world;
        private SceneSaveService service;
        private string directory;

        [SetUp]
        public void Setup()
        {
            var database = AssetDatabase.LoadAssetAtPath<ObjectDatabase>(ProjectBootstrap.DatabasePath);
            root = new GameObject("TestWorld");
            world = root.AddComponent<DioramaWorld>();
            world.Initialize(database, new Vector2(20f, 12f));
            service = root.AddComponent<SceneSaveService>();
            service.Initialize(world, null, null);
            directory = Path.Combine(Path.GetTempPath(), "LivingDioramaTests", Guid.NewGuid().ToString("N"));
            service.UseStore(new SaveFileStore(directory));

            world.Spawn(new PlacedObjectData { id = "t1", definitionId = "tree", position = new Vector3(-2f, 0f, 3f), scale = 1.3f });
            world.Spawn(new PlacedObjectData { id = "b1", definitionId = "bench", position = new Vector3(1f, 0f, 1f), yaw = 180f, scale = 1f });
            world.Spawn(new PlacedObjectData { id = "c1", definitionId = "cat", position = new Vector3(4f, 0f, -2f), scale = 1f, flipX = true });
        }

        [TearDown]
        public void Teardown()
        {
            Object.DestroyImmediate(root);
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }

        [Test]
        public void SaveNewLoad_RestoresSameObjects()
        {
            var before = world.Objects;
            var snapshots = new System.Collections.Generic.List<PlacedObjectData>();
            foreach (var placed in before) snapshots.Add(world.Capture(placed));

            Assert.IsNull(service.Save("Round Trip"));
            Assert.IsTrue(service.HasFile);
            Assert.IsFalse(service.IsDirty);

            service.NewScene();
            Assert.AreEqual(0, world.Objects.Count);
            Assert.AreEqual(SceneSaveService.UntitledSceneName, service.CurrentName);

            var result = service.Load("Round Trip");
            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual("Round Trip", service.CurrentName);
            Assert.AreEqual(snapshots.Count, world.Objects.Count);
            foreach (var snapshot in snapshots)
            {
                var restored = world.Find(snapshot.id);
                Assert.IsNotNull(restored, $"{snapshot.id} missing after load");
                Assert.IsTrue(world.Capture(restored).ApproximatelyEquals(snapshot), $"{snapshot.id} changed");
            }
        }

        [Test]
        public void CorruptFile_LeavesSceneUntouched()
        {
            File.WriteAllText(service.Store.PathFor("Broken").EnsureFolder(), "{ definitely not json");
            var result = service.Load("Broken");
            Assert.IsFalse(result.Success);
            Assert.AreEqual(3, world.Objects.Count, "A failed load must not clear the scene");
        }

        [Test]
        public void MissingFile_FailsGracefully()
        {
            var result = service.Load("Does not exist");
            Assert.IsFalse(result.Success);
            StringAssert.Contains("no longer exists", result.Error);
        }

        [Test]
        public void UnknownObjectType_IsSkippedAndReported()
        {
            service.Save("With Dragon");
            string path = service.Store.PathFor("With Dragon");
            File.WriteAllText(path, File.ReadAllText(path).Replace("\"definitionId\": \"cat\"", "\"definitionId\": \"dragon\""));

            LogAssert.Expect(LogType.Warning, "[DioramaWorld] Unknown object type 'dragon', skipped.");
            var result = service.Load("With Dragon");
            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual(2, world.Objects.Count);
            Assert.AreEqual(1, result.Warnings.Count);
            StringAssert.Contains("dragon", result.Warnings[0]);
        }
    }

    internal static class PathTestExtensions
    {
        public static string EnsureFolder(this string filePath)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
            return filePath;
        }
    }
}

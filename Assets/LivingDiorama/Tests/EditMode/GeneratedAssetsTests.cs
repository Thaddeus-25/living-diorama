using LivingDiorama.Data;
using LivingDiorama.EditorTools;
using LivingDiorama.World;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

namespace LivingDiorama.Tests
{
    /// <summary>Checks that the bootstrap produced a consistent set of assets.</summary>
    public class GeneratedAssetsTests
    {
        private ObjectDatabase database;

        [SetUp]
        public void LoadDatabase()
        {
            database = AssetDatabase.LoadAssetAtPath<ObjectDatabase>(ProjectBootstrap.DatabasePath);
            Assert.IsNotNull(database, "Run Living Diorama > Rebuild Generated Assets first.");
        }

        [TestCase("tree")]
        [TestCase("rock")]
        [TestCase("bench")]
        public void Definition_HasPrefabWithPlacedObjectAndVisual(string id)
        {
            var definition = database.Find(id);
            Assert.IsNotNull(definition, $"No definition '{id}'");
            Assert.IsNotNull(definition.Prefab, $"'{id}' has no prefab");
            Assert.IsNotNull(definition.Icon, $"'{id}' has no icon");

            var placed = definition.Prefab.GetComponent<PlacedObject>();
            Assert.IsNotNull(placed);
            Assert.AreSame(definition, placed.Definition);
            Assert.IsNotNull(placed.Visual);
            Assert.IsNotNull(placed.Visual.Sprite);
            Assert.AreEqual(definition.IsObstacle, definition.Prefab.GetComponent<NavMeshObstacle>() != null);
        }

        [Test]
        public void Find_UnknownId_ReturnsNull()
        {
            Assert.IsNull(database.Find("no_such_object"));
            Assert.IsNull(database.Find(null));
        }

        [Test]
        public void Sprites_UsePixelArtImportSettings()
        {
            var sprite = database.Find("tree").Icon;
            Assert.AreEqual(PlaceholderArtGenerator.PixelsPerUnit, sprite.pixelsPerUnit);
            Assert.AreEqual(FilterMode.Point, sprite.texture.filterMode);
            // Pivot at the bottom centre keeps feet on the ground.
            Assert.AreEqual(0f, sprite.bounds.min.y, 0.001f);
        }

        [Test]
        public void SpriteMeshCache_FacesCameraAndMatchesSpriteBounds()
        {
            var sprite = database.Find("rock").Icon;
            var mesh = SpriteMeshCache.Get(sprite);
            Assert.AreSame(mesh, SpriteMeshCache.Get(sprite), "Meshes should be cached per sprite");
            Assert.AreEqual(sprite.bounds.size.x, mesh.bounds.size.x, 0.001f);
            foreach (var normal in mesh.normals) Assert.AreEqual(Vector3.back, normal);
        }
    }
}

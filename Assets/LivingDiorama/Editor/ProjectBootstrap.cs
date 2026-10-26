using System.Collections.Generic;
using System.IO;
using LivingDiorama.Data;
using UnityEditor;
using UnityEngine;

namespace LivingDiorama.EditorTools
{
    /// <summary>
    /// Generates everything the project needs from code: placeholder art, materials, definitions,
    /// prefabs and the Garden scene. Safe to re-run:
    /// <list type="bullet">
    /// <item>PNGs are only created when missing, so your own art is never overwritten.</item>
    /// <item>Definitions are created when missing; existing ones keep your edits (only their prefab link is refreshed).</item>
    /// <item>Materials, prefabs and the scene are rebuilt in place (GUIDs are kept).</item>
    /// </list>
    /// Command line: Unity.exe -batchmode -quit -projectPath . -executeMethod LivingDiorama.EditorTools.ProjectBootstrap.Run
    /// </summary>
    public static class ProjectBootstrap
    {
        public const string Root = "Assets/LivingDiorama";
        public const string ArtFolder = Root + "/Art/Placeholder";
        public const string MaterialsFolder = Root + "/Materials";
        public const string DefinitionsFolder = Root + "/Definitions";
        public const string PrefabsFolder = Root + "/Prefabs";
        public const string ScenesFolder = Root + "/Scenes";
        public const string DatabasePath = DefinitionsFolder + "/ObjectDatabase.asset";

        /// <summary>Static props in the MVP. Add a row here (plus art) to add a new prop.</summary>
        private static readonly PropSpec[] Props =
        {
            new("tree", "Tree", ObjectCategory.Nature, "Tree", true, new Vector2(1.0f, 0.8f)),
            new("rock", "Rock", ObjectCategory.Nature, "Rock", true, new Vector2(1.4f, 0.9f)),
            new("bench", "Bench", ObjectCategory.Structures, "Bench", true, new Vector2(1.9f, 0.6f)),
        };

        [MenuItem("Living Diorama/Rebuild Generated Assets")]
        public static void Run()
        {
            EnsureFolders();

            var art = PlaceholderArtGenerator.GenerateAll(ArtFolder);
            var materials = CreateMaterials(art);

            var definitions = new List<ObjectDefinition>();
            foreach (var spec in Props)
            {
                var sprite = LoadSprite(art[spec.ArtName]);
                var definition = LoadOrCreateDefinition(spec, sprite);
                var prefab = PrefabBuilder.BuildProp(definition, sprite, materials.SpriteLit, $"{PrefabsFolder}/{spec.DisplayName}.prefab");
                definition.SetPrefab(prefab);
                EditorUtility.SetDirty(definition);
                definitions.Add(definition);
            }

            var database = LoadOrCreate<ObjectDatabase>(DatabasePath);
            database.SetDefinitions(definitions);
            EditorUtility.SetDirty(database);
            AssetDatabase.SaveAssets();

            // Opening a new scene unloads assets that only C# variables reference, so the scene
            // builder reloads what it needs from disk instead of reusing these objects.
            GardenSceneBuilder.Build(art);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Bootstrap] Living Diorama assets generated successfully.");
        }

        private static GeneratedMaterials CreateMaterials(Dictionary<string, string> art)
        {
            return new GeneratedMaterials
            {
                SpriteLit = MaterialFactory.CreateSpriteLit($"{MaterialsFolder}/M_SpriteLit.mat"),
                Backdrop = MaterialFactory.CreateBackdrop($"{MaterialsFolder}/M_Backdrop.mat"),
                Sky = MaterialFactory.CreateUnlitTextured($"{MaterialsFolder}/M_Sky.mat", LoadTexture(art["SkyGradient"])),
                Grass = MaterialFactory.CreateGround($"{MaterialsFolder}/M_Grass.mat", LoadTexture(art["GrassTile"]),
                    new Vector2(GardenSceneBuilder.GardenSize.x, GardenSceneBuilder.GardenSize.y)),
                Dirt = MaterialFactory.CreateGround($"{MaterialsFolder}/M_Dirt.mat", LoadTexture(art["DirtTile"]),
                    new Vector2(GardenSceneBuilder.GardenSize.x, GardenSceneBuilder.BaseDepth)),
            };
        }

        private static ObjectDefinition LoadOrCreateDefinition(PropSpec spec, Sprite sprite)
        {
            string path = $"{DefinitionsFolder}/{spec.DisplayName}.asset";
            var definition = AssetDatabase.LoadAssetAtPath<ObjectDefinition>(path);
            if (definition != null) return definition;

            definition = ScriptableObject.CreateInstance<ObjectDefinition>();
            definition.Configure(spec.Id, spec.DisplayName, spec.Category, sprite, spec.IsObstacle, spec.Footprint);
            AssetDatabase.CreateAsset(definition, path);
            return definition;
        }

        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        public static Sprite LoadSprite(string path)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null)
                throw new FileNotFoundException($"Sprite not found at {path}. Check that the PNG is imported as a Sprite.");
            return sprite;
        }

        private static Texture2D LoadTexture(string path)
        {
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (texture == null) throw new FileNotFoundException($"Texture not found at {path}.");
            return texture;
        }

        private static void EnsureFolders()
        {
            foreach (var folder in new[] { ArtFolder, MaterialsFolder, DefinitionsFolder, PrefabsFolder, ScenesFolder })
            {
                if (AssetDatabase.IsValidFolder(folder)) continue;
                string parent = Path.GetDirectoryName(folder)!.Replace('\\', '/');
                // Create parents first (e.g. Art before Art/Placeholder).
                if (!AssetDatabase.IsValidFolder(parent))
                    AssetDatabase.CreateFolder(Path.GetDirectoryName(parent)!.Replace('\\', '/'), Path.GetFileName(parent));
                AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
            }
        }

        private readonly struct PropSpec
        {
            public readonly string Id;
            public readonly string DisplayName;
            public readonly ObjectCategory Category;
            public readonly string ArtName;
            public readonly bool IsObstacle;
            public readonly Vector2 Footprint;

            public PropSpec(string id, string displayName, ObjectCategory category, string artName, bool isObstacle, Vector2 footprint)
            {
                Id = id;
                DisplayName = displayName;
                Category = category;
                ArtName = artName;
                IsObstacle = isObstacle;
                Footprint = footprint;
            }
        }
    }

    internal sealed class GeneratedMaterials
    {
        public Material SpriteLit;
        public Material Backdrop;
        public Material Sky;
        public Material Grass;
        public Material Dirt;

        public static GeneratedMaterials LoadFromDisk()
        {
            return new GeneratedMaterials
            {
                SpriteLit = Load("M_SpriteLit"),
                Backdrop = Load("M_Backdrop"),
                Sky = Load("M_Sky"),
                Grass = Load("M_Grass"),
                Dirt = Load("M_Dirt"),
            };
        }

        private static Material Load(string materialName)
        {
            string path = $"{ProjectBootstrap.MaterialsFolder}/{materialName}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) throw new FileNotFoundException($"Material not found at {path}.");
            return material;
        }
    }
}

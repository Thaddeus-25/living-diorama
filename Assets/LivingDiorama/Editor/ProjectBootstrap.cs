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
        public const string AnimationsFolder = DefinitionsFolder + "/Animations";
        public const string DatabasePath = DefinitionsFolder + "/ObjectDatabase.asset";

        /// <summary>Static props in the MVP. Add a row here (plus art) to add a new prop.</summary>
        private static readonly PropSpec[] Props =
        {
            new("tree", "Tree", ObjectCategory.Nature, "Tree", true, new Vector2(1.0f, 0.8f)),
            new("rock", "Rock", ObjectCategory.Nature, "Rock", true, new Vector2(1.4f, 0.9f)),
            new("bench", "Bench", ObjectCategory.Structures, "Bench", true, new Vector2(1.9f, 0.6f)),
        };

        /// <summary>
        /// Interaction points per prop id. Positions are local to the prop (x = along it, z = towards
        /// the back). Points sit just in front of the sprite (negative z) so the creature draws in front.
        /// The approach spot must be outside the prop's carved no-walk area.
        /// </summary>
        private static readonly Dictionary<string, InteractionPointSpec[]> PropInteractions = new()
        {
            ["bench"] = new[]
            {
                new InteractionPointSpec
                {
                    Name = "SeatLeft", Type = InteractionType.Sit, EntityIds = new[] { "human" }, Animation = "sit",
                    LocalPosition = new Vector3(-0.45f, 0f, -0.08f), ApproachOffset = new Vector3(0f, 0f, -0.7f)
                },
                new InteractionPointSpec
                {
                    Name = "SeatRight", Type = InteractionType.Sit, EntityIds = new[] { "human" }, Animation = "sit",
                    // The sit frame faces the camera, so neither seat mirrors the sprite.
                    LocalPosition = new Vector3(0.45f, 0f, -0.08f), ApproachOffset = new Vector3(0f, 0f, -0.7f)
                },
            },
            ["tree"] = new[]
            {
                new InteractionPointSpec
                {
                    Name = "NapSpot", Type = InteractionType.Sleep, EntityIds = new[] { "cat" }, Animation = "sleep",
                    LocalPosition = new Vector3(0.1f, 0f, -0.8f)
                },
            },
        };

        /// <summary>
        /// Living creatures. The behaviour tables are only used when a definition is first created;
        /// after that, tune them on the definition asset in the Inspector.
        /// </summary>
        private static readonly EntitySpec[] Entities =
        {
            new("human", "Human", CreatureArt.HumanClips, new Vector2(0.8f, 0.5f),
                moveSpeed: 1.1f, wanderRadius: 6f, agentRadius: 0.25f, new[]
                {
                    new BehaviourWeight(EntityBehaviour.Idle, 3f, new Vector2(2f, 5f)),
                    new BehaviourWeight(EntityBehaviour.Wander, 5f, new Vector2(8f, 15f)),
                    new BehaviourWeight(EntityBehaviour.LookAround, 2f, new Vector2(3f, 5f)),
                    BehaviourWeight.Interact(InteractionType.Sit, 3f, new Vector2(6f, 12f)),
                }),
            new("cat", "Cat", CreatureArt.CatClips, new Vector2(0.9f, 0.5f),
                moveSpeed: 1.5f, wanderRadius: 4f, agentRadius: 0.2f, new[]
                {
                    new BehaviourWeight(EntityBehaviour.Idle, 2f, new Vector2(2f, 4f)),
                    new BehaviourWeight(EntityBehaviour.Wander, 4f, new Vector2(6f, 12f)),
                    new BehaviourWeight(EntityBehaviour.Rest, 2f, new Vector2(4f, 8f), "sit"),
                    new BehaviourWeight(EntityBehaviour.Rest, 1f, new Vector2(10f, 20f), "sleep"),
                    new BehaviourWeight(EntityBehaviour.Rest, 1f, new Vector2(1.5f, 2.5f), "stretch"),
                    new BehaviourWeight(EntityBehaviour.LookAround, 1f, new Vector2(2f, 4f)),
                    BehaviourWeight.Interact(InteractionType.Sleep, 1.5f, new Vector2(10f, 20f)),
                }),
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
                PropInteractions.TryGetValue(spec.Id, out var interactionPoints);
                System.Action<GameObject, LivingDiorama.World.SpriteVisual> decorate = spec.Id == "tree"
                    ? (root, visual) => LeafEffectBuilder.AddFallingLeaves(root, visual, materials.Leaf)
                    : null;
                var prefab = PrefabBuilder.BuildProp(definition, sprite, materials.SpriteLit,
                    $"{PrefabsFolder}/{spec.DisplayName}.prefab", interactionPoints, decorate);
                definition.SetPrefab(prefab);
                EditorUtility.SetDirty(definition);
                definitions.Add(definition);
            }

            foreach (var spec in Entities)
            {
                var animations = BuildAnimationSet(spec, art);
                var definition = LoadOrCreateEntityDefinition(spec, animations);
                var firstFrame = animations.Find(SpriteAnimationSet.Idle).frames[0];
                var prefab = PrefabBuilder.BuildEntity(definition, firstFrame, materials.SpriteLit,
                    LoadSprite(art["ContactShadow"]), materials.ContactShadow, $"{PrefabsFolder}/{spec.DisplayName}.prefab");
                definition.SetPrefab(prefab);
                EditorUtility.SetDirty(definition);
                definitions.Add(definition);
            }

            var database = LoadOrCreate<ObjectDatabase>(DatabasePath);
            database.SetDefinitions(definitions);
            EditorUtility.SetDirty(database);
            AssetDatabase.SaveAssets();

            AtmosphereBuilder.InstallTiltShift();
            AtmosphereBuilder.CreateProfile();

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
                Leaf = MaterialFactory.CreateParticleLit($"{MaterialsFolder}/M_Leaf.mat", LoadTexture(art["Leaf"])),
                ContactShadow = MaterialFactory.CreateTransparentTint($"{MaterialsFolder}/M_ContactShadow.mat", new Color(0.06f, 0.1f, 0.04f, 0.6f)),
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

        private static SpriteAnimationSet BuildAnimationSet(EntitySpec spec, Dictionary<string, string> art)
        {
            var animationSet = LoadOrCreate<SpriteAnimationSet>($"{AnimationsFolder}/{spec.DisplayName}Animations.asset");
            var clips = new List<SpriteAnimationSet.Clip>();
            foreach (var clipSpec in spec.Clips)
            {
                var frames = new Sprite[clipSpec.Frames];
                for (int i = 0; i < frames.Length; i++)
                    frames[i] = LoadSprite(art[CreatureArt.FrameName(spec.DisplayName, clipSpec.Name, i)]);
                clips.Add(new SpriteAnimationSet.Clip { name = clipSpec.Name, frames = frames, framesPerSecond = clipSpec.FramesPerSecond });
            }
            animationSet.SetClips(clips);
            EditorUtility.SetDirty(animationSet);
            return animationSet;
        }

        private static EntityDefinition LoadOrCreateEntityDefinition(EntitySpec spec, SpriteAnimationSet animations)
        {
            string path = $"{DefinitionsFolder}/{spec.DisplayName}.asset";
            var definition = AssetDatabase.LoadAssetAtPath<EntityDefinition>(path);
            if (definition != null)
            {
                definition.SetAnimations(animations);
                // Roll out new default behaviours (e.g. Interact) without touching tuned entries.
                foreach (var entry in spec.Behaviours)
                    if (definition.AddBehaviourIfMissing(entry))
                        Debug.Log($"[Bootstrap] Added {entry.behaviour} {entry.interaction} to {definition.name}");
                return definition;
            }

            definition = ScriptableObject.CreateInstance<EntityDefinition>();
            var icon = animations.Find(SpriteAnimationSet.Idle).frames[0];
            definition.Configure(spec.Id, spec.DisplayName, ObjectCategory.LivingThings, icon,
                obstacle: false, spec.Footprint, rotation: false);
            definition.ConfigureEntity(spec.MoveSpeed, spec.WanderRadius, spec.AgentRadius, animations, spec.Behaviours);
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
            foreach (var folder in new[] { ArtFolder, MaterialsFolder, DefinitionsFolder, AnimationsFolder, PrefabsFolder, ScenesFolder })
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

    internal sealed class EntitySpec
    {
        public readonly string Id;
        public readonly string DisplayName;
        public readonly CreatureArt.ClipSpec[] Clips;
        public readonly Vector2 Footprint;
        public readonly float MoveSpeed;
        public readonly float WanderRadius;
        public readonly float AgentRadius;
        public readonly BehaviourWeight[] Behaviours;

        public EntitySpec(string id, string displayName, CreatureArt.ClipSpec[] clips, Vector2 footprint,
            float moveSpeed, float wanderRadius, float agentRadius, BehaviourWeight[] behaviours)
        {
            Id = id;
            DisplayName = displayName;
            Clips = clips;
            Footprint = footprint;
            MoveSpeed = moveSpeed;
            WanderRadius = wanderRadius;
            AgentRadius = agentRadius;
            Behaviours = behaviours;
        }
    }

    internal sealed class GeneratedMaterials
    {
        public Material SpriteLit;
        public Material Backdrop;
        public Material ContactShadow;
        public Material Leaf;
        public Material Sky;
        public Material Grass;
        public Material Dirt;

        public static GeneratedMaterials LoadFromDisk()
        {
            return new GeneratedMaterials
            {
                SpriteLit = Load("M_SpriteLit"),
                Backdrop = Load("M_Backdrop"),
                ContactShadow = Load("M_ContactShadow"),
                Leaf = Load("M_Leaf"),
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

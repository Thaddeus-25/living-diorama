using LivingDiorama.Data;
using LivingDiorama.Interactions;
using LivingDiorama.World;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;

namespace LivingDiorama.EditorTools
{
    /// <summary>
    /// Builds the standard prefab layout:
    /// <code>
    /// Root  (PlacedObject, NavMeshObstacle) - position, yaw, scale
    ///  └ Visual (SpriteVisual, Billboard, BoxCollider for picking)
    /// </code>
    /// </summary>
    internal static class PrefabBuilder
    {
        public static GameObject BuildProp(ObjectDefinition definition, Sprite sprite, Material material, string path,
            InteractionPointSpec[] interactionPoints = null, System.Action<GameObject, SpriteVisual> decorate = null)
        {
            var root = new GameObject(definition.DisplayName);
            var visual = CreateSpriteVisual(root.transform, sprite, material, castShadows: true);

            foreach (var spec in interactionPoints ?? System.Array.Empty<InteractionPointSpec>())
            {
                var pointObject = new GameObject(spec.Name);
                pointObject.transform.SetParent(root.transform, false);
                pointObject.transform.localPosition = spec.LocalPosition;
                pointObject.AddComponent<InteractionPoint>().Configure(spec.Type, spec.EntityIds, spec.Capacity,
                    spec.ApproachOffset, spec.Animation, spec.FaceLeft);
            }

            var placed = root.AddComponent<PlacedObject>();
            placed.Configure(definition, visual);

            if (definition.IsObstacle)
            {
                // Carving cuts a hole in the NavMesh wherever the object stands, so dragging it
                // around updates paths without rebaking.
                var obstacle = root.AddComponent<NavMeshObstacle>();
                obstacle.shape = NavMeshObstacleShape.Box;
                obstacle.size = new Vector3(definition.Footprint.x, 1f, definition.Footprint.y);
                obstacle.center = new Vector3(0f, 0.5f, 0f);
                obstacle.carving = true;
                obstacle.carveOnlyStationary = true;
            }

            decorate?.Invoke(root, visual);
            root.transform.localScale = Vector3.one * definition.DefaultScale;

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        /// <summary>
        /// Creature layout: like a prop, plus a NavMeshAgent (disabled until the simulation starts),
        /// a LivingEntity brain and a SpriteAnimator on the visual. Creatures are not NavMesh obstacles.
        /// </summary>
        public static GameObject BuildEntity(EntityDefinition definition, Sprite firstFrame, Material material,
            Sprite shadowSprite, Material shadowMaterial, string path)
        {
            var root = new GameObject(definition.DisplayName);
            var visual = CreateSpriteVisual(root.transform, firstFrame, material, castShadows: true);
            CreateContactShadow(root.transform, shadowSprite, shadowMaterial, definition.Footprint.x);
            var animator = visual.gameObject.AddComponent<SpriteAnimator>();
            animator.Configure(visual, definition.Animations);
            visual.gameObject.AddComponent<LivingDiorama.Entities.WalkBob>();

            var placed = root.AddComponent<PlacedObject>();
            placed.Configure(definition, visual);

            var agent = root.AddComponent<NavMeshAgent>();
            agent.radius = definition.AgentRadius;
            agent.height = 1.6f;
            agent.speed = definition.MoveSpeed;
            agent.updateRotation = false;
            agent.enabled = false; // LivingEntity enables it once it has been placed on the NavMesh

            var entity = root.AddComponent<LivingDiorama.Entities.LivingEntity>();
            entity.Configure(animator);

            root.transform.localScale = Vector3.one * definition.DefaultScale;

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        /// <summary>
        /// A soft dark oval lying on the ground under a creature's feet, so it looks grounded even
        /// when the sun's shadow falls behind it. Child of the root, so it follows automatically.
        /// </summary>
        private static void CreateContactShadow(Transform parent, Sprite sprite, Material material, float width)
        {
            var shadow = CreateSpriteVisual(parent, sprite, material, castShadows: false);
            shadow.name = "ContactShadow";
            shadow.Renderer.receiveShadows = false;
            Object.DestroyImmediate(shadow.GetComponent<Billboard>());
            Object.DestroyImmediate(shadow.GetComponent<BoxCollider>()); // not clickable
            shadow.transform.localPosition = new Vector3(0f, 0.015f, 0f); // just above the grass
            shadow.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); // lie flat
            // The sprite is 2 m wide; scale it to roughly the creature's width.
            shadow.transform.localScale = Vector3.one * (width * 1.3f / 2f);
        }

        /// <summary>
        /// Fish layout: a flat sprite lying on the water (turned to face the swimming direction),
        /// a FishSwimmer brain and an animator. No NavMeshAgent: fish steer themselves in a WaterArea.
        /// </summary>
        public static GameObject BuildFish(EntityDefinition definition, Sprite firstFrame, Material material, string path)
        {
            var root = new GameObject(definition.DisplayName);
            var visual = CreateSpriteVisual(root.transform, firstFrame, material, castShadows: false);
            Object.DestroyImmediate(visual.GetComponent<Billboard>());
            visual.transform.localPosition = new Vector3(0f, 0.03f, 0f); // just above the water surface
            visual.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            visual.Tint = new Color(0.85f, 0.92f, 1f); // a touch of blue, as if just under the surface
            var animator = visual.gameObject.AddComponent<SpriteAnimator>();
            animator.Configure(visual, definition.Animations);

            var placed = root.AddComponent<PlacedObject>();
            placed.Configure(definition, visual);
            root.AddComponent<LivingDiorama.Entities.FishSwimmer>().Configure(animator, visual.transform);

            root.transform.localScale = Vector3.one * definition.DefaultScale;
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        /// <summary>Turns a prop's sprite into a flat surface on the ground (ponds, rugs...).</summary>
        public static void LayFlat(SpriteVisual visual, float height = 0.01f)
        {
            Object.DestroyImmediate(visual.GetComponent<Billboard>());
            visual.transform.localPosition = new Vector3(0f, height, 0f);
            visual.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            visual.Renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        public static SpriteVisual CreateSpriteVisual(Transform parent, Sprite sprite, Material material, bool castShadows)
        {
            var visualObject = new GameObject("Visual", typeof(MeshFilter), typeof(MeshRenderer), typeof(BoxCollider));
            visualObject.transform.SetParent(parent, false);

            var meshRenderer = visualObject.GetComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = material;
            meshRenderer.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            meshRenderer.receiveShadows = true;

            var visual = visualObject.AddComponent<SpriteVisual>();
            visual.Sprite = sprite;
            visualObject.AddComponent<Billboard>();
            return visual;
        }
    }

    /// <summary>One interaction point to add to a prop prefab (see <see cref="InteractionPoint"/>).</summary>
    internal sealed class InteractionPointSpec
    {
        public string Name;
        public InteractionType Type;
        public Vector3 LocalPosition;
        public Vector3 ApproachOffset;
        public string[] EntityIds;
        public int Capacity = 1;
        public string Animation;
        public bool FaceLeft;
    }
}

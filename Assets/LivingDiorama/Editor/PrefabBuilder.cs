using LivingDiorama.Data;
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
        public static GameObject BuildProp(ObjectDefinition definition, Sprite sprite, Material material, string path)
        {
            var root = new GameObject(definition.DisplayName);
            var visual = CreateSpriteVisual(root.transform, sprite, material, castShadows: true);

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

            root.transform.localScale = Vector3.one * definition.DefaultScale;

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
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
}

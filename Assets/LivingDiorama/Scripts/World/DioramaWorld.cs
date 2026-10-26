using System;
using System.Collections.Generic;
using LivingDiorama.Data;
using LivingDiorama.Persistence;
using UnityEngine;

namespace LivingDiorama.World
{
    /// <summary>
    /// Owns every placed object in the diorama: creates, finds, changes and removes them by id,
    /// and converts between live objects and <see cref="PlacedObjectData"/> snapshots.
    /// The editor, undo commands and save/load all go through this class.
    /// </summary>
    public class DioramaWorld : MonoBehaviour
    {
        [SerializeField] private ObjectDatabase database;
        [Tooltip("Editable ground area in metres (x = width, y = depth), centred on the origin.")]
        [SerializeField] private Vector2 gardenSize = new(20f, 12f);
        [Tooltip("Objects are kept at least this far inside the garden edge.")]
        [SerializeField] private float edgeMargin = 0.4f;

        private readonly Dictionary<string, PlacedObject> objects = new();

        public ObjectDatabase Database => database;
        public Vector2 GardenSize => gardenSize;
        public IReadOnlyCollection<PlacedObject> Objects => objects.Values;

        /// <summary>Raised after an object is spawned, changed through <see cref="Apply"/>, or before it is removed.</summary>
        public event Action<PlacedObject> ObjectAdded;
        public event Action<PlacedObject> ObjectChanged;
        public event Action<PlacedObject> ObjectRemoving;

        private void Awake() => RebuildIndex();

        /// <summary>Sets up a world created from code (tests, tools).</summary>
        public void Initialize(ObjectDatabase objectDatabase, Vector2 size)
        {
            database = objectDatabase;
            gardenSize = size;
            RebuildIndex();
        }

        /// <summary>Indexes objects that are already children (e.g. placed in the scene file).</summary>
        public void RebuildIndex()
        {
            objects.Clear();
            foreach (var placed in GetComponentsInChildren<PlacedObject>(true))
            {
                if (string.IsNullOrEmpty(placed.InstanceId) || objects.ContainsKey(placed.InstanceId))
                    placed.AssignNewId();
                objects[placed.InstanceId] = placed;
            }
        }

        public PlacedObject Find(string id) =>
            id != null && objects.TryGetValue(id, out var placed) && placed != null ? placed : null;

        /// <summary>Creates an object from a snapshot. Returns null (with a warning) if its definition is unknown.</summary>
        public PlacedObject Spawn(PlacedObjectData data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));

            var definition = database != null ? database.Find(data.definitionId) : null;
            if (definition == null || definition.Prefab == null)
            {
                Debug.LogWarning($"[DioramaWorld] Unknown object type '{data.definitionId}', skipped.");
                return null;
            }

            if (string.IsNullOrEmpty(data.id) || objects.ContainsKey(data.id))
                data.id = PlacedObject.NewId();

            var instance = Instantiate(definition.Prefab, transform);
            instance.name = definition.DisplayName;
            var placed = instance.GetComponent<PlacedObject>();
            if (placed == null)
            {
                Debug.LogWarning($"[DioramaWorld] Prefab for '{definition.Id}' has no PlacedObject component.");
                DestroyObject(instance);
                return null;
            }

            placed.AssignId(data.id);
            objects[data.id] = placed;
            ApplyTo(placed, data);
            ObjectAdded?.Invoke(placed);
            return placed;
        }

        public bool Remove(string id)
        {
            var placed = Find(id);
            if (placed == null) return false;

            ObjectRemoving?.Invoke(placed);
            objects.Remove(id);
            // Deactivate first so the object stops being clickable immediately (Destroy runs at end of frame).
            placed.gameObject.SetActive(false);
            DestroyObject(placed.gameObject);
            return true;
        }

        public void Clear()
        {
            foreach (var id in new List<string>(objects.Keys)) Remove(id);
        }

        public PlacedObjectData Capture(PlacedObject placed)
        {
            return new PlacedObjectData
            {
                id = placed.InstanceId,
                definitionId = placed.Definition != null ? placed.Definition.Id : null,
                position = placed.transform.position,
                yaw = placed.Yaw,
                scale = placed.transform.localScale.x,
                flipX = placed.FlipX
            };
        }

        /// <summary>Updates the object with the snapshot's id to match the snapshot.</summary>
        public bool Apply(PlacedObjectData data)
        {
            var placed = Find(data.id);
            if (placed == null) return false;
            ApplyTo(placed, data);
            ObjectChanged?.Invoke(placed);
            return true;
        }

        /// <summary>Moves an object without creating a snapshot (used while dragging).</summary>
        public void Move(PlacedObject placed, Vector3 position)
        {
            placed.transform.position = ClampToGarden(position);
            ObjectChanged?.Invoke(placed);
        }

        public Vector3 ClampToGarden(Vector3 position)
        {
            float halfWidth = Mathf.Max(0f, gardenSize.x * 0.5f - edgeMargin);
            float halfDepth = Mathf.Max(0f, gardenSize.y * 0.5f - edgeMargin);
            return new Vector3(
                Mathf.Clamp(position.x, -halfWidth, halfWidth),
                0f,
                Mathf.Clamp(position.z, -halfDepth, halfDepth));
        }

        private void ApplyTo(PlacedObject placed, PlacedObjectData data)
        {
            placed.transform.position = ClampToGarden(data.position);
            placed.transform.localScale = Vector3.one * Mathf.Clamp(data.scale, 0.1f, 10f);
            placed.Yaw = placed.Definition != null && !placed.Definition.AllowRotation ? 0f : data.yaw;
            placed.FlipX = data.flipX;
        }

        private static void DestroyObject(GameObject target)
        {
            if (Application.isPlaying) Destroy(target);
            else DestroyImmediate(target);
        }
    }
}

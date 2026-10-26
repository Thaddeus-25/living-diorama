using UnityEngine;

namespace LivingDiorama.Data
{
    /// <summary>
    /// Describes one kind of placeable object (a tree, a bench, ...).
    /// Save files only store <see cref="Id"/>, so ids must stay stable once scenes have been saved.
    /// </summary>
    [CreateAssetMenu(menuName = "Living Diorama/Object Definition", fileName = "NewObjectDefinition")]
    public class ObjectDefinition : ScriptableObject
    {
        [Tooltip("Stable id written into save files. Never rename after scenes have been saved.")]
        [SerializeField] private string id;
        [SerializeField] private string displayName;
        [SerializeField] private ObjectCategory category = ObjectCategory.Nature;
        [SerializeField] private Sprite icon;
        [SerializeField] private GameObject prefab;

        [Header("Placement")]
        [SerializeField, Min(0.1f)] private float defaultScale = 1f;
        [SerializeField] private bool allowRotation = true;

        [Header("Navigation")]
        [Tooltip("Ground creatures walk around this object.")]
        [SerializeField] private bool isObstacle = true;
        [Tooltip("Blocked area on the ground in metres (x = width, y = depth).")]
        [SerializeField] private Vector2 footprint = Vector2.one;

        public string Id => id;
        public string DisplayName => string.IsNullOrEmpty(displayName) ? name : displayName;
        public ObjectCategory Category => category;
        public Sprite Icon => icon;
        public GameObject Prefab => prefab;
        public float DefaultScale => defaultScale;
        public bool AllowRotation => allowRotation;
        public bool IsObstacle => isObstacle;
        public Vector2 Footprint => footprint;

        /// <summary>Used by the editor bootstrap to fill in generated definitions.</summary>
        public void Configure(string newId, string newDisplayName, ObjectCategory newCategory,
            Sprite newIcon, bool obstacle, Vector2 newFootprint, bool rotation = true)
        {
            id = newId;
            displayName = newDisplayName;
            category = newCategory;
            icon = newIcon;
            isObstacle = obstacle;
            footprint = newFootprint;
            allowRotation = rotation;
        }

        public void SetPrefab(GameObject newPrefab) => prefab = newPrefab;

        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(id))
                id = name.ToLowerInvariant().Replace(' ', '_');
        }
    }
}

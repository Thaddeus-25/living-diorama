using System.Collections.Generic;
using UnityEngine;

namespace LivingDiorama.Data
{
    /// <summary>
    /// The single registry of everything that can be placed. To add a new asset,
    /// create its definition and add it to this list.
    /// </summary>
    [CreateAssetMenu(menuName = "Living Diorama/Object Database", fileName = "ObjectDatabase")]
    public class ObjectDatabase : ScriptableObject
    {
        [SerializeField] private List<ObjectDefinition> definitions = new();

        private Dictionary<string, ObjectDefinition> byId;

        public IReadOnlyList<ObjectDefinition> Definitions => definitions;

        /// <summary>Returns null (instead of throwing) for unknown ids so old saves load gracefully.</summary>
        public ObjectDefinition Find(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            if (byId == null) RebuildLookup();
            return byId.TryGetValue(id, out var definition) ? definition : null;
        }

        public void SetDefinitions(IEnumerable<ObjectDefinition> newDefinitions)
        {
            definitions = new List<ObjectDefinition>(newDefinitions);
            byId = null;
        }

        private void RebuildLookup()
        {
            byId = new Dictionary<string, ObjectDefinition>();
            foreach (var definition in definitions)
            {
                if (definition == null) continue;
                if (!byId.TryAdd(definition.Id, definition))
                    Debug.LogWarning($"[ObjectDatabase] Duplicate id '{definition.Id}' on {definition.name}; ignoring it.");
            }
        }

        private void OnValidate() => byId = null;
    }
}

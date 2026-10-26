using System;
using LivingDiorama.Data;
using UnityEngine;

namespace LivingDiorama.World
{
    /// <summary>
    /// Marks a scene object as user-placed. Links the instance to its definition and gives it a
    /// unique id, which is what save files and undo commands refer to.
    /// </summary>
    [DisallowMultipleComponent]
    public class PlacedObject : MonoBehaviour
    {
        [SerializeField] private string instanceId;
        [SerializeField] private ObjectDefinition definition;
        [SerializeField] private SpriteVisual visual;

        public string InstanceId => instanceId;
        public ObjectDefinition Definition => definition;
        public SpriteVisual Visual => visual;

        public void Configure(ObjectDefinition newDefinition, SpriteVisual newVisual)
        {
            definition = newDefinition;
            visual = newVisual;
        }

        /// <summary>Gives the object a fresh id (new placements and duplicates).</summary>
        public void AssignNewId() => instanceId = Guid.NewGuid().ToString("N");

        /// <summary>Restores a known id (loading a save, undoing a delete).</summary>
        public void AssignId(string id) => instanceId = id;

        private void Awake()
        {
            if (string.IsNullOrEmpty(instanceId)) AssignNewId();
        }
    }
}

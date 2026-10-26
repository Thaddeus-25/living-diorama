using System;
using System.Collections.Generic;
using LivingDiorama.Data;
using LivingDiorama.Entities;
using UnityEngine;

namespace LivingDiorama.Interactions
{
    /// <summary>
    /// A spot on an object where a creature can do something (sit on a bench seat, nap by a tree).
    /// Put it on a child transform of the prefab: the transform is where the creature ends up,
    /// <see cref="ApproachPosition"/> is where it walks to first. Moves and rotates with its object.
    /// </summary>
    public class InteractionPoint : MonoBehaviour
    {
        [SerializeField] private InteractionType type;
        [Tooltip("Entity definition ids that may use this point (e.g. \"human\"). Empty = any creature.")]
        [SerializeField] private string[] supportedEntityIds = Array.Empty<string>();
        [SerializeField, Min(1)] private int capacity = 1;
        [Tooltip("Where to walk to before stepping onto the point, relative to the point. Must be on walkable ground.")]
        [SerializeField] private Vector3 approachOffset;
        [Tooltip("Animation played while using the point.")]
        [SerializeField] private string animation = "sit";
        [Tooltip("Mirror the creature (face left) while using the point.")]
        [SerializeField] private bool faceLeft;

        private readonly List<LivingEntity> occupants = new();

        public InteractionType Type => type;
        public int Capacity => capacity;
        public IReadOnlyList<LivingEntity> Occupants => occupants;
        public string Animation => animation;
        public bool FaceLeft => faceLeft;
        public Vector3 Position => transform.position;
        public Vector3 ApproachPosition => transform.position + transform.rotation * approachOffset;

        /// <summary>Used by the editor bootstrap.</summary>
        public void Configure(InteractionType newType, string[] entityIds, int newCapacity, Vector3 newApproachOffset,
            string newAnimation, bool newFaceLeft)
        {
            type = newType;
            supportedEntityIds = entityIds ?? Array.Empty<string>();
            capacity = Mathf.Max(1, newCapacity);
            approachOffset = newApproachOffset;
            animation = newAnimation;
            faceLeft = newFaceLeft;
        }

        public bool Supports(string entityId)
        {
            if (supportedEntityIds == null || supportedEntityIds.Length == 0) return true;
            return Array.IndexOf(supportedEntityIds, entityId) >= 0;
        }

        public bool IsReservedBy(LivingEntity entity) => occupants.Contains(entity);

        public bool IsAvailableFor(LivingEntity entity)
        {
            if (!isActiveAndEnabled || entity == null) return false;
            if (IsReservedBy(entity)) return true;
            var definition = entity.Definition;
            return occupants.Count < capacity && Supports(definition != null ? definition.Id : null);
        }

        public bool TryReserve(LivingEntity entity)
        {
            if (IsReservedBy(entity)) return true;
            if (!IsAvailableFor(entity)) return false;
            occupants.Add(entity);
            return true;
        }

        public void Release(LivingEntity entity) => occupants.Remove(entity);

        private void OnEnable() => InteractionRegistry.Register(this);

        private void OnDisable()
        {
            // The object was deleted, replaced or deactivated: nobody can use this point any more.
            // Creatures notice through IsReservedBy() and give up on their own.
            InteractionRegistry.Unregister(this);
            occupants.Clear();
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(Position, 0.12f);
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(Position, ApproachPosition);
            Gizmos.DrawWireSphere(ApproachPosition, 0.08f);
        }
    }
}

using System;
using UnityEngine;

namespace LivingDiorama.Persistence
{
    /// <summary>
    /// Plain snapshot of one placed object. The same type is used by undo/redo commands and by
    /// save files, so anything that can be saved can also be undone (and vice versa).
    /// </summary>
    [Serializable]
    public class PlacedObjectData
    {
        public string id;
        public string definitionId;
        public Vector3 position;
        public float yaw;
        public float scale = 1f;
        public bool flipX;

        public PlacedObjectData Clone() => (PlacedObjectData)MemberwiseClone();

        /// <summary>True when both snapshots describe the same object in the same state (within float tolerance).</summary>
        public bool ApproximatelyEquals(PlacedObjectData other)
        {
            if (other == null) return false;
            return id == other.id
                   && definitionId == other.definitionId
                   && (position - other.position).sqrMagnitude < 1e-6f
                   && Mathf.Abs(Mathf.DeltaAngle(yaw, other.yaw)) < 0.01f
                   && Mathf.Abs(scale - other.scale) < 1e-4f
                   && flipX == other.flipX;
        }
    }
}

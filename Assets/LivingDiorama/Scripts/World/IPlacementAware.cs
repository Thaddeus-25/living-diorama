using UnityEngine;

namespace LivingDiorama.World
{
    /// <summary>
    /// Optional for components on a placed object that move on their own (creatures).
    /// <see cref="DioramaWorld"/> calls it when the editor, undo/redo or loading sets the object's
    /// placement, and asks it where the object should be recorded in snapshots and saves.
    /// </summary>
    public interface IPlacementAware
    {
        /// <summary>The object's transform was just set from outside; adopt it as the new position.</summary>
        void OnPlacementApplied();

        /// <summary>
        /// Where the object should be recorded. Usually its position, but e.g. a creature sitting
        /// inside a bench reports the walkable spot in front of it.
        /// </summary>
        Vector3 SavedPosition { get; }
    }
}

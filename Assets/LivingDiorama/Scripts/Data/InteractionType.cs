namespace LivingDiorama.Data
{
    /// <summary>
    /// Kinds of things a creature can do at an object. Add a value here, put an InteractionPoint
    /// of that type on a prefab, and give a creature an Interact behaviour with it.
    /// </summary>
    public enum InteractionType
    {
        /// <summary>Sit down (e.g. on a bench).</summary>
        Sit,
        /// <summary>Lie down and sleep (e.g. a cat napping by a tree).</summary>
        Sleep
    }
}

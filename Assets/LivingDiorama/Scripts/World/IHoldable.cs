namespace LivingDiorama.World
{
    /// <summary>
    /// Something that acts on its own (a creature) and can be held still, e.g. while it is selected
    /// in the editor so it can be dragged and edited.
    /// </summary>
    public interface IHoldable
    {
        void SetHeld(bool held);
    }
}

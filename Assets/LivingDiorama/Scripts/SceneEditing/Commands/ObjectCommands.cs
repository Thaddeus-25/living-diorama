using LivingDiorama.Persistence;
using LivingDiorama.World;

namespace LivingDiorama.SceneEditing.Commands
{
    /// <summary>Creates an object (also used for duplicates). Undo removes it.</summary>
    public sealed class PlaceObjectCommand : ICommand
    {
        private readonly DioramaWorld world;
        private readonly PlacedObjectData data;

        public PlaceObjectCommand(DioramaWorld world, PlacedObjectData data, string description = "Place")
        {
            this.world = world;
            this.data = data.Clone();
            Description = description;
        }

        public string Description { get; }
        public string ObjectId => data.id;

        public void Execute() => world.Spawn(data.Clone());
        public void Undo() => world.Remove(data.id);
    }

    /// <summary>Removes an object. Undo recreates it with the same id and state.</summary>
    public sealed class DeleteObjectCommand : ICommand
    {
        private readonly DioramaWorld world;
        private readonly PlacedObjectData data;

        public DeleteObjectCommand(DioramaWorld world, PlacedObjectData data)
        {
            this.world = world;
            this.data = data.Clone();
        }

        public string Description => "Delete";

        public void Execute() => world.Remove(data.id);
        public void Undo() => world.Spawn(data.Clone());
    }

    /// <summary>Any change to an existing object: move, rotate, scale, flip.</summary>
    public sealed class ModifyObjectCommand : ICommand
    {
        private readonly DioramaWorld world;
        private readonly PlacedObjectData before;
        private readonly PlacedObjectData after;

        public ModifyObjectCommand(DioramaWorld world, PlacedObjectData before, PlacedObjectData after, string description = "Edit")
        {
            this.world = world;
            this.before = before.Clone();
            this.after = after.Clone();
            Description = description;
        }

        public string Description { get; }

        public void Execute() => world.Apply(after);
        public void Undo() => world.Apply(before);
    }
}

namespace LivingDiorama.Entities
{
    /// <summary>A state of a finite-state machine that runs on some context (e.g. a creature).</summary>
    public interface IState<in TContext>
    {
        string Name { get; }

        /// <summary>When true, the owner picks the next state.</summary>
        bool IsFinished { get; }

        void Enter(TContext context);
        void Tick(TContext context, float deltaTime);
        void Exit(TContext context);
    }

    /// <summary>
    /// Minimal finite-state machine: one current state, with Exit/Enter called on every change.
    /// Choosing the next state is up to the owner, which keeps this class reusable.
    /// </summary>
    public sealed class StateMachine<TContext>
    {
        public IState<TContext> Current { get; private set; }

        public void ChangeState(IState<TContext> next, TContext context)
        {
            Current?.Exit(context);
            Current = next;
            Current?.Enter(context);
        }

        public void Tick(TContext context, float deltaTime) => Current?.Tick(context, deltaTime);

        public void Stop(TContext context) => ChangeState(null, context);
    }
}

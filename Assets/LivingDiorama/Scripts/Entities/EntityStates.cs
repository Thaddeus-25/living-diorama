using LivingDiorama.Data;
using UnityEngine;
using UnityEngine.AI;

namespace LivingDiorama.Entities
{
    /// <summary>Shared timing for creature states.</summary>
    public abstract class EntityState : IState<LivingEntity>
    {
        protected float Elapsed { get; private set; }

        public abstract string Name { get; }
        public bool IsFinished { get; protected set; }

        public virtual void Enter(LivingEntity entity)
        {
            Elapsed = 0f;
            IsFinished = false;
        }

        public virtual void Tick(LivingEntity entity, float deltaTime) => Elapsed += deltaTime;

        public virtual void Exit(LivingEntity entity) { }
    }

    /// <summary>Stays in place playing one animation for a while. Used for Idle and for Rest (sit, sleep, stretch...).</summary>
    public sealed class StayState : EntityState
    {
        private readonly string name;
        private readonly float duration;
        private readonly string animation;

        public StayState(string name, float duration, string animation)
        {
            this.name = name;
            this.duration = duration;
            this.animation = string.IsNullOrEmpty(animation) ? SpriteAnimationSet.Idle : animation;
        }

        public override string Name => name;
        public string Animation => animation;

        public override void Enter(LivingEntity entity)
        {
            base.Enter(entity);
            entity.StopMoving();
            entity.Animator.Play(animation);
        }

        public override void Tick(LivingEntity entity, float deltaTime)
        {
            base.Tick(entity, deltaTime);
            if (Elapsed >= duration) IsFinished = true;
        }
    }

    /// <summary>Stands still and turns to look one way, then the other, a few times.</summary>
    public sealed class LookAroundState : EntityState
    {
        private readonly float duration;
        private float nextTurn;

        public LookAroundState(float duration) => this.duration = duration;

        public override string Name => "LookAround";

        public override void Enter(LivingEntity entity)
        {
            base.Enter(entity);
            entity.StopMoving();
            entity.Animator.Play(SpriteAnimationSet.Idle);
            nextTurn = NextTurnDelay(entity);
        }

        public override void Tick(LivingEntity entity, float deltaTime)
        {
            base.Tick(entity, deltaTime);
            if (Elapsed >= nextTurn)
            {
                entity.Placed.FlipX = !entity.Placed.FlipX;
                nextTurn = Elapsed + NextTurnDelay(entity);
            }
            if (Elapsed >= duration) IsFinished = true;
        }

        private static float NextTurnDelay(LivingEntity entity) => 0.8f + (float)entity.Random.NextDouble() * 0.9f;
    }

    /// <summary>Walks to a random reachable point within the creature's wander radius.</summary>
    public sealed class WanderState : EntityState
    {
        private const int DestinationAttempts = 8;
        private const float MinimumDistance = 1f;
        private const float StuckSeconds = 1.5f;

        private readonly float timeLimit;
        private float stuckTime;

        public WanderState(float timeLimit) => this.timeLimit = timeLimit;

        public override string Name => "Wander";
        public Vector3 Destination { get; private set; }

        public override void Enter(LivingEntity entity)
        {
            base.Enter(entity);
            stuckTime = 0f;
            if (!TryPickDestination(entity, out var destination) || !entity.Agent.SetDestination(destination))
            {
                IsFinished = true; // nowhere to go; the brain picks something else
                return;
            }
            Destination = destination;
            entity.Animator.Play(SpriteAnimationSet.Walk);
        }

        public override void Tick(LivingEntity entity, float deltaTime)
        {
            base.Tick(entity, deltaTime);
            var agent = entity.Agent;
            if (agent.pathPending) return;

            bool arrived = agent.remainingDistance <= agent.stoppingDistance + 0.05f;
            bool invalid = agent.pathStatus == NavMeshPathStatus.PathInvalid;

            // Blocked by something (e.g. another creature standing on the destination).
            stuckTime = agent.velocity.sqrMagnitude < 0.01f ? stuckTime + deltaTime : 0f;

            if (arrived || invalid || stuckTime > StuckSeconds || Elapsed > timeLimit) IsFinished = true;
        }

        public override void Exit(LivingEntity entity) => entity.StopMoving();

        private static bool TryPickDestination(LivingEntity entity, out Vector3 destination)
        {
            var origin = entity.transform.position;
            float radius = entity.Definition != null ? entity.Definition.WanderRadius : 4f;
            for (int i = 0; i < DestinationAttempts; i++)
            {
                double angle = entity.Random.NextDouble() * Mathf.PI * 2f;
                float distance = Mathf.Lerp(MinimumDistance, radius, (float)entity.Random.NextDouble());
                var candidate = origin + new Vector3(Mathf.Cos((float)angle), 0f, Mathf.Sin((float)angle)) * distance;

                // The NavMesh only covers the garden (minus carved obstacles), so this also keeps creatures in bounds.
                if (NavMesh.SamplePosition(candidate, out var hit, 1f, NavMesh.AllAreas))
                {
                    destination = hit.position;
                    return true;
                }
            }
            destination = default;
            return false;
        }
    }
}

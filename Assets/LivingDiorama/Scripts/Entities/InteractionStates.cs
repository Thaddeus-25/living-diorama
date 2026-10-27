using LivingDiorama.Data;
using LivingDiorama.Interactions;
using UnityEngine;
using UnityEngine.AI;

namespace LivingDiorama.Entities
{
    /// <summary>
    /// Finds and reserves the nearest free interaction point of a type, then walks to the spot in
    /// front of it. On arrival it hands the reservation over to an <see cref="InteractState"/>.
    /// </summary>
    public sealed class SeekInteractionState : EntityState
    {
        private const float SearchRadius = 15f;
        private const float TimeLimit = 25f;
        private const float StuckSeconds = 2f;

        private readonly InteractionType type;
        private readonly float useDuration;
        private InteractionPoint point;
        private Vector3 approach;
        private bool handedOver;
        private float stuckTime;

        public SeekInteractionState(InteractionType type, float useDuration)
        {
            this.type = type;
            this.useDuration = useDuration;
        }

        public override string Name => $"Seek {type}";
        public InteractionPoint Target => point;

        public override void Enter(LivingEntity entity)
        {
            base.Enter(entity);
            handedOver = false;
            stuckTime = 0f;

            point = InteractionRegistry.FindNearestAvailable(entity, type, SearchRadius, out approach);
            if (point == null || !point.TryReserve(entity) || !entity.Agent.SetDestination(approach))
            {
                point = null;
                IsFinished = true; // nothing free nearby; the brain picks something else
                return;
            }
            entity.Animator.Play(SpriteAnimationSet.Walk);
        }

        public override void Tick(LivingEntity entity, float deltaTime)
        {
            base.Tick(entity, deltaTime);
            if (IsFinished) return;

            // The object was removed (or the point disabled), which also clears its reservations.
            if (point == null || !point.IsReservedBy(entity))
            {
                IsFinished = true;
                return;
            }

            var agent = entity.Agent;
            if (agent.pathPending) return;

            if (agent.remainingDistance <= agent.stoppingDistance + 0.1f)
            {
                handedOver = true;
                Next = new InteractState(point, approach, useDuration);
                IsFinished = true;
                return;
            }

            stuckTime = agent.velocity.sqrMagnitude < 0.01f ? stuckTime + deltaTime : 0f;
            if (agent.pathStatus == NavMeshPathStatus.PathInvalid || stuckTime > StuckSeconds || Elapsed > TimeLimit)
                IsFinished = true;
        }

        public override void Exit(LivingEntity entity)
        {
            entity.StopMoving();
            if (!handedOver && point != null) point.Release(entity);
        }
    }

    /// <summary>
    /// Uses a reserved interaction point: slides from the approach spot onto the point (which sits
    /// inside the object's no-walk area), holds the pose, slides back off and releases the point.
    /// While on the point the NavMesh agent is detached so it doesn't pull the creature back.
    /// The creature follows the point if its object is moved (e.g. a bench dragged in the editor).
    /// </summary>
    public sealed class InteractState : EntityState
    {
        private const float SlideSeconds = 0.35f;

        private enum Phase { SlideIn, Hold, SlideOut }

        private readonly InteractionPoint point;
        private Vector3 approach;
        private readonly float holdSeconds;
        private readonly string name;
        private readonly string animation;
        private readonly bool faceLeft;
        private Vector3 spot;
        private Vector3 slideFrom;
        private Phase phase;
        private float phaseTime;

        public InteractState(InteractionPoint point, Vector3 approach, float holdSeconds)
        {
            this.point = point;
            this.approach = approach;
            this.holdSeconds = holdSeconds;
            // Cached: the point may be destroyed while we're using it.
            name = $"Interact {point.Type}";
            animation = point.Animation;
            faceLeft = point.FaceLeft;
        }

        public override string Name => name;
        public InteractionPoint Point => point;
        public bool IsHolding => phase == Phase.Hold;

        /// <summary>The walkable spot in front of the point, where the creature stands up to.</summary>
        public Vector3 StandPosition
        {
            get
            {
                RefreshApproach();
                return approach;
            }
        }

        public override void Enter(LivingEntity entity)
        {
            base.Enter(entity);
            entity.StopMoving();
            entity.Agent.updatePosition = false;

            spot = point.Position;
            BeginPhase(Phase.SlideIn, entity.transform.position);
            entity.Animator.Play(SpriteAnimationSet.Walk);
            if (Mathf.Abs(spot.x - slideFrom.x) > 0.01f) entity.Placed.FlipX = spot.x < slideFrom.x;
        }

        public override void Tick(LivingEntity entity, float deltaTime)
        {
            base.Tick(entity, deltaTime);
            phaseTime += deltaTime;

            bool pointGone = point == null || !point.IsReservedBy(entity);
            if (pointGone && phase != Phase.SlideOut) BeginPhase(Phase.SlideOut, entity.transform.position);

            switch (phase)
            {
                case Phase.SlideIn:
                    if (!pointGone) spot = point.Position;
                    entity.transform.position = Vector3.Lerp(slideFrom, spot, Ease(phaseTime / SlideSeconds));
                    if (phaseTime >= SlideSeconds)
                    {
                        BeginPhase(Phase.Hold, spot);
                        entity.Animator.Play(animation);
                        entity.Placed.FlipX = faceLeft;
                    }
                    break;

                case Phase.Hold:
                    entity.transform.position = point.Position; // stay on the seat if the bench moves
                    if (phaseTime >= holdSeconds)
                    {
                        BeginPhase(Phase.SlideOut, entity.transform.position);
                        entity.Animator.Play(SpriteAnimationSet.Walk);
                    }
                    break;

                case Phase.SlideOut:
                    entity.transform.position = Vector3.Lerp(slideFrom, approach, Ease(phaseTime / SlideSeconds));
                    if (phaseTime >= SlideSeconds) IsFinished = true;
                    break;
            }
        }

        /// <summary>Always runs, even when interrupted (mode switch, object removed): back on the NavMesh, point released.</summary>
        public override void Exit(LivingEntity entity)
        {
            RefreshApproach();
            if (point != null) point.Release(entity);

            entity.transform.position = approach;
            var agent = entity.Agent;
            if (agent.enabled && agent.isOnNavMesh) agent.Warp(approach);
            agent.updatePosition = true;
        }

        /// <summary>Ease-in-out so stepping onto and off the seat starts and ends gently.</summary>
        public static float Ease(float t) => Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));

        private void BeginPhase(Phase next, Vector3 from)
        {
            phase = next;
            phaseTime = 0f;
            slideFrom = from;
            if (next == Phase.SlideOut) RefreshApproach();
        }

        /// <summary>The object may have moved since we arrived, so find the walkable spot in front of it again.</summary>
        private void RefreshApproach()
        {
            if (point == null) return; // destroyed: keep the last known spot
            if (NavMesh.SamplePosition(point.ApproachPosition, out var hit, 1f, NavMesh.AllAreas))
                approach = hit.position;
        }
    }
}

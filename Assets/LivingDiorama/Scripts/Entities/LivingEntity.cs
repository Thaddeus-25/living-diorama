using LivingDiorama.Core;
using LivingDiorama.Data;
using LivingDiorama.World;
using UnityEngine;
using UnityEngine.AI;

namespace LivingDiorama.Entities
{
    /// <summary>
    /// The "brain" of a creature. While the simulation runs it moves through states chosen from its
    /// <see cref="EntityDefinition"/>'s behaviour table. It stops (agent switched off, so it can be
    /// dragged freely) when the simulation is paused or while it is held, e.g. selected in the editor.
    /// </summary>
    [RequireComponent(typeof(PlacedObject), typeof(NavMeshAgent))]
    public class LivingEntity : MonoBehaviour, IPlacementAware, IHoldable
    {
        [SerializeField] private SpriteAnimator animator;
        [Tooltip("When the simulation starts, how far (metres) to search for walkable ground.")]
        [SerializeField] private float navMeshSnapDistance = 3f;

        // NavMesh crowd avoidance: lower number = more important. An agent pushes less important
        // agents out of its way, so creatures standing still get the most important value and are
        // walked around instead of shoved along.
        private const int StationaryPriority = 20;
        private const int MovingPriorityBase = 50;

        private readonly StateMachine<LivingEntity> brain = new();
        private AppModeController modes;
        private int movingPriority;
        private bool held;

        public PlacedObject Placed { get; private set; }
        public NavMeshAgent Agent { get; private set; }
        public SpriteAnimator Animator => animator;
        public EntityDefinition Definition => Placed != null ? Placed.Definition as EntityDefinition : null;
        public System.Random Random { get; private set; }
        public bool IsSimulating { get; private set; }
        public bool IsHeld => held;
        public IState<LivingEntity> CurrentState => brain.Current;

        public void Configure(SpriteAnimator spriteAnimator) => animator = spriteAnimator;

        private void Awake()
        {
            Placed = GetComponent<PlacedObject>();
            Agent = GetComponent<NavMeshAgent>();
            Random = new System.Random(System.Environment.TickCount ^ GetInstanceID());
            ApplyDefinitionToAgent();
        }

        private void OnEnable()
        {
            modes = AppModeController.Instance;
            if (modes != null) modes.SimulationChanged += OnSimulationChanged;
            RefreshSimulating();
        }

        private void OnDisable()
        {
            if (modes != null) modes.SimulationChanged -= OnSimulationChanged;
            SetSimulating(false);
        }

        /// <summary>
        /// Holding pauses just this creature (the editor holds the selected one so it can be dragged
        /// and edited). A sitting creature gets up first. Releasing lets it carry on.
        /// </summary>
        public void SetHeld(bool isHeld)
        {
            if (held == isHeld) return;
            held = isHeld;
            RefreshSimulating();
        }

        /// <summary>
        /// Where to record this creature: while sitting on a bench (inside its no-walk area) it's
        /// the walkable spot in front of the seat, so saves never place it inside an object.
        /// </summary>
        public Vector3 SavedPosition =>
            brain.Current is InteractState interaction ? interaction.StandPosition : transform.position;

        /// <summary>The editor, undo or a load moved this creature: continue from the new spot.</summary>
        public void OnPlacementApplied()
        {
            if (!IsSimulating) return;
            var target = transform.position;
            SetSimulating(false); // exits the current state (which may step off a bench)
            transform.position = target;
            RefreshSimulating();
        }

        private void RefreshSimulating()
        {
            // Without a mode controller (e.g. isolated tests) the creature simply lives.
            bool simulationOn = modes == null || modes.IsSimulationRunning;
            SetSimulating(simulationOn && !held && isActiveAndEnabled);
        }

        /// <summary>Makes behaviour repeatable (tests, debugging).</summary>
        public void SetRandomSeed(int seed) => Random = new System.Random(seed);

        public void SetSimulating(bool running)
        {
            if (running == IsSimulating) return;

            if (running)
            {
                if (!TryPlaceOnNavMesh())
                {
                    Debug.LogWarning($"[LivingEntity] {name} is not near walkable ground and will stay still.", this);
                    return;
                }
                IsSimulating = true;
                // A short pause before the first decision so creatures don't all start in sync.
                brain.ChangeState(new StayState("Idle", 0.3f + (float)Random.NextDouble(), SpriteAnimationSet.Idle), this);
            }
            else
            {
                IsSimulating = false;
                brain.Stop(this);
                StopMoving();
                if (Agent != null) Agent.enabled = false;
                if (animator != null) animator.Play(SpriteAnimationSet.Idle);
            }
        }

        /// <summary>Starts a specific behaviour now (debugging, tests, future interactions).</summary>
        public void StartBehaviour(BehaviourWeight behaviour)
        {
            if (!IsSimulating) return;
            brain.ChangeState(BehaviourPicker.CreateState(behaviour, Random), this);
        }

        public void StopMoving()
        {
            if (Agent != null && Agent.enabled && Agent.isOnNavMesh) Agent.ResetPath();
        }

        private void Update()
        {
            if (!IsSimulating) return;

            brain.Tick(this, Time.deltaTime);
            if (brain.Current == null || brain.Current.IsFinished)
            {
                // A chained follow-up (e.g. sit after reaching the bench) wins over a fresh pick.
                var next = (brain.Current as EntityState)?.Next;
                if (next == null)
                {
                    var entry = BehaviourPicker.Pick(Definition != null ? Definition.Behaviours : null, Random);
                    next = BehaviourPicker.CreateState(entry, Random);
                }
                brain.ChangeState(next, this);
            }

            UpdateAvoidancePriority();
            UpdateFacing();
        }

        private void UpdateAvoidancePriority()
        {
            bool moving = Agent.pathPending || Agent.hasPath;
            Agent.avoidancePriority = moving ? movingPriority : StationaryPriority;
        }

        /// <summary>Sprites face right; mirror them while walking left.</summary>
        private void UpdateFacing()
        {
            float sideways = Agent.velocity.x;
            if (Mathf.Abs(sideways) > 0.05f) Placed.FlipX = sideways < 0f;
        }

        private bool TryPlaceOnNavMesh()
        {
            if (Agent == null) return false;
            if (!NavMesh.SamplePosition(transform.position, out var hit, navMeshSnapDistance, NavMesh.AllAreas))
                return false;

            transform.position = hit.position;
            Agent.enabled = true;
            Agent.Warp(hit.position);
            return Agent.isOnNavMesh;
        }

        private void ApplyDefinitionToAgent()
        {
            var definition = Definition;
            if (definition == null) return;
            Agent.speed = definition.MoveSpeed;
            Agent.radius = definition.AgentRadius;
            Agent.updateRotation = false; // sprites face the camera; facing is handled by mirroring
            Agent.angularSpeed = 720f;
            Agent.acceleration = 6f;
            Agent.stoppingDistance = 0.1f;
            Agent.obstacleAvoidanceType = ObstacleAvoidanceType.LowQualityObstacleAvoidance;
            // A little variation so two walking creatures don't negotiate as exact equals.
            movingPriority = MovingPriorityBase + Random.Next(10);
            Agent.avoidancePriority = StationaryPriority;
        }

        private void OnSimulationChanged(bool running) => RefreshSimulating();
    }
}

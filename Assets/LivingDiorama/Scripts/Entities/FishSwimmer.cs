using LivingDiorama.Core;
using LivingDiorama.Data;
using LivingDiorama.World;
using UnityEngine;

namespace LivingDiorama.Entities
{
    /// <summary>
    /// A water creature. It can't use the NavMesh (ponds are holes in it), so it steers itself:
    /// swim to a random spot inside its <see cref="WaterArea"/>, turning smoothly, hover a while,
    /// repeat. Its definition's behaviour table decides the mix (Wander = swim, Idle = hover).
    /// It never leaves the water; out of water it waits, still, until it's back in a pond.
    /// </summary>
    [RequireComponent(typeof(PlacedObject))]
    public class FishSwimmer : MonoBehaviour, IPlacementAware, IHoldable, IWaterResident
    {
        public const string Swim = "swim";

        [SerializeField] private SpriteAnimator animator;
        [Tooltip("The flat sprite; rotated to face the swimming direction.")]
        [SerializeField] private Transform visual;
        [Tooltip("Keep this far (metres) from the water's edge.")]
        [SerializeField] private float edgeMargin = 0.3f;
        [SerializeField] private float turnSpeed = 220f;
        [SerializeField] private float acceleration = 1.5f;

        private PlacedObject placed;
        private AppModeController modes;
        private System.Random random;
        private bool held;
        private bool swimming;
        private float heading;
        private float speed;
        private float stateTimeLeft;
        private float habitatSearchTimer;
        private Vector3 target;

        public WaterArea Habitat { get; private set; }
        public bool IsSimulating { get; private set; }
        public bool IsSwimming => IsSimulating && swimming;
        public bool IsHeld => held;
        public EntityDefinition Definition => placed != null ? placed.Definition as EntityDefinition : null;

        public void Configure(SpriteAnimator spriteAnimator, Transform flatVisual)
        {
            animator = spriteAnimator;
            visual = flatVisual;
        }

        private void Awake()
        {
            placed = GetComponent<PlacedObject>();
            random = new System.Random(System.Environment.TickCount ^ GetInstanceID());
            heading = (float)random.NextDouble() * 360f;
        }

        private void OnEnable()
        {
            modes = AppModeController.Instance;
            if (modes != null) modes.SimulationChanged += OnSimulationChanged;
            AdoptWaterHere();
            Refresh();
        }

        private void OnDisable()
        {
            if (modes != null) modes.SimulationChanged -= OnSimulationChanged;
            SetHabitat(null);
            IsSimulating = false;
        }

        public void SetRandomSeed(int seed) => random = new System.Random(seed);

        /// <summary>Held (selected) fish stay still; letting go puts them back in the nearest water.</summary>
        public void SetHeld(bool isHeld)
        {
            if (held == isHeld) return;
            held = isHeld;
            if (!held) SettleInWater();
            Refresh();
        }

        public void OnPlacementApplied()
        {
            // While dragged it may cross dry land; it settles into water when released.
            if (held) AdoptWaterHere();
            else SettleInWater();
            Refresh();
        }

        public Vector3 SavedPosition => transform.position;

        public void ShiftBy(Vector3 offset)
        {
            transform.position += offset;
            target += offset;
        }

        private void Update()
        {
            // Lost its water (pond deleted or not loaded yet): look again now and then.
            if (Habitat == null || !Habitat.isActiveAndEnabled)
            {
                // Unity's == treats the destroyed or disabled pond as gone; ReferenceEquals tells us it just happened.
                if (!ReferenceEquals(Habitat, null))
                {
                    // Stop at once (no water holds the fish in any more) and look for new water straight away.
                    SetHabitat(null);
                    habitatSearchTimer = 0f;
                    Refresh();
                }
                habitatSearchTimer -= Time.deltaTime;
                if (habitatSearchTimer <= 0f)
                {
                    habitatSearchTimer = 1f;
                    AdoptWaterHere();
                    Refresh();
                }
            }

            if (IsSimulating)
            {
                stateTimeLeft -= Time.deltaTime;
                if (swimming) SwimTowardsTarget(Time.deltaTime);
                else Hover(Time.deltaTime);
            }
            ApplyHeading();
        }

        private void SwimTowardsTarget(float deltaTime)
        {
            var offset = target - transform.position;
            offset.y = 0f;
            float distance = offset.magnitude;
            if (distance < 0.15f || stateTimeLeft <= 0f)
            {
                PickNextBehaviour();
                return;
            }

            float desired = Mathf.Atan2(-offset.z, offset.x) * Mathf.Rad2Deg;
            heading = Mathf.MoveTowardsAngle(heading, desired, turnSpeed * deltaTime);
            // Slow down for sharp turns and near the target, like a fish gliding in.
            float alignment = Mathf.Clamp01(Mathf.Cos(Mathf.DeltaAngle(heading, desired) * Mathf.Deg2Rad));
            float cruise = Definition != null ? Definition.MoveSpeed : 0.6f;
            float wanted = cruise * alignment * Mathf.Clamp01(distance / 0.6f + 0.3f);
            speed = Mathf.MoveTowards(speed, wanted, acceleration * deltaTime);
            Move(Direction(heading) * (speed * deltaTime));
        }

        private void Hover(float deltaTime)
        {
            speed = Mathf.MoveTowards(speed, 0f, acceleration * deltaTime);
            // A lazy wiggle so a hovering fish still looks alive.
            heading += Mathf.Sin(Time.time * 1.7f + GetInstanceID()) * 12f * deltaTime;
            Move(Direction(heading) * (speed * deltaTime));
            if (stateTimeLeft <= 0f) PickNextBehaviour();
        }

        private void Move(Vector3 step)
        {
            var next = transform.position + step;
            transform.position = Habitat != null ? Habitat.ClosestPointInside(next, edgeMargin * 0.5f) : next;
        }

        private void PickNextBehaviour()
        {
            var entry = BehaviourPicker.Pick(Definition != null ? Definition.Behaviours : null, random);
            float min = entry != null ? Mathf.Max(0.5f, entry.durationRange.x) : 1.5f;
            float max = entry != null ? Mathf.Max(min, entry.durationRange.y) : 3f;
            stateTimeLeft = Mathf.Lerp(min, max, (float)random.NextDouble());

            swimming = entry != null && entry.behaviour == EntityBehaviour.Wander && Habitat != null;
            if (swimming) target = Habitat.RandomPointInside(random, edgeMargin);
            PlayAnimation();
        }

        private void Refresh()
        {
            bool simulationOn = modes == null || modes.IsSimulationRunning;
            bool shouldSimulate = simulationOn && !held && Habitat != null && isActiveAndEnabled;
            if (shouldSimulate == IsSimulating) return;
            IsSimulating = shouldSimulate;
            if (IsSimulating) PickNextBehaviour();
            else
            {
                swimming = false;
                speed = 0f;
                PlayAnimation();
            }
        }

        /// <summary>Joins the water it's in, if any.</summary>
        private void AdoptWaterHere() => SetHabitat(WaterArea.FindContaining(transform.position));

        /// <summary>Moves into the nearest water (if any exists) and lives there.</summary>
        private void SettleInWater()
        {
            var water = WaterArea.FindContaining(transform.position) ?? WaterArea.FindNearest(transform.position);
            SetHabitat(water);
            // Only fish on dry land are moved, so a fish already in the water (e.g. after loading) stays exactly put.
            if (water != null && !water.Contains(transform.position))
                transform.position = water.ClosestPointInside(transform.position, edgeMargin);
        }

        private void SetHabitat(WaterArea water)
        {
            // Reference comparison: Unity's == says a destroyed pond equals null, which would skip clearing it.
            if (ReferenceEquals(Habitat, water)) return;
            if (Habitat != null) Habitat.RemoveResident(this);
            Habitat = water;
            if (Habitat != null) Habitat.AddResident(this);
        }

        private void PlayAnimation()
        {
            if (animator != null) animator.Play(IsSwimming ? Swim : SpriteAnimationSet.Idle);
        }

        /// <summary>Heading in degrees around the up axis; 0 = +X (right), 90 = -Z (towards the viewer).</summary>
        private static Vector3 Direction(float headingDegrees)
        {
            float radians = headingDegrees * Mathf.Deg2Rad;
            return new Vector3(Mathf.Cos(radians), 0f, -Mathf.Sin(radians));
        }

        private void ApplyHeading()
        {
            // The sprite lies flat (90 degrees about X) and faces +X; turn it about the up axis.
            if (visual != null) visual.rotation = Quaternion.Euler(0f, heading, 0f) * Quaternion.Euler(90f, 0f, 0f);
        }

        private void OnSimulationChanged(bool running) => Refresh();
    }
}

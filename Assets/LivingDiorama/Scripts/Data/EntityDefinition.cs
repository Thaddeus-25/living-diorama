using System;
using System.Collections.Generic;
using UnityEngine;

namespace LivingDiorama.Data
{
    /// <summary>The reusable behaviours a creature can pick from. Each maps to a state class.</summary>
    public enum EntityBehaviour
    {
        /// <summary>Stand still.</summary>
        Idle,
        /// <summary>Walk to a random reachable spot nearby.</summary>
        Wander,
        /// <summary>Stand and turn to look one way, then the other.</summary>
        LookAround,
        /// <summary>Stay put playing a named animation (sit, sleep, stretch...).</summary>
        Rest,
        /// <summary>Walk to a free interaction point of a given type (e.g. a bench seat) and use it.</summary>
        Interact
    }

    /// <summary>One entry in a creature's "what do I do next?" table.</summary>
    [Serializable]
    public class BehaviourWeight
    {
        public EntityBehaviour behaviour;
        [Tooltip("Relative chance of being picked. 0 disables the entry.")]
        [Min(0f)] public float weight = 1f;
        [Tooltip("How long the behaviour lasts, in seconds (x = min, y = max). For Wander it's a time limit.")]
        public Vector2 durationRange = new(2f, 5f);
        [Tooltip("Animation to play (Rest uses it; others default to idle/walk when empty).")]
        public string animation;
        [Tooltip("For Interact: which kind of interaction point to look for. The duration is how long it stays there.")]
        public InteractionType interaction;

        public BehaviourWeight() { }

        public BehaviourWeight(EntityBehaviour behaviour, float weight, Vector2 durationRange, string animation = null)
        {
            this.behaviour = behaviour;
            this.weight = weight;
            this.durationRange = durationRange;
            this.animation = animation;
        }

        public static BehaviourWeight Interact(InteractionType interaction, float weight, Vector2 durationRange) =>
            new(EntityBehaviour.Interact, weight, durationRange) { interaction = interaction };

        /// <summary>True when both entries describe the same choice (ignoring weight and duration).</summary>
        public bool SameChoiceAs(BehaviourWeight other) =>
            other != null && behaviour == other.behaviour
            // Unity saves null strings as "", so treat them as equal.
            && (animation ?? "") == (other.animation ?? "")
            && (behaviour != EntityBehaviour.Interact || interaction == other.interaction);
    }

    /// <summary>
    /// A placeable living creature. Everything that makes a cat different from a human lives here
    /// as data (speed, wander range, behaviour table, animations), so most new creatures need a
    /// new asset and art, not new code.
    /// </summary>
    [CreateAssetMenu(menuName = "Living Diorama/Entity Definition", fileName = "NewEntityDefinition")]
    public class EntityDefinition : ObjectDefinition
    {
        [Header("Living Entity")]
        [SerializeField, Min(0.1f)] private float moveSpeed = 1.2f;
        [Tooltip("How far (metres) a single wander can go from the current position.")]
        [SerializeField, Min(0.5f)] private float wanderRadius = 5f;
        [SerializeField, Min(0.05f)] private float agentRadius = 0.25f;
        [SerializeField] private SpriteAnimationSet animations;
        [SerializeField] private List<BehaviourWeight> behaviours = new();

        public float MoveSpeed => moveSpeed;
        public float WanderRadius => wanderRadius;
        public float AgentRadius => agentRadius;
        public SpriteAnimationSet Animations => animations;
        public IReadOnlyList<BehaviourWeight> Behaviours => behaviours;

        public void SetAnimations(SpriteAnimationSet animationSet) => animations = animationSet;

        /// <summary>
        /// Adds a default behaviour to an existing definition unless an equivalent entry is already
        /// there. Used to roll out new defaults without overwriting tuned tables (to turn one off,
        /// set its weight to 0 rather than deleting it).
        /// </summary>
        public bool AddBehaviourIfMissing(BehaviourWeight entry)
        {
            foreach (var existing in behaviours)
                if (existing != null && existing.SameChoiceAs(entry)) return false;
            behaviours.Add(entry);
            return true;
        }

        public void ConfigureEntity(float speed, float wander, float radius, SpriteAnimationSet animationSet,
            IEnumerable<BehaviourWeight> behaviourTable)
        {
            moveSpeed = speed;
            wanderRadius = wander;
            agentRadius = radius;
            animations = animationSet;
            behaviours = new List<BehaviourWeight>(behaviourTable);
        }
    }
}

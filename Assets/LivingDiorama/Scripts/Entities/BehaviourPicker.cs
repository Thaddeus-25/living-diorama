using System;
using System.Collections.Generic;
using LivingDiorama.Data;
using UnityEngine;

namespace LivingDiorama.Entities
{
    /// <summary>Turns a creature's behaviour table into its next state.</summary>
    public static class BehaviourPicker
    {
        /// <summary>Weighted random choice. Returns null when nothing has a positive weight.</summary>
        public static BehaviourWeight Pick(IReadOnlyList<BehaviourWeight> table, System.Random random)
        {
            if (table == null) return null;

            float total = 0f;
            foreach (var entry in table)
                if (entry != null && entry.weight > 0f) total += entry.weight;
            if (total <= 0f) return null;

            double roll = random.NextDouble() * total;
            foreach (var entry in table)
            {
                if (entry == null || entry.weight <= 0f) continue;
                roll -= entry.weight;
                if (roll < 0) return entry;
            }
            // Floating-point leftovers: return the last valid entry.
            for (int i = table.Count - 1; i >= 0; i--)
                if (table[i] != null && table[i].weight > 0f) return table[i];
            return null;
        }

        public static EntityState CreateState(BehaviourWeight entry, System.Random random)
        {
            if (entry == null) return new StayState("Idle", 2f, SpriteAnimationSet.Idle);

            float min = Mathf.Max(0.1f, Mathf.Min(entry.durationRange.x, entry.durationRange.y));
            float max = Mathf.Max(min, entry.durationRange.y);
            float duration = Mathf.Lerp(min, max, (float)random.NextDouble());

            return entry.behaviour switch
            {
                EntityBehaviour.Idle => new StayState("Idle", duration, entry.animation),
                EntityBehaviour.Wander => new WanderState(duration),
                EntityBehaviour.LookAround => new LookAroundState(duration),
                EntityBehaviour.Rest => new StayState($"Rest ({entry.animation})", duration, entry.animation),
                _ => throw new ArgumentOutOfRangeException(nameof(entry), entry.behaviour, "Unknown behaviour")
            };
        }
    }
}

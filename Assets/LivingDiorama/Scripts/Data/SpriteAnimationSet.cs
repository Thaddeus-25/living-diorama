using System;
using System.Collections.Generic;
using UnityEngine;

namespace LivingDiorama.Data
{
    /// <summary>
    /// Named flipbook animations for one creature ("idle", "walk", "sit", ...).
    /// Behaviours ask for animations by name; a missing name falls back to "idle".
    /// </summary>
    [CreateAssetMenu(menuName = "Living Diorama/Sprite Animation Set", fileName = "NewAnimationSet")]
    public class SpriteAnimationSet : ScriptableObject
    {
        public const string Idle = "idle";
        public const string Walk = "walk";

        [Serializable]
        public class Clip
        {
            public string name;
            public Sprite[] frames = Array.Empty<Sprite>();
            [Min(0.1f)] public float framesPerSecond = 6f;
            public bool loop = true;
        }

        [SerializeField] private List<Clip> clips = new();

        public IReadOnlyList<Clip> Clips => clips;

        /// <summary>The named clip, else "idle", else the first clip with frames, else null.</summary>
        public Clip Find(string clipName)
        {
            Clip idle = null;
            Clip first = null;
            foreach (var clip in clips)
            {
                if (clip == null || clip.frames == null || clip.frames.Length == 0) continue;
                if (clip.name == clipName) return clip;
                if (clip.name == Idle) idle = clip;
                first ??= clip;
            }
            return idle ?? first;
        }

        public void SetClips(IEnumerable<Clip> newClips) => clips = new List<Clip>(newClips);

        /// <summary>Which frame to show <paramref name="time"/> seconds into a clip.</summary>
        public static int FrameAt(float time, float framesPerSecond, int frameCount, bool loop)
        {
            if (frameCount <= 1) return 0;
            int frame = Mathf.FloorToInt(Mathf.Max(0f, time) * framesPerSecond);
            return loop ? frame % frameCount : Mathf.Min(frame, frameCount - 1);
        }
    }
}

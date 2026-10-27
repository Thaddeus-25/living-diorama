using LivingDiorama.Data;
using UnityEngine;

namespace LivingDiorama.World
{
    /// <summary>Plays named flipbook clips from a <see cref="SpriteAnimationSet"/> on a <see cref="SpriteVisual"/>.</summary>
    public class SpriteAnimator : MonoBehaviour
    {
        [SerializeField] private SpriteVisual visual;
        [SerializeField] private SpriteAnimationSet animations;

        private SpriteAnimationSet.Clip currentClip;
        private float clipTime;

        public string CurrentClipName { get; private set; }

        /// <summary>Index of the frame being shown in the current clip.</summary>
        public int CurrentFrame { get; private set; }

        public void Configure(SpriteVisual targetVisual, SpriteAnimationSet animationSet)
        {
            visual = targetVisual;
            animations = animationSet;
        }

        private void OnEnable()
        {
            if (CurrentClipName == null) Play(SpriteAnimationSet.Idle);
        }

        /// <summary>Switches clip. Asking for the clip that's already playing does nothing unless <paramref name="restart"/>.</summary>
        public void Play(string clipName, bool restart = false)
        {
            if (!restart && clipName == CurrentClipName) return;
            CurrentClipName = clipName;
            currentClip = animations != null ? animations.Find(clipName) : null;
            clipTime = 0f;
            ShowCurrentFrame();
        }

        private void Update()
        {
            if (currentClip == null) return;
            clipTime += Time.deltaTime;
            ShowCurrentFrame();
        }

        private void ShowCurrentFrame()
        {
            if (currentClip == null || visual == null) return;
            int frame = SpriteAnimationSet.FrameAt(clipTime, currentClip.framesPerSecond, currentClip.frames.Length, currentClip.loop);
            CurrentFrame = frame;
            visual.Sprite = currentClip.frames[frame];
        }
    }
}

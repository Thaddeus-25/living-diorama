using LivingDiorama.Data;
using LivingDiorama.World;
using UnityEngine;

namespace LivingDiorama.Entities
{
    /// <summary>
    /// Lifts a walking creature's sprite by exactly one art pixel on every other walk frame, a
    /// classic pixel-art bounce that stays in step with the animation. Whole-pixel steps keep the
    /// art crisp (sub-pixel scaling or wobble would shimmer with point filtering).
    /// Sits on the creature's visual, next to its <see cref="SpriteAnimator"/>.
    /// </summary>
    [RequireComponent(typeof(SpriteAnimator))]
    public class WalkBob : MonoBehaviour
    {
        [Tooltip("Height of the bounce in art pixels.")]
        [SerializeField, Min(0)] private int bobPixels = 1;

        private SpriteAnimator animator;
        private bool hasApplied;

        /// <summary>The current lift in metres (before the creature's scale).</summary>
        public float CurrentLift { get; private set; }

        private void Awake() => animator = GetComponent<SpriteAnimator>();

        private void LateUpdate()
        {
            bool walking = animator.CurrentClipName == SpriteAnimationSet.Walk;
            bool upFrame = animator.CurrentFrame % 2 == 0;
            float lift = walking && upFrame ? bobPixels / (float)PixelsPerUnit : 0f;
            if (Mathf.Approximately(lift, CurrentLift) && hasApplied) return; // the offset rides along with the parent
            CurrentLift = lift;
            hasApplied = true;

            // Lift along the sprite's own up axis (it faces the camera), so the step is one screen-aligned pixel.
            var parent = transform.parent;
            float scale = parent != null ? parent.lossyScale.y : 1f;
            var origin = parent != null ? parent.position : Vector3.zero;
            transform.position = origin + transform.up * (CurrentLift * scale);
        }

        // Matches the import setting of all sprites (16 px per metre).
        private const int PixelsPerUnit = 16;
    }
}

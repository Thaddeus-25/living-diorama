using LivingDiorama.World;
using UnityEngine;

namespace LivingDiorama.SceneEditing
{
    /// <summary>
    /// A ring on the ground under an object. Used twice: a gold pulsing ring for the selection and a
    /// faint still ring for whatever the mouse is over.
    /// </summary>
    public class SelectionIndicator : MonoBehaviour
    {
        [Tooltip("Flat ring visual, lying on the ground. Its sprite is 2 m across at scale 1.")]
        [SerializeField] private Transform ring;
        [SerializeField] private float padding = 0.6f;
        [SerializeField] private float pulseAmount = 0.05f;
        [SerializeField] private float pulseSpeed = 4f;
        [Tooltip("Multiplies the ring's colours.")]
        [SerializeField] private Color tint = Color.white;

        private Transform target;
        private float targetDiameter;

        public Transform Target => target;

        private void Awake()
        {
            if (ring != null && ring.TryGetComponent(out SpriteVisual visual)) visual.Tint = tint;
        }

        public void Show(PlacedObject placed)
        {
            target = placed != null ? placed.transform : null;
            if (placed != null)
            {
                var footprint = placed.Definition != null ? placed.Definition.Footprint : Vector2.one;
                targetDiameter = Mathf.Max(footprint.x, footprint.y);
            }
            ring.gameObject.SetActive(target != null);
            LateUpdate();
        }

        private void LateUpdate()
        {
            if (target == null)
            {
                if (ring.gameObject.activeSelf) ring.gameObject.SetActive(false);
                return;
            }

            float pulse = 1f + Mathf.Sin(Time.unscaledTime * pulseSpeed) * pulseAmount;
            float diameter = (targetDiameter * target.localScale.x + padding) * pulse;
            // Slightly above the grass to avoid z-fighting.
            ring.position = new Vector3(target.position.x, 0.02f, target.position.z);
            ring.localScale = Vector3.one * (diameter * 0.5f);
        }
    }
}

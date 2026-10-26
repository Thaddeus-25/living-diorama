using LivingDiorama.World;
using UnityEngine;

namespace LivingDiorama.SceneEditing
{
    /// <summary>A gently pulsing ring on the ground under the selected object.</summary>
    public class SelectionIndicator : MonoBehaviour
    {
        [Tooltip("Flat ring visual, lying on the ground. Its sprite is 2 m across at scale 1.")]
        [SerializeField] private Transform ring;
        [SerializeField] private float padding = 0.6f;
        [SerializeField] private float pulseAmount = 0.05f;
        [SerializeField] private float pulseSpeed = 4f;

        private Transform target;
        private float targetDiameter;

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

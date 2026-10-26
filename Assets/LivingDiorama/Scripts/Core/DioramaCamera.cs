using UnityEngine;
using UnityEngine.InputSystem;

namespace LivingDiorama.Core
{
    /// <summary>
    /// Fixed orthographic camera angled down at the diorama. The only user control is a clamped,
    /// smoothed scroll-wheel zoom.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class DioramaCamera : MonoBehaviour
    {
        [SerializeField] private Vector3 focusPoint = new(0f, 0.5f, 0f);
        [SerializeField, Range(10f, 60f)] private float pitch = 30f;
        [SerializeField] private float distance = 30f;

        [Header("Zoom")]
        [SerializeField] private float defaultSize = 6.5f;
        [SerializeField] private float minSize = 3.5f;
        [SerializeField] private float maxSize = 7.5f;
        [SerializeField] private float zoomStep = 0.5f;
        [SerializeField] private float zoomSmoothing = 10f;

        private Camera cam;
        private float targetSize;

        /// <summary>The UI turns zoom off while the pointer is over a panel that scrolls.</summary>
        public bool ZoomEnabled { get; set; } = true;

        public float Pitch => pitch;
        public Vector3 FocusPoint => focusPoint;
        public float DefaultSize => defaultSize;

        private void Awake()
        {
            cam = GetComponent<Camera>();
            cam.orthographic = true;
            targetSize = defaultSize;
            cam.orthographicSize = defaultSize;
            ApplyPose();
        }

        /// <summary>Places the camera on the pitch arc around the focus point.</summary>
        public void ApplyPose()
        {
            var rotation = Quaternion.Euler(pitch, 0f, 0f);
            transform.SetPositionAndRotation(focusPoint - rotation * Vector3.forward * distance, rotation);
        }

        private void Update()
        {
            var mouse = Mouse.current;
            if (mouse != null && ZoomEnabled)
            {
                float scroll = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(scroll) > 0.01f)
                    targetSize = Mathf.Clamp(targetSize - Mathf.Sign(scroll) * zoomStep, minSize, maxSize);
            }

            // Frame-rate independent exponential smoothing.
            float t = 1f - Mathf.Exp(-zoomSmoothing * Time.unscaledDeltaTime);
            cam.orthographicSize = Mathf.Lerp(cam.orthographicSize, targetSize, t);
        }
    }
}

using UnityEngine;

namespace LivingDiorama.World
{
    /// <summary>
    /// Keeps a sprite parallel to the camera's view plane, like a Paper Mario cutout.
    /// The parent keeps its own yaw, so colliders and interaction points still rotate with the object.
    /// </summary>
    [ExecuteAlways]
    public class Billboard : MonoBehaviour
    {
        private void LateUpdate()
        {
            var cam = Camera.main;
            if (cam != null) AlignTo(cam);
        }

        public void AlignTo(Camera cam)
        {
            // The camera rarely rotates; skipping unchanged writes avoids marking the transform
            // (and its click collider) as moved every frame.
            var rotation = cam.transform.rotation;
            if (transform.rotation != rotation) transform.rotation = rotation;
        }

        /// <summary>Aligns every billboard at once. Used by editor tools, where LateUpdate doesn't tick.</summary>
        public static void AlignAll(Camera cam)
        {
            foreach (var billboard in FindObjectsByType<Billboard>(FindObjectsSortMode.None))
                billboard.AlignTo(cam);
        }
    }
}

using System;
using LivingDiorama.Data;
using UnityEngine;

namespace LivingDiorama.World
{
    /// <summary>
    /// Marks a scene object as user-placed. Links the instance to its definition and gives it a
    /// unique id, which is what save files and undo commands refer to.
    /// </summary>
    [DisallowMultipleComponent]
    public class PlacedObject : MonoBehaviour
    {
        [SerializeField] private string instanceId;
        [SerializeField] private ObjectDefinition definition;
        [SerializeField] private SpriteVisual visual;
        [SerializeField] private bool flipX;

        public string InstanceId => instanceId;
        public ObjectDefinition Definition => definition;
        public SpriteVisual Visual => visual;

        /// <summary>The user's mirror choice. The drawn sprite also mirrors when the object faces away (see <see cref="ApplyFacing"/>).</summary>
        public bool FlipX
        {
            get => flipX;
            set
            {
                flipX = value;
                ApplyFacing();
            }
        }

        /// <summary>Yaw in degrees around the up axis.</summary>
        public float Yaw
        {
            get => transform.eulerAngles.y;
            set
            {
                transform.rotation = Quaternion.Euler(0f, value, 0f);
                ApplyFacing();
            }
        }

        public void Configure(ObjectDefinition newDefinition, SpriteVisual newVisual)
        {
            definition = newDefinition;
            visual = newVisual;
        }

        /// <summary>Gives the object a fresh id (new placements and duplicates).</summary>
        public void AssignNewId() => instanceId = NewId();

        /// <summary>Restores a known id (loading a save, undoing a delete).</summary>
        public void AssignId(string id) => instanceId = id;

        public static string NewId() => Guid.NewGuid().ToString("N");

        /// <summary>
        /// Sprites always face the camera, so yaw can't turn them. Instead, a sprite whose object
        /// faces away from the viewer (yaw between 90 and 270 degrees) is mirrored.
        /// </summary>
        public void ApplyFacing()
        {
            if (visual == null) return;
            bool facingAway = Mathf.Cos(Yaw * Mathf.Deg2Rad) < -0.01f;
            visual.FlipX = flipX ^ facingAway;
        }

        private void Awake()
        {
            if (string.IsNullOrEmpty(instanceId)) AssignNewId();
        }
    }
}

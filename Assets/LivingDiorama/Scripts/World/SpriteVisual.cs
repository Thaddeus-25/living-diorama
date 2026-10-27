using UnityEngine;

namespace LivingDiorama.World
{
    /// <summary>
    /// Draws a pixel-art sprite in the 3D world with a shared lit cutout material.
    /// The texture is passed per renderer through a property block, so every object shares one material.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class SpriteVisual : MonoBehaviour
    {
        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField] private Sprite sprite;
        [SerializeField] private bool flipX;
        [Tooltip("Multiplies the sprite's colours (e.g. to mark a placement preview).")]
        [SerializeField] private Color tint = Color.white;

        private MeshFilter meshFilter;
        private MeshRenderer meshRenderer;
        private MaterialPropertyBlock propertyBlock;

        public Sprite Sprite
        {
            get => sprite;
            set
            {
                if (sprite == value) return;
                sprite = value;
                Apply();
            }
        }

        public bool FlipX
        {
            get => flipX;
            set
            {
                if (flipX == value) return;
                flipX = value;
                ApplyFlip();
            }
        }

        public Color Tint
        {
            get => tint;
            set
            {
                if (tint == value) return;
                tint = value;
                Apply();
            }
        }

        public MeshRenderer Renderer => meshRenderer != null ? meshRenderer : meshRenderer = GetComponent<MeshRenderer>();

        private void OnEnable() => Apply();

        private void OnValidate()
        {
            if (isActiveAndEnabled) Apply();
        }

        public void Apply()
        {
            meshFilter = meshFilter != null ? meshFilter : GetComponent<MeshFilter>();
            meshFilter.sharedMesh = SpriteMeshCache.Get(sprite);

            propertyBlock ??= new MaterialPropertyBlock();
            propertyBlock.Clear();
            if (sprite != null) propertyBlock.SetTexture(BaseMapId, sprite.texture);
            // Only override the colour when tinted, so materials that rely on their own colour
            // (e.g. the dark contact shadow) keep it.
            if (tint != Color.white) propertyBlock.SetColor(BaseColorId, tint);
            Renderer.SetPropertyBlock(propertyBlock);

            if (sprite != null && TryGetComponent(out BoxCollider box))
            {
                // A thin box over the visible sprite, used for mouse picking.
                // Only when it changes: animation frames are usually the same size, and resizing
                // a collider rebuilds its physics shape.
                var bounds = sprite.bounds;
                var size = new Vector3(bounds.size.x, bounds.size.y, 0.1f);
                if (box.center != bounds.center) box.center = bounds.center;
                if (box.size != size) box.size = size;
            }

            ApplyFlip();
        }

        private void ApplyFlip()
        {
            var scale = transform.localScale;
            scale.x = Mathf.Abs(scale.x) * (flipX ? -1f : 1f);
            transform.localScale = scale;
        }
    }
}

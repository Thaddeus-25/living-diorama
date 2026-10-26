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

        [SerializeField] private Sprite sprite;
        [SerializeField] private bool flipX;

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
            Renderer.GetPropertyBlock(propertyBlock);
            if (sprite != null) propertyBlock.SetTexture(BaseMapId, sprite.texture);
            Renderer.SetPropertyBlock(propertyBlock);

            if (sprite != null && TryGetComponent(out BoxCollider box))
            {
                // A thin box over the visible sprite, used for mouse picking.
                var bounds = sprite.bounds;
                box.center = bounds.center;
                box.size = new Vector3(bounds.size.x, bounds.size.y, 0.1f);
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

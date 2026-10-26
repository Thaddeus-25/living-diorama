using System.Collections.Generic;
using UnityEngine;

namespace LivingDiorama.World
{
    /// <summary>
    /// Builds a mesh for each sprite once, using the sprite's own geometry plus normals that face
    /// the camera. SpriteRenderer meshes have no normals, which breaks URP Lit shading, so sprites
    /// are drawn with a MeshRenderer instead.
    /// </summary>
    public static class SpriteMeshCache
    {
        private static readonly Dictionary<Sprite, Mesh> Meshes = new();

        public static Mesh Get(Sprite sprite)
        {
            if (sprite == null) return null;
            if (Meshes.TryGetValue(sprite, out var cached) && cached != null) return cached;

            var spriteVertices = sprite.vertices;
            var vertices = new Vector3[spriteVertices.Length];
            var normals = new Vector3[spriteVertices.Length];
            var tangents = new Vector4[spriteVertices.Length];
            for (int i = 0; i < spriteVertices.Length; i++)
            {
                vertices[i] = spriteVertices[i];
                normals[i] = Vector3.back; // towards the camera
                tangents[i] = new Vector4(1f, 0f, 0f, -1f);
            }

            var triangles = sprite.triangles;
            var indices = new int[triangles.Length];
            for (int i = 0; i < triangles.Length; i++) indices[i] = triangles[i];

            var mesh = new Mesh
            {
                name = $"SpriteMesh_{sprite.name}",
                hideFlags = HideFlags.DontSave,
                vertices = vertices,
                uv = sprite.uv,
                normals = normals,
                tangents = tangents,
                triangles = indices
            };
            mesh.RecalculateBounds();
            Meshes[sprite] = mesh;
            return mesh;
        }
    }
}

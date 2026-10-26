using System;
using UnityEngine;

namespace LivingDiorama.EditorTools
{
    /// <summary>
    /// Tiny pixel painter used to generate original placeholder art.
    /// Coordinates match Texture2D: (0,0) is the bottom-left pixel.
    /// </summary>
    internal sealed class PixelCanvas
    {
        public readonly int Width;
        public readonly int Height;
        private readonly Color32[] pixels;

        public PixelCanvas(int width, int height)
        {
            Width = width;
            Height = height;
            pixels = new Color32[width * height]; // default is fully transparent
        }

        public static Color32 Hex(string hex)
        {
            if (!ColorUtility.TryParseHtmlString(hex, out var color))
                throw new ArgumentException($"Bad colour '{hex}'");
            return color;
        }

        public bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;

        public Color32 Get(int x, int y) => InBounds(x, y) ? pixels[y * Width + x] : default;

        public bool IsOpaque(int x, int y) => InBounds(x, y) && pixels[y * Width + x].a > 0;

        public void Set(int x, int y, Color32 color)
        {
            if (InBounds(x, y)) pixels[y * Width + x] = color;
        }

        public void Fill(Color32 color)
        {
            for (int i = 0; i < pixels.Length; i++) pixels[i] = color;
        }

        public void FillRect(int x, int y, int width, int height, Color32 color)
        {
            for (int py = y; py < y + height; py++)
            for (int px = x; px < x + width; px++)
                Set(px, py, color);
        }

        public void FillEllipse(float centerX, float centerY, float radiusX, float radiusY, Color32 color)
        {
            int minX = Mathf.FloorToInt(centerX - radiusX), maxX = Mathf.CeilToInt(centerX + radiusX);
            int minY = Mathf.FloorToInt(centerY - radiusY), maxY = Mathf.CeilToInt(centerY + radiusY);
            for (int y = minY; y <= maxY; y++)
            for (int x = minX; x <= maxX; x++)
            {
                float dx = (x + 0.5f - centerX) / radiusX;
                float dy = (y + 0.5f - centerY) / radiusY;
                if (dx * dx + dy * dy <= 1f) Set(x, y, color);
            }
        }

        /// <summary>
        /// Simple pixel-art shading: pixels whose neighbour toward the light (up-left) is empty
        /// get the highlight; pixels whose neighbour away from the light is empty get the shadow.
        /// Only pixels matching <paramref name="baseColor"/> are touched.
        /// </summary>
        public void ShadeEdges(Color32 baseColor, Color32 highlight, Color32 shadow, int depth = 2)
        {
            var copy = (Color32[])pixels.Clone();
            bool OpaqueIn(int x, int y) => InBounds(x, y) && copy[y * Width + x].a > 0;

            for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
            {
                if (!Same(copy[y * Width + x], baseColor)) continue;
                if (!OpaqueIn(x + depth, y - depth) || !OpaqueIn(x + 1, y - depth)) Set(x, y, shadow);
                else if (!OpaqueIn(x - depth, y + depth) || !OpaqueIn(x - 1, y + depth)) Set(x, y, highlight);
            }
        }

        /// <summary>Randomly swaps some pixels of one colour for another (texture/dither).</summary>
        public void Speckle(Color32 baseColor, Color32 speckle, float chance, System.Random random)
        {
            for (int i = 0; i < pixels.Length; i++)
                if (Same(pixels[i], baseColor) && random.NextDouble() < chance)
                    pixels[i] = speckle;
        }

        /// <summary>Adds a 1px outline around all opaque pixels (4-neighbourhood).</summary>
        public void Outline(Color32 color)
        {
            var copy = (Color32[])pixels.Clone();
            bool OpaqueIn(int x, int y) => InBounds(x, y) && copy[y * Width + x].a > 0;

            for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
            {
                if (OpaqueIn(x, y)) continue;
                if (OpaqueIn(x + 1, y) || OpaqueIn(x - 1, y) || OpaqueIn(x, y + 1) || OpaqueIn(x, y - 1))
                    Set(x, y, color);
            }
        }

        public byte[] EncodeToPng()
        {
            var texture = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
            texture.SetPixels32(pixels);
            texture.Apply();
            var png = texture.EncodeToPNG();
            UnityEngine.Object.DestroyImmediate(texture);
            return png;
        }

        private static bool Same(Color32 a, Color32 b) => a.r == b.r && a.g == b.g && a.b == b.b && a.a == b.a;
    }
}

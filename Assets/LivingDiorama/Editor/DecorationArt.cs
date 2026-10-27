using System.Collections.Generic;
using UnityEngine;
using ArtKind = LivingDiorama.EditorTools.PlaceholderArtGenerator.ArtKind;
using ArtSpec = LivingDiorama.EditorTools.PlaceholderArtGenerator.ArtSpec;
using Random = System.Random;

namespace LivingDiorama.EditorTools
{
    /// <summary>Original placeholder pixel art for decorations (16 px per metre, pivot at the bottom).</summary>
    internal static class DecorationArt
    {
        /// <summary>The campfire's flame animation.</summary>
        public static readonly CreatureArt.ClipSpec[] CampfireClips = { new("idle", 3, 8f) };

        public static IEnumerable<ArtSpec> Specs()
        {
            yield return new ArtSpec("Bush", ArtKind.Sprite, PaintBush);
            yield return new ArtSpec("Flowers", ArtKind.Sprite, PaintFlowers);
            yield return new ArtSpec("Mushrooms", ArtKind.Sprite, PaintMushrooms);
            yield return new ArtSpec("Fence", ArtKind.Sprite, PaintFence);
            yield return new ArtSpec("Streetlight", ArtKind.Sprite, PaintStreetlight);
            yield return new ArtSpec("Lantern", ArtKind.Sprite, PaintLantern);
            yield return new ArtSpec("Sign", ArtKind.Sprite, PaintSign);
            yield return new ArtSpec("Ember", ArtKind.CenteredSprite, PaintEmber);
            for (int frame = 0; frame < CampfireClips[0].Frames; frame++)
            {
                int f = frame;
                yield return new ArtSpec(CreatureArt.FrameName("Campfire", "idle", f), ArtKind.Sprite, () => PaintCampfire(f));
            }
        }

        private static Color32 Hex(string hex) => PixelCanvas.Hex(hex);

        private static PixelCanvas PaintBush()
        {
            var random = new Random(21);
            var canvas = new PixelCanvas(32, 20);
            var leaf = Hex("#4c8f3c");
            canvas.FillEllipse(16, 8, 14, 7.5f, leaf);
            canvas.FillEllipse(9, 10, 7, 6, leaf);
            canvas.FillEllipse(23, 10, 7, 6, leaf);
            canvas.FillEllipse(16, 13, 9, 5.5f, leaf);
            canvas.Speckle(leaf, Hex("#417d34"), 0.12f, random);
            canvas.ShadeEdges(leaf, Hex("#6db552"), Hex("#2f6229"), 2);
            foreach (var (x, y) in new[] { (8, 9), (14, 12), (21, 7), (25, 11), (18, 4) }) canvas.Set(x, y, Hex("#c94a4a"));
            canvas.Outline(Hex("#1e3320"));
            return canvas;
        }

        private static PixelCanvas PaintFlowers()
        {
            var random = new Random(5);
            var canvas = new PixelCanvas(24, 12);
            var stem = Hex("#3f7f33");
            string[] petals = { "#e85d5d", "#f2d14b", "#f5f0e6", "#a77be0", "#f08fb8" };

            // Grass tufts along the bottom.
            for (int x = 1; x < 23; x += 2)
                canvas.FillRect(x, 0, 1, 2 + random.Next(3), stem);

            for (int i = 0; i < 6; i++)
            {
                int x = 2 + i * 4 + random.Next(2);
                int height = 4 + random.Next(5);
                canvas.FillRect(x, 0, 1, height, stem);
                var petal = Hex(petals[random.Next(petals.Length)]);
                canvas.Set(x - 1, height, petal);
                canvas.Set(x + 1, height, petal);
                canvas.Set(x, height + 1, petal);
                canvas.Set(x, height - 1, petal);
                canvas.Set(x, height, Hex("#f7c948"));
            }
            canvas.Outline(Hex("#23401f"));
            return canvas;
        }

        private static PixelCanvas PaintMushrooms()
        {
            var canvas = new PixelCanvas(13, 10);
            var stalk = Hex("#efe6d6");
            var cap = Hex("#d24a3a");
            var dot = Hex("#fbf5ea");
            canvas.FillRect(3, 1, 2, 4, stalk);
            canvas.FillEllipse(4, 5.5f, 3.5f, 2.3f, cap);
            canvas.FillRect(9, 1, 1, 3, stalk);
            canvas.FillEllipse(9.5f, 3.8f, 2.5f, 1.6f, cap);
            foreach (var (x, y) in new[] { (2, 6), (5, 7), (4, 5), (9, 4) }) canvas.Set(x, y, dot);
            canvas.ShadeEdges(cap, Hex("#e8735f"), Hex("#a5382c"), 1);
            canvas.Outline(Hex("#3a2020"));
            return canvas;
        }

        private static PixelCanvas PaintFence()
        {
            var canvas = new PixelCanvas(32, 16);
            var post = Hex("#8a5a34");
            var rail = Hex("#9c6a3e");
            foreach (int y in new[] { 4, 9 }) canvas.FillRect(1, y, 30, 2, rail);
            foreach (int x in new[] { 1, 8, 15, 22, 29 })
            {
                canvas.FillRect(x, 1, 2, 12, post);
                canvas.Set(x, 13, post); // pointed top
            }
            canvas.ShadeEdges(post, Hex("#b07a46"), Hex("#6b4527"), 1);
            canvas.Outline(Hex("#2e2118"));
            return canvas;
        }

        private static PixelCanvas PaintStreetlight()
        {
            var canvas = new PixelCanvas(12, 48);
            var iron = Hex("#3c3f46");
            var glass = Hex("#ffe7a3");
            canvas.FillRect(3, 1, 6, 2, iron);   // base
            canvas.FillRect(5, 3, 2, 35, iron);  // pole
            canvas.FillRect(2, 38, 8, 1, iron);  // lamp bottom
            canvas.FillRect(3, 39, 6, 5, glass); // glass
            canvas.FillRect(5, 39, 2, 5, Hex("#fff6d6"));
            canvas.FillRect(2, 44, 8, 1, iron);  // lamp top
            canvas.FillRect(4, 45, 4, 1, iron);
            canvas.Set(5, 46, iron);
            canvas.Set(6, 46, iron);
            canvas.Outline(Hex("#1c1d22"));
            return canvas;
        }

        private static PixelCanvas PaintLantern()
        {
            var canvas = new PixelCanvas(10, 17);
            var wood = Hex("#4a3423");
            var frame = Hex("#3c3f46");
            canvas.FillRect(4, 1, 2, 7, wood);    // short post
            canvas.FillRect(2, 8, 6, 1, frame);   // lantern bottom
            canvas.FillRect(2, 9, 1, 5, frame);
            canvas.FillRect(7, 9, 1, 5, frame);
            canvas.FillRect(3, 9, 4, 5, Hex("#ffd27a"));
            canvas.FillRect(4, 10, 2, 3, Hex("#fff0c2"));
            canvas.FillRect(2, 14, 6, 1, frame);  // top
            canvas.FillRect(4, 15, 2, 1, frame);
            canvas.Outline(Hex("#1c1d22"));
            return canvas;
        }

        private static PixelCanvas PaintSign()
        {
            var canvas = new PixelCanvas(20, 20);
            var post = Hex("#7a5232");
            var board = Hex("#b07a46");
            canvas.FillRect(3, 1, 2, 9, post);
            canvas.FillRect(15, 1, 2, 9, post);
            canvas.FillRect(1, 8, 18, 10, board);
            canvas.ShadeEdges(board, Hex("#c99260"), Hex("#8a5e34"), 1);
            foreach (int y in new[] { 14, 11 }) canvas.FillRect(4, y, 12, 1, Hex("#5e3f24")); // painted "text"
            canvas.Outline(Hex("#2e2118"));
            return canvas;
        }

        private static PixelCanvas PaintEmber()
        {
            var canvas = new PixelCanvas(4, 4);
            canvas.FillRect(1, 1, 2, 2, new Color32(255, 255, 255, 255));
            foreach (var (x, y) in new[] { (0, 1), (0, 2), (3, 1), (3, 2), (1, 0), (2, 0), (1, 3), (2, 3) })
                canvas.Set(x, y, new Color32(255, 255, 255, 110));
            return canvas;
        }

        /// <summary>Stone ring and crossed logs, with one of three flame shapes on top.</summary>
        private static PixelCanvas PaintCampfire(int frame)
        {
            var canvas = new PixelCanvas(24, 22);
            var stone = Hex("#7f828c");
            canvas.FillEllipse(12, 3, 10.5f, 3.2f, stone);
            canvas.FillEllipse(12, 3.6f, 8f, 2.2f, Hex("#3a2a20")); // ash inside the ring
            canvas.ShadeEdges(stone, Hex("#a9acb5"), Hex("#5d6069"), 1);

            var log = Hex("#6b4a2f");
            for (int x = 6; x <= 17; x++)
            {
                int up = 2 + (x - 6) * 4 / 11;
                int down = 6 - (x - 6) * 4 / 11;
                canvas.FillRect(x, up, 1, 2, log);
                canvas.FillRect(x, down, 1, 2, Hex("#5a3d26"));
            }
            canvas.Outline(Hex("#2a1f1a"));

            // Flames are drawn after the outline, so they glow without a dark edge.
            int[] heights = { 13, 11, 14 };
            int height = heights[frame % heights.Length];
            PaintFlame(canvas, frame, height, 5.5f, Hex("#f08a24"));
            PaintFlame(canvas, frame, height - 3, 3.8f, Hex("#ffd35c"));
            PaintFlame(canvas, frame, height - 7, 1.8f, Hex("#fff4c2"));
            return canvas;
        }

        private static void PaintFlame(PixelCanvas canvas, int frame, int height, float maxHalfWidth, Color32 colour)
        {
            const int baseY = 5;
            for (int row = 0; row < height; row++)
            {
                float t = row / (float)height;
                float halfWidth = maxHalfWidth * Mathf.Pow(1f - t, 0.8f);
                int sway = Mathf.RoundToInt(Mathf.Sin(row * 0.55f + frame * 2.1f) * t * 1.5f);
                int from = Mathf.RoundToInt(12f - halfWidth) + sway;
                int to = Mathf.RoundToInt(12f + halfWidth) + sway;
                for (int x = from; x < to; x++) canvas.Set(x, baseY + row, colour);
            }
        }
    }
}

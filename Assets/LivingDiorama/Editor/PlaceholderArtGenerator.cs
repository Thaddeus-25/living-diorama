using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Random = System.Random;

namespace LivingDiorama.EditorTools
{
    /// <summary>
    /// Paints the original placeholder pixel art and writes it as PNGs.
    /// Existing PNGs are never overwritten, so you can replace any file with your own art
    /// (same name) and re-run the bootstrap safely.
    /// </summary>
    public static class PlaceholderArtGenerator
    {
        public const int PixelsPerUnit = 16;

        public enum ArtKind
        {
            /// <summary>Billboarded sprite, pivot at bottom centre.</summary>
            Sprite,
            /// <summary>Sprite with a centred pivot (e.g. the flat selection ring).</summary>
            CenteredSprite,
            /// <summary>Repeating ground texture.</summary>
            Tile,
            /// <summary>Smooth texture (no pixel filtering), e.g. the sky.</summary>
            Gradient
        }

        internal readonly struct ArtSpec
        {
            public readonly string Name;
            public readonly ArtKind Kind;
            public readonly Func<PixelCanvas> Paint;

            public ArtSpec(string name, ArtKind kind, Func<PixelCanvas> paint)
            {
                Name = name;
                Kind = kind;
                Paint = paint;
            }
        }

        private static readonly ArtSpec[] PropSpecs =
        {
            new("Tree", ArtKind.Sprite, PaintTree),
            new("Rock", ArtKind.Sprite, PaintRock),
            new("Bench", ArtKind.Sprite, PaintBench),
            new("HillsNear", ArtKind.Sprite, () => PaintHills(5, "#79a865", "#95c27c", "#6a9758", 196f, 14f, 8f, 4f, true)),
            new("HillsFar", ArtKind.Sprite, () => PaintHills(9, "#a6c3c4", "#bed6d6", "#98b7b9", 226f, 12f, 7f, 2f, false)),
            new("SelectionRing", ArtKind.CenteredSprite, PaintSelectionRing),
            new("ContactShadow", ArtKind.CenteredSprite, PaintContactShadow),
            new("Leaf", ArtKind.CenteredSprite, PaintLeaf),
            new("GrassTile", ArtKind.Tile, PaintGrassTile),
            new("DirtTile", ArtKind.Tile, PaintDirtTile),
            new("SkyGradient", ArtKind.Gradient, PaintSky),
        };

        /// <summary>Writes any missing PNGs into <paramref name="folder"/> and returns asset paths by name.</summary>
        public static Dictionary<string, string> GenerateAll(string folder)
        {
            var paths = new Dictionary<string, string>();
            foreach (var spec in PropSpecs.Concat(CreatureArt.Specs()))
            {
                string assetPath = $"{folder}/{spec.Name}.png";
                paths[spec.Name] = assetPath;
                if (File.Exists(assetPath)) continue;

                File.WriteAllBytes(assetPath, spec.Paint().EncodeToPng());
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
                ConfigureImporter(assetPath, spec.Kind);
                Debug.Log($"[PlaceholderArt] Generated {assetPath}");
            }
            return paths;
        }

        /// <summary>Import settings for pixel art: point filtering, no compression, 16 px per metre.</summary>
        public static void ConfigureImporter(string assetPath, ArtKind kind)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(assetPath);
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);

            settings.mipmapEnabled = false;
            settings.alphaIsTransparency = true;
            settings.filterMode = kind == ArtKind.Gradient ? FilterMode.Bilinear : FilterMode.Point;
            settings.wrapMode = kind == ArtKind.Tile ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;

            if (kind is ArtKind.Sprite or ArtKind.CenteredSprite)
            {
                settings.textureType = TextureImporterType.Sprite;
                settings.spriteMode = (int)SpriteImportMode.Single;
                settings.spritePixelsPerUnit = PixelsPerUnit;
                settings.spriteAlignment = (int)(kind == ArtKind.Sprite ? SpriteAlignment.BottomCenter : SpriteAlignment.Center);
                settings.spriteMeshType = SpriteMeshType.FullRect;
                settings.spriteExtrude = 0;
            }
            else
            {
                settings.textureType = TextureImporterType.Default;
            }

            importer.SetTextureSettings(settings);
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }

        // ---------------------------------------------------------------- props

        private static PixelCanvas PaintTree()
        {
            var random = new Random(11);
            var canvas = new PixelCanvas(48, 64);

            var trunk = PixelCanvas.Hex("#6b4a2f");
            canvas.FillRect(21, 1, 6, 24, trunk);
            canvas.FillRect(19, 1, 2, 2, trunk);
            canvas.FillRect(27, 1, 2, 2, trunk);
            canvas.ShadeEdges(trunk, PixelCanvas.Hex("#8a6240"), PixelCanvas.Hex("#4e3420"), 1);

            var leaf = PixelCanvas.Hex("#3f8f3a");
            canvas.FillEllipse(24, 42, 16, 12, leaf);
            canvas.FillEllipse(13, 35, 10, 8, leaf);
            canvas.FillEllipse(35, 35, 10, 8, leaf);
            canvas.FillEllipse(24, 52, 12, 9, leaf);
            canvas.FillEllipse(15, 47, 9, 7, leaf);
            canvas.FillEllipse(33, 47, 9, 7, leaf);
            canvas.Speckle(leaf, PixelCanvas.Hex("#357d33"), 0.08f, random);
            canvas.ShadeEdges(leaf, PixelCanvas.Hex("#6cbf4f"), PixelCanvas.Hex("#2d6b2e"), 2);

            canvas.Outline(PixelCanvas.Hex("#1e3320"));
            return canvas;
        }

        private static PixelCanvas PaintRock()
        {
            var random = new Random(7);
            var canvas = new PixelCanvas(24, 14);

            var stone = PixelCanvas.Hex("#8c8f99");
            canvas.FillEllipse(11, 2, 10.5f, 8.5f, stone);
            canvas.FillEllipse(18, 1.5f, 4.5f, 4f, stone);
            canvas.Speckle(stone, PixelCanvas.Hex("#7f828c"), 0.12f, random);
            canvas.ShadeEdges(stone, PixelCanvas.Hex("#b4b7c0"), PixelCanvas.Hex("#63666f"), 2);

            // Moss on the top edge.
            var moss = PixelCanvas.Hex("#6a9a4a");
            for (int x = 0; x < canvas.Width; x++)
            for (int y = canvas.Height - 1; y >= 0; y--)
            {
                if (!canvas.IsOpaque(x, y)) continue;
                if (y > 6 && random.NextDouble() < 0.6) canvas.Set(x, y, moss);
                break;
            }

            canvas.Outline(PixelCanvas.Hex("#3b3d44"));
            return canvas;
        }

        private static PixelCanvas PaintBench()
        {
            var canvas = new PixelCanvas(32, 22);
            var iron = PixelCanvas.Hex("#3c3f46");
            var wood = PixelCanvas.Hex("#9a6a3e");
            var woodLight = PixelCanvas.Hex("#b9844f");
            var woodDark = PixelCanvas.Hex("#6e4a2a");

            // Legs and back posts.
            canvas.FillRect(3, 1, 2, 8, iron);
            canvas.FillRect(27, 1, 2, 8, iron);
            canvas.FillRect(3, 11, 2, 10, iron);
            canvas.FillRect(27, 11, 2, 10, iron);

            // Seat.
            canvas.FillRect(1, 8, 30, 3, wood);
            canvas.FillRect(1, 10, 30, 1, woodLight);
            canvas.FillRect(1, 8, 30, 1, woodDark);

            // Back planks.
            foreach (int y in new[] { 13, 17 })
            {
                canvas.FillRect(1, y, 30, 2, wood);
                canvas.FillRect(1, y + 1, 30, 1, woodLight);
            }

            canvas.Outline(PixelCanvas.Hex("#2e2118"));
            return canvas;
        }

        private static PixelCanvas PaintSelectionRing()
        {
            // 32 px = 2 m across; scaled at runtime to fit the selected object.
            var canvas = new PixelCanvas(32, 32);
            var ring = PixelCanvas.Hex("#ffe7a3");
            var edge = PixelCanvas.Hex("#c98a2e");
            for (int y = 0; y < canvas.Height; y++)
            for (int x = 0; x < canvas.Width; x++)
            {
                float distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(16f, 16f));
                if (distance <= 15.5f && distance >= 13.5f) canvas.Set(x, y, ring);
                else if (distance < 13.5f && distance >= 12.5f) canvas.Set(x, y, edge);
            }
            return canvas;
        }

        /// <summary>A small white leaf; particles tint it green or yellow.</summary>
        private static PixelCanvas PaintLeaf()
        {
            var canvas = new PixelCanvas(8, 8);
            var leaf = new Color32(255, 255, 255, 255);
            var vein = new Color32(205, 205, 205, 255);
            string[] rows =
            {
                "#.......", // bottom row (stem)
                ".###....",
                ".####...",
                ".#####..",
                "..#####.",
                "...####.",
                "....###.",
                "......#.", // top row (tip)
            };
            var edge = new Color32(150, 140, 130, 255);
            for (int y = 0; y < rows.Length; y++)
            for (int x = 0; x < 8; x++)
                if (rows[y][x] == '#') canvas.Set(x, y, x == y ? vein : leaf);
            // Darken the outer pixels so the leaf reads against the grass once tinted.
            var copy = new bool[8, 8];
            for (int y = 0; y < 8; y++)
            for (int x = 0; x < 8; x++)
                copy[x, y] = canvas.IsOpaque(x, y);
            for (int y = 0; y < 8; y++)
            for (int x = 0; x < 8; x++)
            {
                if (!copy[x, y]) continue;
                bool border = x == 0 || y == 0 || x == 7 || y == 7
                              || !copy[x - 1, y] || !copy[x + 1, y] || !copy[x, y - 1] || !copy[x, y + 1];
                if (border && x != y) canvas.Set(x, y, edge);
            }
            return canvas;
        }

        /// <summary>
        /// Soft oval for under creatures' feet: white with alpha fading out in a few hard steps,
        /// which keeps the pixel-art look. The material tints it dark.
        /// </summary>
        private static PixelCanvas PaintContactShadow()
        {
            var canvas = new PixelCanvas(32, 16);
            for (int y = 0; y < canvas.Height; y++)
            for (int x = 0; x < canvas.Width; x++)
            {
                float dx = (x + 0.5f - 16f) / 16f;
                float dy = (y + 0.5f - 8f) / 8f;
                float distance = Mathf.Sqrt(dx * dx + dy * dy);
                if (distance >= 1f) continue;
                float alpha = Mathf.Ceil((1f - distance * distance) * 4f) / 4f; // 4 steps
                canvas.Set(x, y, new Color(1f, 1f, 1f, alpha));
            }
            return canvas;
        }

        // ---------------------------------------------------------------- backdrop

        private static PixelCanvas PaintHills(int seed, string bodyHex, string rimHex, string clumpHex,
            float baseHeight, float waveA, float waveB, float waveC, bool clumps)
        {
            var random = new Random(seed);
            var canvas = new PixelCanvas(448, 256);
            var body = PixelCanvas.Hex(bodyHex);
            var rim = PixelCanvas.Hex(rimHex);
            var clump = PixelCanvas.Hex(clumpHex);

            float phase = seed * 0.7f;
            for (int x = 0; x < canvas.Width; x++)
            {
                float height = baseHeight
                               + waveA * Mathf.Sin(x * 0.021f + phase)
                               + waveB * Mathf.Sin(x * 0.057f + 1.3f + phase)
                               + waveC * Mathf.Sin(x * 0.13f + 0.4f);
                int top = Mathf.Clamp(Mathf.RoundToInt(height), 0, canvas.Height - 12);
                canvas.FillRect(x, 0, 1, top, body);
                canvas.FillRect(x, top - 2, 1, 2, rim);

                // Little tree clumps along the ridge give the hills scale.
                if (clumps && random.NextDouble() < 0.035)
                {
                    float radius = 3f + (float)random.NextDouble() * 4f;
                    canvas.FillEllipse(x, top + radius * 0.4f, radius, radius * 0.9f, clump);
                }
            }

            // Soft horizontal bands lower down suggest fields.
            for (int y = 0; y < canvas.Height; y++)
            {
                if (y % 37 > 1) continue;
                for (int x = 0; x < canvas.Width; x++)
                    if (Same(canvas.Get(x, y), body)) canvas.Set(x, y, clump);
            }
            return canvas;
        }

        private static PixelCanvas PaintGrassTile()
        {
            var random = new Random(3);
            var canvas = new PixelCanvas(16, 16);
            var grass = PixelCanvas.Hex("#5aa046");
            canvas.Fill(grass);
            canvas.Speckle(grass, PixelCanvas.Hex("#4f9140"), 0.14f, random);
            canvas.Speckle(grass, PixelCanvas.Hex("#68b250"), 0.08f, random);

            var blade = PixelCanvas.Hex("#7cc35e");
            for (int i = 0; i < 6; i++)
            {
                int x = random.Next(16), y = random.Next(16);
                canvas.Set(x, y, blade);
                canvas.Set(x, (y + 1) % 16, blade); // wrap so the tile repeats seamlessly
            }
            return canvas;
        }

        private static PixelCanvas PaintDirtTile()
        {
            var random = new Random(4);
            var canvas = new PixelCanvas(16, 16);
            var dirt = PixelCanvas.Hex("#7a5233");
            canvas.Fill(dirt);
            canvas.Speckle(dirt, PixelCanvas.Hex("#6a4529"), 0.15f, random);
            canvas.Speckle(dirt, PixelCanvas.Hex("#8a6040"), 0.08f, random);
            for (int i = 0; i < 3; i++)
                canvas.Set(random.Next(16), random.Next(16), PixelCanvas.Hex("#9a8a7a"));
            return canvas;
        }

        private static PixelCanvas PaintSky()
        {
            var canvas = new PixelCanvas(4, 64);
            var top = (Color)PixelCanvas.Hex("#78b0e6");
            var middle = (Color)PixelCanvas.Hex("#b5d6ef");
            var bottom = (Color)PixelCanvas.Hex("#f1ecd8");
            for (int y = 0; y < canvas.Height; y++)
            {
                float t = y / (canvas.Height - 1f);
                var color = t < 0.5f ? Color.Lerp(bottom, middle, t * 2f) : Color.Lerp(middle, top, (t - 0.5f) * 2f);
                canvas.FillRect(0, y, canvas.Width, 1, color);
            }
            return canvas;
        }

        private static bool Same(Color32 a, Color32 b) => a.r == b.r && a.g == b.g && a.b == b.b && a.a == b.a;
    }
}

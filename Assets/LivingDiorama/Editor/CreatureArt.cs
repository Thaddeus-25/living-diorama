using System.Collections.Generic;
using UnityEngine;
using ArtKind = LivingDiorama.EditorTools.PlaceholderArtGenerator.ArtKind;
using ArtSpec = LivingDiorama.EditorTools.PlaceholderArtGenerator.ArtSpec;

namespace LivingDiorama.EditorTools
{
    /// <summary>
    /// Original placeholder pixel art for creatures, one PNG per animation frame
    /// (e.g. Human_walk_2.png). All creatures are drawn facing right; walking left mirrors them.
    /// </summary>
    internal static class CreatureArt
    {
        public readonly struct ClipSpec
        {
            public readonly string Name;
            public readonly int Frames;
            public readonly float FramesPerSecond;

            public ClipSpec(string name, int frames, float framesPerSecond)
            {
                Name = name;
                Frames = frames;
                FramesPerSecond = framesPerSecond;
            }
        }

        public static readonly ClipSpec[] HumanClips =
        {
            new("idle", 2, 1.5f),
            new("walk", 4, 8f),
            new("sit", 1, 1f),
        };

        public static readonly ClipSpec[] CatClips =
        {
            new("idle", 2, 1.2f),
            new("walk", 4, 10f),
            new("sit", 1, 1f),
            new("sleep", 2, 0.8f),
            new("stretch", 1, 1f),
        };

        public static readonly ClipSpec[] DogClips =
        {
            new("idle", 2, 3f), // tail wag
            new("walk", 4, 10f),
            new("sit", 1, 1f),
            new("sleep", 2, 0.8f),
        };

        public static string FrameName(string creature, string clip, int frame) => $"{creature}_{clip}_{frame}";

        public static IEnumerable<ArtSpec> Specs()
        {
            foreach (var clip in HumanClips)
                for (int frame = 0; frame < clip.Frames; frame++)
                {
                    string clipName = clip.Name;
                    int frameIndex = frame;
                    yield return new ArtSpec(FrameName("Human", clipName, frameIndex), ArtKind.Sprite, () => PaintHuman(clipName, frameIndex));
                }

            foreach (var clip in CatClips)
                for (int frame = 0; frame < clip.Frames; frame++)
                {
                    string clipName = clip.Name;
                    int frameIndex = frame;
                    yield return new ArtSpec(FrameName("Cat", clipName, frameIndex), ArtKind.Sprite, () => PaintCat(clipName, frameIndex));
                }

            foreach (var clip in DogClips)
                for (int frame = 0; frame < clip.Frames; frame++)
                {
                    string clipName = clip.Name;
                    int frameIndex = frame;
                    yield return new ArtSpec(FrameName("Dog", clipName, frameIndex), ArtKind.Sprite, () => PaintDog(clipName, frameIndex));
                }
        }

        // ------------------------------------------------------------------ dog (24 x 17 px, about 1.4 m nose to tail)

        private static readonly Color32 DogCoat = PixelCanvas.Hex("#b9824e");
        private static readonly Color32 DogCoatLight = PixelCanvas.Hex("#d9a876");
        private static readonly Color32 DogCoatShade = PixelCanvas.Hex("#8f5f34");
        private static readonly Color32 DogEar = PixelCanvas.Hex("#6b4527");
        private static readonly Color32 DogMuzzle = PixelCanvas.Hex("#e6c39a");
        private static readonly Color32 Collar = PixelCanvas.Hex("#c94a4a");

        private static PixelCanvas PaintDog(string clip, int frame)
        {
            var canvas = new PixelCanvas(24, 17);
            switch (clip)
            {
                case "sit": PaintDogSitting(canvas); break;
                case "sleep": PaintDogSleeping(canvas, breathingIn: frame == 1); break;
                default:
                    // Feet x positions: (back near, back far, front far, front near).
                    var feet = clip == "walk"
                        ? frame switch
                        {
                            0 => new[] { 6, 6, 13, 16 },
                            1 => new[] { 5, 7, 14, 15 },
                            2 => new[] { 4, 8, 15, 14 },
                            _ => new[] { 5, 7, 14, 15 }
                        }
                        : new[] { 5, 7, 14, 16 };
                    bool tailHigh = clip != "idle" || frame == 0;
                    PaintDogStanding(canvas, feet, tailHigh);
                    break;
            }
            canvas.Outline(PixelCanvas.Hex("#2e1f14"));
            return canvas;
        }

        private static void PaintDogStanding(PixelCanvas canvas, int[] feet, bool tailHigh)
        {
            // Far legs first, darker, behind the body.
            canvas.FillRect(feet[1], 1, 2, 5, DogCoatShade);
            canvas.FillRect(feet[2], 1, 2, 5, DogCoatShade);

            // Tail: up for a wag, out to the side on the other frame.
            var tail = tailHigh
                ? new[] { (4, 8), (3, 9), (3, 10), (2, 11), (2, 12) }
                : new[] { (4, 8), (3, 8), (2, 9), (1, 9), (0, 10) };
            foreach (var (x, y) in tail) canvas.Set(x, y, DogCoat);

            canvas.FillEllipse(11f, 7.5f, 7f, 3f, DogCoat);
            canvas.FillEllipse(12f, 6.2f, 4.5f, 1.2f, DogCoatLight); // belly

            canvas.FillRect(feet[0], 1, 2, 5, DogCoat);
            canvas.FillRect(feet[3], 1, 2, 5, DogCoat);

            PaintDogHead(canvas, centerX: 18.5f, centerY: 11f, eyesOpen: true);
            canvas.ShadeEdges(DogCoat, DogCoatLight, DogCoatShade, 1);
        }

        private static void PaintDogSitting(PixelCanvas canvas)
        {
            foreach (var (x, y) in new[] { (6, 1), (5, 1), (4, 1), (3, 2) }) canvas.Set(x, y, DogCoat); // tail on the ground
            canvas.FillEllipse(10f, 5.5f, 4.2f, 4.8f, DogCoat);
            canvas.FillEllipse(12f, 5f, 1.6f, 3f, DogCoatLight); // chest
            canvas.FillRect(12, 1, 2, 4, DogCoat);
            canvas.FillRect(14, 1, 1, 3, DogCoatShade);
            PaintDogHead(canvas, centerX: 13.5f, centerY: 11.5f, eyesOpen: true);
            canvas.ShadeEdges(DogCoat, DogCoatLight, DogCoatShade, 1);
        }

        private static void PaintDogSleeping(PixelCanvas canvas, bool breathingIn)
        {
            canvas.FillEllipse(11f, 3.2f, 7.5f, breathingIn ? 3f : 2.7f, DogCoat);
            canvas.FillRect(5, 1, 11, 1, DogCoatShade); // tail and paws tucked in front
            PaintDogHead(canvas, centerX: 17.5f, centerY: 3.8f, eyesOpen: false);
            canvas.ShadeEdges(DogCoat, DogCoatLight, DogCoatShade, 1);
        }

        private static void PaintDogHead(PixelCanvas canvas, float centerX, float centerY, bool eyesOpen)
        {
            int cx = Mathf.RoundToInt(centerX), cy = Mathf.RoundToInt(centerY);
            canvas.FillRect(cx - 3, cy - 3, 2, 2, Collar);
            canvas.FillEllipse(centerX, centerY, 3.2f, 2.8f, DogCoat);
            canvas.FillRect(cx + 1, cy - 2, 3, 2, DogMuzzle); // snout
            canvas.Set(cx + 4, cy - 1, Eye);                 // nose
            canvas.FillRect(cx - 2, cy - 2, 2, 4, DogEar);    // floppy ear
            if (eyesOpen) canvas.Set(cx + 1, cy + 1, Eye);
            else canvas.FillRect(cx, cy + 1, 2, 1, DogCoatShade);
        }

        // ------------------------------------------------------------------ human (16 x 30 px, about 1.7 m)

        private static readonly Color32 Skin = PixelCanvas.Hex("#f0c39b");
        private static readonly Color32 SkinShade = PixelCanvas.Hex("#d9a27a");
        private static readonly Color32 Hair = PixelCanvas.Hex("#5a3a26");
        private static readonly Color32 Shirt = PixelCanvas.Hex("#5b8fd1");
        private static readonly Color32 ShirtShade = PixelCanvas.Hex("#4672ad");
        private static readonly Color32 Pants = PixelCanvas.Hex("#3f4560");
        private static readonly Color32 PantsShade = PixelCanvas.Hex("#31354b");
        private static readonly Color32 Shoe = PixelCanvas.Hex("#2b2220");
        private static readonly Color32 Eye = PixelCanvas.Hex("#2a1d16");

        private static PixelCanvas PaintHuman(string clip, int frame)
        {
            var canvas = new PixelCanvas(16, 30);
            if (clip == "sit") PaintHumanSitting(canvas);
            else
            {
                // (back foot x, front foot x, back hand x, front hand x, upper-body drop)
                var (backFoot, frontFoot, backHand, frontHand, drop) = clip switch
                {
                    "walk" => frame switch
                    {
                        0 => (6, 8, 7, 8, 0),
                        1 => (4, 10, 10, 5, 0),
                        2 => (8, 6, 8, 7, 0),
                        _ => (10, 4, 5, 10, 0)
                    },
                    _ => (6, 8, 7, 9, frame == 1 ? 1 : 0) // idle: gentle breathing
                };
                PaintHumanStanding(canvas, backFoot, frontFoot, backHand, frontHand, drop);
            }
            canvas.Outline(PixelCanvas.Hex("#241a17"));
            return canvas;
        }

        private static void PaintHumanStanding(PixelCanvas canvas, int backFoot, int frontFoot, int backHand, int frontHand, int drop)
        {
            // Back limbs first so the torso covers their tops.
            DrawLimb(canvas, 7, 16 - drop, backHand, 10 - drop, 2, ShirtShade);
            canvas.FillRect(backHand, 10 - drop, 2, 1, SkinShade);
            DrawLimb(canvas, 6, 8, backFoot, 1, 2, PantsShade);
            DrawLimb(canvas, 8, 8, frontFoot, 1, 2, Pants);
            canvas.FillRect(backFoot, 1, 3, 1, Shoe);
            canvas.FillRect(frontFoot, 1, 3, 1, Shoe);

            PaintUpperBody(canvas, torsoBottom: 9 - drop);

            DrawLimb(canvas, 8, 16 - drop, frontHand, 10 - drop, 2, Shirt);
            canvas.FillRect(frontHand, 10 - drop, 2, 1, Skin);
        }

        /// <summary>
        /// Seated, facing the camera, so one frame works on any seat without mirroring.
        /// Hips sit at bench-seat height; the thighs point at the viewer, so only the knees show.
        /// </summary>
        private static void PaintHumanSitting(PixelCanvas canvas)
        {
            // Shins hanging down from the knees, with shoes.
            canvas.FillRect(5, 2, 2, 7, Pants);
            canvas.FillRect(9, 2, 2, 7, Pants);
            canvas.FillRect(6, 2, 1, 7, PantsShade);
            canvas.FillRect(10, 2, 1, 7, PantsShade);
            canvas.FillRect(5, 1, 2, 1, Shoe);
            canvas.FillRect(9, 1, 2, 1, Shoe);

            // Knees (foreshortened thighs) and lap.
            canvas.FillRect(4, 9, 8, 2, Pants);
            canvas.FillRect(4, 9, 8, 1, PantsShade);

            // Arms at the sides, behind the torso edges.
            canvas.FillRect(3, 12, 1, 7, ShirtShade);
            canvas.FillRect(12, 12, 1, 7, ShirtShade);

            // Torso with belt; light comes from the left, so the right edge is shaded.
            canvas.FillRect(4, 11, 8, 8, Shirt);
            canvas.FillRect(11, 12, 1, 7, ShirtShade);
            canvas.FillRect(4, 11, 8, 1, PantsShade);

            // Hands resting on the lap.
            canvas.FillRect(4, 11, 2, 1, Skin);
            canvas.FillRect(10, 11, 2, 1, SkinShade);

            // Neck and head, front view: two eyes, hair framing the face.
            canvas.FillRect(7, 19, 2, 1, SkinShade);
            canvas.FillRect(5, 20, 6, 6, Skin);
            canvas.FillRect(10, 20, 1, 5, SkinShade);
            canvas.Set(6, 22, Eye);
            canvas.Set(9, 22, Eye);
            canvas.FillRect(7, 21, 2, 1, SkinShade); // mouth

            canvas.FillRect(5, 25, 6, 2, Hair);
            canvas.FillRect(4, 21, 1, 5, Hair);
            canvas.FillRect(11, 21, 1, 5, Hair);
            canvas.FillRect(6, 27, 4, 1, Hair);
        }

        /// <summary>Torso (8 px), neck and head with hair. <paramref name="torsoBottom"/> is the belt row.</summary>
        private static void PaintUpperBody(PixelCanvas canvas, int torsoBottom)
        {
            int y = torsoBottom;
            canvas.FillRect(5, y, 6, 8, Shirt);
            canvas.FillRect(5, y + 1, 1, 7, ShirtShade);
            canvas.FillRect(5, y, 6, 1, PantsShade); // belt

            canvas.FillRect(7, y + 8, 2, 1, SkinShade); // neck
            canvas.FillRect(6, y + 9, 6, 6, Skin);      // head
            canvas.Set(12, y + 11, Skin);               // nose
            canvas.Set(10, y + 12, Eye);

            canvas.FillRect(6, y + 14, 6, 2, Hair);
            canvas.FillRect(5, y + 10, 2, 6, Hair);
            canvas.FillRect(7, y + 16, 4, 1, Hair);
        }

        /// <summary>Straight limb from (fromX, fromY) to (toX, toY), <paramref name="width"/> px wide, one row at a time.</summary>
        private static void DrawLimb(PixelCanvas canvas, int fromX, int fromY, int toX, int toY, int width, Color32 color)
        {
            int minY = Mathf.Min(fromY, toY), maxY = Mathf.Max(fromY, toY);
            for (int y = minY; y <= maxY; y++)
            {
                float t = maxY == minY ? 0f : (y - toY) / (float)(fromY - toY);
                int x = Mathf.RoundToInt(Mathf.Lerp(toX, fromX, t));
                canvas.FillRect(x, y, width, 1, color);
            }
        }

        // ------------------------------------------------------------------ cat (20 x 15 px, about 1.2 m nose to tail)

        private static readonly Color32 Fur = PixelCanvas.Hex("#e39b4f");
        private static readonly Color32 FurLight = PixelCanvas.Hex("#f3b874");
        private static readonly Color32 FurShade = PixelCanvas.Hex("#c47a35");
        private static readonly Color32 Stripe = PixelCanvas.Hex("#b86a2b");
        private static readonly Color32 Belly = PixelCanvas.Hex("#f6d3a3");
        private static readonly Color32 Nose = PixelCanvas.Hex("#e88a8a");

        private static PixelCanvas PaintCat(string clip, int frame)
        {
            var canvas = new PixelCanvas(20, 15);
            switch (clip)
            {
                case "sit": PaintCatSitting(canvas); break;
                case "sleep": PaintCatSleeping(canvas, breathingIn: frame == 1); break;
                case "stretch": PaintCatStretching(canvas); break;
                default:
                    // Feet x positions: (back near, back far, front far, front near).
                    var feet = clip == "walk"
                        ? frame switch
                        {
                            0 => new[] { 5, 5, 10, 14 },
                            1 => new[] { 4, 6, 11, 13 },
                            2 => new[] { 3, 7, 12, 12 },
                            _ => new[] { 4, 6, 11, 13 }
                        }
                        : new[] { 4, 6, 11, 13 };
                    bool tailFlick = clip == "idle" && frame == 1;
                    PaintCatStanding(canvas, feet, tailFlick, walking: clip == "walk");
                    break;
            }
            canvas.Outline(PixelCanvas.Hex("#3a2516"));
            return canvas;
        }

        private static void PaintCatStanding(PixelCanvas canvas, int[] feet, bool tailFlick, bool walking)
        {
            // Far legs (darker) behind the body.
            canvas.FillRect(feet[1], 1, 1, 4, FurShade);
            canvas.FillRect(feet[2], 1, 1, 4, FurShade);

            if (walking)
                foreach (var (x, y) in new[] { (3, 6), (2, 7), (1, 8), (1, 9) }) canvas.Set(x, y, Fur);
            else if (tailFlick)
                foreach (var (x, y) in new[] { (3, 6), (2, 7), (2, 8), (1, 9), (1, 10) }) canvas.Set(x, y, Fur);
            else
                foreach (var (x, y) in new[] { (3, 6), (2, 7), (2, 8), (2, 9), (3, 10) }) canvas.Set(x, y, Fur);

            canvas.FillEllipse(9.5f, 5.5f, 6f, 2.6f, Fur);
            canvas.FillEllipse(10f, 4.2f, 4f, 1f, Belly);
            foreach (int x in new[] { 7, 9, 11 }) canvas.FillRect(x, 6, 1, 2, Stripe);

            canvas.FillRect(feet[0], 1, 1, 4, Fur);
            canvas.FillRect(feet[3], 1, 1, 4, Fur);

            PaintCatHead(canvas, centerX: 15.5f, centerY: 7.5f, eyesOpen: true);
            canvas.ShadeEdges(Fur, FurLight, FurShade, 1);
        }

        private static void PaintCatSitting(PixelCanvas canvas)
        {
            foreach (var (x, y) in new[] { (5, 1), (4, 1), (3, 1), (2, 2) }) canvas.Set(x, y, Fur); // tail on the ground
            canvas.FillEllipse(9f, 4.5f, 3.6f, 3.8f, Fur);
            canvas.FillEllipse(10.6f, 4.2f, 1.4f, 2.4f, Belly);
            canvas.FillRect(10, 1, 1, 2, Fur);
            canvas.FillRect(12, 1, 1, 2, Fur);
            PaintCatHead(canvas, centerX: 11.5f, centerY: 9.8f, eyesOpen: true);
            canvas.ShadeEdges(Fur, FurLight, FurShade, 1);
        }

        private static void PaintCatSleeping(PixelCanvas canvas, bool breathingIn)
        {
            canvas.FillEllipse(10f, 3f, 6f, breathingIn ? 2.9f : 2.6f, Fur);
            canvas.FillRect(5, 1, 10, 1, FurShade); // tail wrapped around the front
            foreach (int x in new[] { 7, 9 }) canvas.FillRect(x, 3, 1, 2, Stripe);
            PaintCatHead(canvas, centerX: 14.5f, centerY: 3.6f, eyesOpen: false);
            canvas.ShadeEdges(Fur, FurLight, FurShade, 1);
        }

        private static void PaintCatStretching(PixelCanvas canvas)
        {
            foreach (var (x, y) in new[] { (3, 7), (2, 8), (2, 9), (2, 10), (3, 11) }) canvas.Set(x, y, Fur); // tail up
            canvas.FillRect(4, 1, 1, 5, FurShade);
            canvas.FillRect(6, 1, 1, 5, Fur);
            canvas.FillEllipse(9.5f, 5f, 5.8f, 2.2f, Fur);
            canvas.FillRect(12, 1, 5, 1, Fur); // front legs stretched forward
            canvas.FillRect(12, 2, 3, 1, FurShade);
            PaintCatHead(canvas, centerX: 16.5f, centerY: 4f, eyesOpen: false);
            canvas.ShadeEdges(Fur, FurLight, FurShade, 1);
        }

        private static void PaintCatHead(PixelCanvas canvas, float centerX, float centerY, bool eyesOpen)
        {
            canvas.FillEllipse(centerX, centerY, 3f, 2.5f, Fur);
            int cx = Mathf.RoundToInt(centerX), cy = Mathf.RoundToInt(centerY);

            // Ears.
            canvas.Set(cx - 2, cy + 2, Fur);
            canvas.Set(cx - 2, cy + 3, Fur);
            canvas.Set(cx + 1, cy + 2, Fur);
            canvas.Set(cx + 1, cy + 3, Fur);

            if (eyesOpen) canvas.Set(cx + 1, cy, Eye);
            else canvas.FillRect(cx, cy, 2, 1, Stripe);
            canvas.Set(cx + 3, cy - 1, Nose);
        }
    }
}

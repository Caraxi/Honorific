using Dalamud.Game.Text.SeStringHandling;
using FFXIVClientStructs.FFXIV.Component.GUI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Honorific
{
    public enum PaintType
    {
        Static,
        Alternating,
        GradientRGB,
        GradientLAB,
        RainbowCW,
        RainbowCCW
    }

    public class Gradients {


        public static Vector3 GradientLAB(Vector3 color1, Vector3 color2, float ratio)
        {
            // Convert RGB to LAB
            var lab1 = RgbToLab(color1);
            var lab2 = RgbToLab(color2);

            // Interpolation
            float l = lab1.X + (lab2.X - lab1.X) * ratio;
            float a = lab1.Y + (lab2.Y - lab1.Y) * ratio;
            float b = lab1.Z + (lab2.Z - lab1.Z) * ratio;

            // Convert back to RGB
            return LabToRgb(new Vector3(l, a, b));
        }

        // Helper: RGB to LAB
        private static Vector3 RgbToLab(Vector3 rgb)
        {
            // Convert RGB [0,1] to XYZ
            float r = PivotRgb(rgb.X);
            float g = PivotRgb(rgb.Y);
            float b = PivotRgb(rgb.Z);

            // Observer = 2°, Illuminant = D65
            float x = r * 0.4124f + g * 0.3576f + b * 0.1805f;
            float y = r * 0.2126f + g * 0.7152f + b * 0.0722f;
            float z = r * 0.0193f + g * 0.1192f + b * 0.9505f;

            // Normalize for D65 white point
            x /= 0.95047f;
            y /= 1.00000f;
            z /= 1.08883f;

            x = PivotLab(x);
            y = PivotLab(y);
            z = PivotLab(z);

            float l = 116f * y - 16f;
            float a = 500f * (x - y);
            float bLab = 200f * (y - z);

            return new Vector3(l, a, bLab);
        }

        private static float PivotRgb(float n)
        {
            return (n > 0.04045f) ? MathF.Pow((n + 0.055f) / 1.055f, 2.4f) : n / 12.92f;
        }

        private static float PivotLab(float n)
        {
            return (n > 0.008856f) ? MathF.Pow(n, 1f / 3f) : (7.787f * n) + (16f / 116f);
        }

        // Helper: LAB to RGB
        private static Vector3 LabToRgb(Vector3 lab)
        {
            float y = (lab.X + 16f) / 116f;
            float x = lab.Y / 500f + y;
            float z = y - lab.Z / 200f;

            x = 0.95047f * InversePivotLab(x);
            y = 1.00000f * InversePivotLab(y);
            z = 1.08883f * InversePivotLab(z);

            float r = x * 3.2406f + y * -1.5372f + z * -0.4986f;
            float g = x * -0.9689f + y * 1.8758f + z * 0.0415f;
            float b = x * 0.0557f + y * -0.2040f + z * 1.0570f;

            r = InversePivotRgb(r);
            g = InversePivotRgb(g);
            b = InversePivotRgb(b);

            // Clamp to [0,1]
            r = Math.Clamp(r, 0f, 1f);
            g = Math.Clamp(g, 0f, 1f);
            b = Math.Clamp(b, 0f, 1f);

            return new Vector3(r, g, b);
        }

        private static float InversePivotLab(float n)
        {
            float n3 = n * n * n;
            return (n3 > 0.008856f) ? n3 : (n - 16f / 116f) / 7.787f;
        }

        private static float InversePivotRgb(float n)
        {
            return (n > 0.0031308f) ? 1.055f * MathF.Pow(n, 1f / 2.4f) - 0.055f : 12.92f * n;
        }

        public static Vector3 GradientRGB(Vector3 color1, Vector3 color2, float ratio)
        {
            // Interpolate between two RGB colors
            return new Vector3(
                color1.X * (1 - ratio) + color2.X * ratio,
                color1.Y * (1 - ratio) + color2.Y * ratio,
                color1.Z * (1 - ratio) + color2.Z * ratio
            );
        }

        // Helper: RGB [0,1] to HSV
        private static Vector3 RgbToHsv(Vector3 rgb)
        {
            float r = rgb.X, g = rgb.Y, b = rgb.Z;
            float max = MathF.Max(r, MathF.Max(g, b));
            float min = MathF.Min(r, MathF.Min(g, b));
            float h, s, v = max;

            float d = max - min;
            s = max == 0 ? 0 : d / max;

            if (d == 0)
                h = 0;
            else if (max == r)
                h = 60f * (((g - b) / d) % 6f);
            else if (max == g)
                h = 60f * (((b - r) / d) + 2f);
            else
                h = 60f * (((r - g) / d) + 4f);

            if (h < 0) h += 360f;
            return new Vector3(h, s, v);
        }

        // Helper: HSV to RGB [0,1]
        private static Vector3 HsvToRgb(Vector3 hsv)
        {
            float h = hsv.X, s = hsv.Y, v = hsv.Z;
            float c = v * s;
            float x = c * (1 - MathF.Abs((h / 60f) % 2 - 1));
            float m = v - c;
            float r, g, b;

            if (h < 60f) { r = c; g = x; b = 0; }
            else if (h < 120f) { r = x; g = c; b = 0; }
            else if (h < 180f) { r = 0; g = c; b = x; }
            else if (h < 240f) { r = 0; g = x; b = c; }
            else if (h < 300f) { r = x; g = 0; b = c; }
            else { r = c; g = 0; b = x; }

            return new Vector3(r + m, g + m, b + m);
        }

        // Use HSV to rotate hue clockwise using ratio
        public static Vector3 GradientRainbowCW(Vector3 color1, Vector3 color2, float ratio)
        {
            if (ratio <= 0) { return color1; }
            if (ratio >= 1) { return color2; }

            var hsv1 = RgbToHsv(color1);
            var hsv2 = RgbToHsv(color2);

            // Calculate shortest CW hue rotation
            float h1 = hsv1.X;
            float h2 = hsv2.X;
            float delta = ((h2 - h1 + 360f) % 360f);

            float h = (h1 + delta * ratio) % 360f;
            float s = hsv1.Y * (1 - ratio) + hsv2.Y * ratio;
            float v = hsv1.Z * (1 - ratio) + hsv2.Z * ratio;

            return HsvToRgb(new Vector3(h, s, v));
        }

        // Use HSV to rotate hue counter-clockwise using ratio
        public static Vector3 GradientRainbowCCW(Vector3 color1, Vector3 color2, float ratio)
        {
            if (ratio <= 0) { return color1; }
            if (ratio >= 1) { return color2; }

            var hsv1 = RgbToHsv(color1);
            var hsv2 = RgbToHsv(color2);

            // Calculate shortest CCW hue rotation
            float h1 = hsv1.X;
            float h2 = hsv2.X;
            float delta = ((h1 - h2 + 360f) % 360f);

            float h = (h1 - delta * ratio + 360f) % 360f;
            float s = hsv1.Y * (1 - ratio) + hsv2.Y * ratio;
            float v = hsv1.Z * (1 - ratio) + hsv2.Z * ratio;

            return HsvToRgb(new Vector3(h, s, v));
        }
    }

    public class Paint
    {
        public PaintType Type { get; set; } = PaintType.Static;
        public Vector3 Color1 { get; set; }
        public Vector3? Color2 { get; set; }
        public uint Length { get; set; } = 0; // If length is 0, cover the entire title
        

        
        public bool PaintCharacter(float ratio, out Vector3 Color)
        {
            switch (Type)
            {
                case PaintType.Static:
                    Color = Color1;
                    return true;
                case PaintType.Alternating:
                    Color = (ratio > 0.5f) ? Color1 : Color2 ?? Color1;
                    return true;
                case PaintType.GradientRGB:
                    Color = Gradients.GradientRGB(Color1, Color2 ?? Color1, ratio);
                    return true;
                case PaintType.GradientLAB:
                    Color = Gradients.GradientLAB(Color1, Color2 ?? Color1, ratio);
                    return true;
                case PaintType.RainbowCW:
                    Color = Gradients.GradientRainbowCW(Color1, Color2 ?? Color1, ratio);
                    return true;
                case PaintType.RainbowCCW:
                    Color = Gradients.GradientRainbowCCW(Color1, Color2 ?? Color1, ratio);
                    return true;
                default:
                    Color = Vector3.Zero;
                    return false;
            }   
        }
    }

    public class Palette
    {
        public string Name { get; set; } = string.Empty;
        public string UniqueId { get; set; } = string.Empty;
        public List<Paint> Paints { get; set; } = [];

        public static Palette TestingPalette()
        {
            var palette = new Palette
            {
                Name = "Test Palette",
                UniqueId = "test_palette_001",
                Paints = new List<Paint>
                {
                    new Paint
                    {
                        Type = PaintType.GradientLAB,
                        Color1 = new Vector3(0f, 1f, 1f), 
                        Color2 = new Vector3(1f, 0f, 0f), // Red
                        Length = 0
                    }
                }
            };
            return palette;
        }

        public static Vector3 ParseVector3(string s)
        {
            // format is "X,Y,Z"
            var parts = s.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 3)
                throw new FormatException($"Format Vector3 invalide: {s}");
            float x, y, z;
            x = float.Parse(parts[0], System.Globalization.CultureInfo.InvariantCulture);
            y = float.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture);
            z = float.Parse(parts[2], System.Globalization.CultureInfo.InvariantCulture);
            return new Vector3(
                x,y,z
            );
        }

        public static string ExportPalette(Palette palette)
        {
            var sb = new StringBuilder();
            sb.Append(palette.Name).Append(":");
            sb.Append(palette.UniqueId).Append(":");
            sb.Append(palette.Paints.Count).Append(":");
            foreach (var paint in palette.Paints)
            {
                sb.Append(paint.Type.ToString()).Append(":");
                sb.Append($"{paint.Color1.X},{paint.Color1.Y},{paint.Color1.Z}").Append(":");
                sb.Append(paint.Color2.HasValue ? $"{paint.Color2.Value.X},{paint.Color2.Value.Y},{paint.Color2.Value.Z}" : "null").Append(":");
                sb.Append(paint.Length).Append(":");
            }
            return sb.ToString();
        }

        // Palette format : // Name:UniqueId:PaintType:Color1:Color2:Length:PaintType:(...)

        public static Palette ImportPalette(string palette_string)
        {
            try { 
                var palette = new Palette();
                var lines = palette_string.Split(":");
                palette.Name = lines[0];
                palette.UniqueId = lines[1];
                int paintCount = int.Parse(lines[2]);
                int offset = 3;
                for (int i = 0; i < paintCount; i++)
                {
                    var paint = new Paint();
                    paint.Type = Enum.Parse<PaintType>(lines[offset + i * 4]);
                    paint.Color1 = ParseVector3(lines[offset + i * 4 + 1]);
                    if (lines[offset + i * 4 + 2] != "null")
                        paint.Color2 = ParseVector3(lines[offset + i * 4 + 2]);
                    else
                        paint.Color2 = null;
                    if (uint.TryParse(lines[offset + i * 4 + 3], out var length))
                        paint.Length = length;

                    PluginService.Chat.Print($"Importing paint: {paint.Type}, Color1: {paint.Color1}, Color2: {paint.Color2}, Length: {paint.Length}");
                    palette.Paints.Add(paint);
                }

                return palette;
            } 
            catch
            {
                return null;
            }
        }

        public static SeString PaintSeString(string title, Palette palette, bool includeQuotes = true)
        {
            var builder = new SeStringBuilder();
            int start_char = 0;
            var characters = title.ToCharArray();

            if (includeQuotes) builder.AddText("《");
            if (string.IsNullOrEmpty(title)) return builder.Build().Cleanup();

            foreach (var paint in palette.Paints)
            {
                if (paint.Type == PaintType.Static)
                    builder.Add(new ColorPayload(paint.Color1));

                int segmentLength = paint.Length == 0 ? characters.Length - start_char : (int)paint.Length;
                int end_char = start_char + segmentLength;

                for (int i = start_char; i < end_char && i < characters.Length; i++)
                {
                    float progress;
                    if (paint.Type == PaintType.Alternating)
                        progress = ((i - start_char) % 2 == 0) ? 0f : 1f;
                    else
                        progress = segmentLength <= 1 ? 0f : (float)(i - start_char) / (segmentLength - 1);

                    if (paint.PaintCharacter(progress, out var color))
                    {
                        if (paint.Type != PaintType.Static)
                            builder.Add(new ColorPayload(color));
                        builder.AddText(characters[i].ToString());
                        if (paint.Type != PaintType.Static)
                            builder.Add(new ColorEndPayload());
                    }
                    else
                    {
                        builder.AddText(characters[i].ToString());
                    }
                }

                if (paint.Type == PaintType.Static)
                    builder.Add(new ColorEndPayload());

                start_char = end_char;
            }

            if (includeQuotes) builder.AddText("》");
            return builder.Build().Cleanup();
        }
    }
}


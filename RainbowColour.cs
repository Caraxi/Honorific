global using RGB = (byte R, byte G, byte B);
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.ImGuiSeStringRenderer;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using FFXIVClientStructs.FFXIV.Client.System.Framework;

namespace Honorific;
public static unsafe class RainbowColour {
    public record RainbowStyle(string Name, byte[,] Colours, int ChrMultiplier) {
        public RainbowStyle(string name, string b64, int chrMultipler) : this(name, Decode(b64), chrMultipler) { }
        private static byte[,] Decode(string b64) {
            var arr = Convert.FromBase64String(b64);
            var arr2 = new byte[arr.Length / 3, 3];
            for (var i = 0; i < arr.Length; i += 3) {
                arr2[i / 3, 0] =  arr[i];
                arr2[i / 3, 1] =  arr[i + 1];
                arr2[i / 3, 2] =  arr[i + 2];
            }

            return arr2;
        }
    }
    
    private static readonly List<RainbowStyle> ColourLists = [
        new("Pride Rainbow (Wave)", "5AMD6RsC7TMC8ksB92MB/HsA/5EA/6IA/7IA/8MA/9QA/+UA5+MEutAKjr0RYaoYNZYeCIMlAHlFAG9rAGaRAF23AFTdAkv9FkXnKj/RPjm8UjOmZi2QcymCcymCcymCcymCcymCcymCZi2QUjOmPjm8Kj/RFkXnAkv9AFTdAF23AGaRAG9rAHlFCIMlNZYeYaoYjr0RutAK5+ME/+UA/9QA/8MA/7IA/6IA/5EA/HsA92MB8ksB7TMC6RsC5AMD", 1),
        new("Pride Rainbow (Pulse)", "5AMD6RsC7TMC8ksB92MB/HsA/5EA/6IA/7IA/8MA/9QA/+UA5+MEutAKjr0RYaoYNZYeCIMlAHlFAG9rAGaRAF23AFTdAkv9FkXnKj/RPjm8UjOmZi2QcymCcymCcymCcymCcymCcymCZi2QUjOmPjm8Kj/RFkXnAkv9AFTdAF23AGaRAG9rAHlFCIMlNZYeYaoYjr0RutAK5+ME/+UA/9QA/8MA/7IA/6IA/5EA/HsA92MB8ksB7TMC6RsC5AMD", 0),
        new("Transgender (Wave)", "W876b8nygsXplsDhqbvYvbfQ0LLI5K2/9aq59rXC+MDL+cvU+tbd/OHm/ezv/vf4//z9/fH0/Obr+9zi+tHZ+MbQ97vH9rC+7qu72q/Ex7TMs7nUn77djMLleMftZcz2Zcz2eMftjMLln77ds7nUx7TM2q/E7qu79rC+97vH+MbQ+tHZ+9zi/Obr/fH0//z9/vf4/ezv/OHm+tbd+cvU+MDL9rXC9aq55K2/0LLIvbfQqbvYlsDhgsXpb8nyW876", 1),
        new("Transgender (Pulse)", "W876b8nygsXplsDhqbvYvbfQ0LLI5K2/9aq59rXC+MDL+cvU+tbd/OHm/ezv/vf4//z9/fH0/Obr+9zi+tHZ+MbQ97vH9rC+7qu72q/Ex7TMs7nUn77djMLleMftZcz2Zcz2eMftjMLln77ds7nUx7TM2q/E7qu79rC+97vH+MbQ+tHZ+9zi/Obr/fH0//z9/vf4/ezv/OHm+tbd+cvU+MDL9rXC9aq55K2/0LLIvbfQqbvYlsDhgsXpb8nyW876", 0),
        new("Lesbian (Wave)", "1S0A2lQT33ol46E46MdL7e5d8Opg9Nhe98Zc+rVZ/aNX/6Rm/7eG/8qm/93H//Hn/fj79Nnp67vY4p3G2n+10WKkzGCgxl2cwVuZvFmVtleRskqJrzqBrCp4qBpvpQpmpQpmqBpvrCp4rzqBskqJtleRvFmVwVuZxl2czGCg0WKk2oC1457H67zY9Nrp/fj7//Hn/93H/8qm/7eG/6Rm/aNX+rVZ98dc9Nhe8Opg7exd6MZK46A43nkl2lMT1S0A", 1),
        new("Lesbian (Pulse)", "1S0A2lQT33ol46E46MdL7e5d8Opg9Nhe98Zc+rVZ/aNX/6Rm/7eG/8qm/93H//Hn/fj79Nnp67vY4p3G2n+10WKkzGCgxl2cwVuZvFmVtleRskqJrzqBrCp4qBpvpQpmpQpmqBpvrCp4rzqBskqJtleRvFmVwVuZxl2czGCg0WKk2oC1457H67zY9Nrp/fj7//Hn/93H/8qm/7eG/6Rm/aNX+rVZ98dc9Nhe8Opg7exd6MZK46A43nkl2lMT1S0A", 0),
        new("Bisexual (Wave)", "1gJwzgx1xxZ6vyB/uCmDsDOIqT2NoUeSm0+Wm0+Wm0+Wm0+WlU6XgUuZbUibWUWeRkKgMj+iHjylCjmnCjmnHjylMj+iRkKgWUWebUibgUuZlU6Xm0+Wm0+Wm0+Wm0+WoUeSqT2NsDOIuCmDvyB/xxZ6zgx11gJw", 1),
        new("Bisexual (Pulse)", "1gJwzgx1xxZ6vyB/uCmDsDOIqT2NoUeSm0+Wm0+Wm0+Wm0+WlU6XgUuZbUibWUWeRkKgMj+iHjylCjmnCjmnHjylMj+iRkKgWUWebUibgUuZlU6Xm0+Wm0+Wm0+Wm0+WoUeSqT2NsDOIuCmDvyB/xxZ6zgx11gJw", 0),
        new("Black & White (Wave)", "////9/f37+/v5+fn39/f19fXzs7OxsbGvr6+tra2rq6upqamnp6elpaWjo6OhoaGfX19dXV1bW1tZWVlXV1dVVVVTU1NRUVFPT09NTU1LS0tJCQkHBwcFBQUDAwMBAQEBAQEDAwMFBQUHBwcJCQkLS0tNTU1PT09RUVFTU1NVVVVXV1dZWVlbW1tdXV1fX19hoaGjo6OlpaWnp6epqamrq6utra2vr6+xsbGzs7O19fX39/f5+fn7+/v9/f3////", 1),
        new("Black & White (Pulse)", "////9/f37+/v5+fn39/f19fXzs7OxsbGvr6+tra2rq6upqamnp6elpaWjo6OhoaGfX19dXV1bW1tZWVlXV1dVVVVTU1NRUVFPT09NTU1LS0tJCQkHBwcFBQUDAwMBAQEBAQEDAwMFBQUHBwcJCQkLS0tNTU1PT09RUVFTU1NVVVVXV1dZWVlbW1tdXV1fX19hoaGjo6OlpaWnp6epqamrq6utra2vr6+xsbGzs7O19fX39/f5+fn7+/v9/f3////", 0),
    ];
    
    public static int NumColourLists => ColourLists.Count;
    public static string GetName(int i) => i > ColourLists.Count ? "Invalid" : ColourLists[i - 1].Name;
    public static readonly RGB White = (255, 255, 255);

    public static RGB GetColourRGB(int rainbowMode, int chrIndex, int throttle, bool animate = true) {
        if (rainbowMode == 0 || rainbowMode > ColourLists.Count) return White;
        var style = ColourLists[rainbowMode - 1];
        return GetColourRGB(style, chrIndex, throttle, animate);
    }
    
    public static RGB GetColourRGB(RainbowStyle style, int chrIndex, int throttle, bool animate = true) {
        if (throttle < 1) throttle = 1;
        var rainbowColors = style.Colours;
        var animationOffset = animate ? Framework.Instance()->FrameCounter : 0;
        var i = (animationOffset/ throttle + (chrIndex * style.ChrMultiplier)) % rainbowColors.GetLength(0);
        return (rainbowColors[i, 0], rainbowColors[i, 1], rainbowColors[i, 2]);
    }

    public static Vector3 GetColourVec3(int rainbowMode, int offset, int throttle) {
        var rgb = GetColourRGB(rainbowMode, offset, throttle);
        return new Vector3(rgb.R / 255f, rgb.G / 255f, rgb.B / 255f);
    }
    
    private static class GradientBuilder {
        public static int Length = 64;
        public static readonly List<FixedColour> FixedColours = [new(ushort.MaxValue / 2, 0xFF000000)];
        public static readonly List<Pair> Pairs = new();
        public static Guid Editing = Guid.Empty;
        
        public class FixedColour(ushort position, uint colour) {
            public ushort Position = position;
            public uint Colour = colour;
            public Guid Guid { get; init; } = Guid.NewGuid();
        }
        
        public record Pair(FixedColour Begin, FixedColour End);
        
        public static void UpdatePairs() {
            Pairs.Clear();

            var start = FixedColours.Find(f => f.Position == 0);

            if (start == null) {
                start = new FixedColour(0, 0xFFFFFFFF);
                FixedColours.Insert(0, start);
            }

            if (FixedColours.Find(f => f.Position == ushort.MaxValue) == null) {
                FixedColours.Add(new FixedColour(ushort.MaxValue, start.Colour));
            }
            
            var colours = FixedColours.OrderBy(f => f.Position).ToList();
            for (var i = 0; i < colours.Count - 1; i++) {
                var a = colours[i];
                var b = colours[i + 1];
                Pairs.Add(new Pair(a, b));
            }
        }

        public static RainbowStyle? GeneratedStyle;

        public static void GenerateStyle(int? steps = null) {
            steps ??= Length;
            if (steps < 2) steps = 2;
            if (steps > 1024) steps = 1024;

            UpdatePairs();
            
            var l = new List<RGB>();
            var step = (double)ushort.MaxValue / ((steps ?? Length) - 1);
            for (int i = 0; i < steps; i++) {
                var pos = (float) step * i;

                var fixedColour = FixedColours.Find(f => f.Position == (ushort)MathF.Round(pos));
                
                uint c;
                if (fixedColour != null) {
                    c = fixedColour.Colour;
                } else {
                    var pair = Pairs.Find(p => p.Begin.Position < pos &&  p.End.Position > pos);
                    if (pair == null) throw new Exception($"Failed to get pair at position: {pos}");
                    c = LerpOpaque(pair.Begin.Colour, pair.End.Colour, (pos - pair.Begin.Position) / (pair.End.Position - pair.Begin.Position));
                }
                
                
                l.Add(UintToRGB(c));
            }

            byte[,] bytes = new byte[l.Count, 3];

            for (var i = 0; i < l.Count; i++) {
                bytes[i, 0] = l[i].R;
                bytes[i, 1] = l[i].G;
                bytes[i, 2] = l[i].B;
            }
            
            GeneratedStyle = new RainbowStyle("Generated Style", bytes, 1);
        }
        
        public static uint LerpOpaque(uint start, uint end, float t) {
            return ImGui.ColorConvertFloat4ToU32(LerpOpaque(ImGui.ColorConvertU32ToFloat4(start),  ImGui.ColorConvertU32ToFloat4(end), t));
        }
        
        public static Vector4 LerpOpaque(Vector4 start, Vector4 end, float t) {
            t = Math.Clamp(t, 0f, 1f);
            Vector4 result = start + (end - start) * t;
            result.W = 1f;
            return result;
        }

        public static RGB UintToRGB(uint color) {
            return ((byte)(color & 0xFF), (byte)((color >> 8) & 0xFF), (byte)((color >> 16) & 0xFF));
        }
    }
    
    private static uint RGBA(RGB c, byte alpha = 255) =>  ((uint)alpha << 24) | ((uint)c.R << 16) | ((uint)c.G << 8) | c.B;
    private static RGB ToRGB(this Vector4 color) => ((byte)(color.X * 255f), (byte) (color.Y * 255f), (byte) (color.Z * 255f));
    
    public static void DrawGradientBuilder() {
        using var _ = ImRaii.PushId("GradientBuilder");
        
        ImGui.SetNextItemWidth(200);
        if (ImGui.SliderInt("Total Steps", ref GradientBuilder.Length, 32, 512)) {
            GradientBuilder.GenerateStyle();
        }
        
        ImGui.SameLine();
        if (ImGui.SmallButton("Spread") && GradientBuilder.FixedColours.Count > 2) {
            var step = (double)ushort.MaxValue / (GradientBuilder.FixedColours.Count - 1);
            var i = 0;
            foreach (var a in GradientBuilder.FixedColours.OrderBy(f => f.Position)) {
                a.Position = (ushort)Math.Round(step * i++);
            }
            
            GradientBuilder.UpdatePairs();
            GradientBuilder.GenerateStyle();
        }
        
        ImGui.Dummy(new Vector2(ImGui.GetContentRegionAvail().X, 32));
        ImGui.Dummy(new Vector2(16));
        ImGui.SameLine();
        ImGui.Dummy(new Vector2(ImGui.GetContentRegionAvail().X - 16, 100));

        var dl = ImGui.GetWindowDrawList();
        var tl = ImGui.GetItemRectMin();
        var size = ImGui.GetItemRectSize();

        GradientBuilder.UpdatePairs();
        
        foreach (var pair in GradientBuilder.Pairs) {
            var startPos = tl + new Vector2(size.X * pair.Begin.Position / ushort.MaxValue, 0);
            var endPos = tl + size with { X = size.X * pair.End.Position / ushort.MaxValue };
            dl.AddRectFilledMultiColor(startPos, endPos, pair.Begin.Colour, pair.End.Colour, pair.End.Colour, pair.Begin.Colour);
        }

        foreach (var f in GradientBuilder.FixedColours) {
            var pos = tl + new Vector2(size.X * f.Position / ushort.MaxValue, -16);
            var pos2 = pos + new Vector2(0, size.Y + 32);
            dl.AddLine(pos, pos2, f.Colour, 4);
            dl.AddCircleFilled(pos, 10,  f.Colour, 16);
            dl.AddCircleFilled(pos2, 10,  f.Colour, 16);
            
            if (ImGui.IsMouseHoveringRect(pos - new Vector2(10), pos + new Vector2(10)) || ImGui.IsMouseHoveringRect(pos2 - new Vector2(10), pos2 + new Vector2(10)) ) {
                dl.AddCircle(pos, 10,  0xFFFFFF00, 16, 2);
                dl.AddCircle(pos2, 10,  0xFFFFFF00, 16, 2);
                if (ImGui.GetIO().MouseClicked[0]) {
                    if (GradientBuilder.Editing == f.Guid) {
                        GradientBuilder.Editing = Guid.Empty;
                    } else {
                        GradientBuilder.Editing = f.Guid;
                    }
                }
            } else if (GradientBuilder.Editing == f.Guid) {
                dl.AddCircle(pos, 10, 0xFF0000FF, 16, 2);
                dl.AddCircle(pos2, 10, 0xFF0000FF, 16, 2);
            } else {
                dl.AddCircle(pos, 10,  0xFFFFFFFF, 16);
                dl.AddCircle(pos2, 10,  0xFFFFFFFF, 16);
            }
        }
        
        
        ImGui.GetWindowDrawList().AddRect(ImGui.GetItemRectMin(), ImGui.GetItemRectMax(), 0xFFFFFFFF);
        
        if (ImGui.IsItemHovered()) {
            var hoverPos = (ImGui.GetMousePos() - tl).X / size.X;
            ImGui.SetTooltip($"@ {MathF.Round(hoverPos * 100f, 1)}%");

            if (ImGui.IsMouseClicked(ImGuiMouseButton.Left)) {
                var pos = (hoverPos * ushort.MaxValue);
                var posShort = (ushort)(pos);
                
                if (pos is 0 or ushort.MaxValue || GradientBuilder.FixedColours.All(p => p.Position != posShort)) {
                    
                    var pair = GradientBuilder.Pairs.Find(p => p.Begin.Position < pos &&  p.End.Position > pos);

                    var c = new Vector4(0, 0, 0, 1);
                    if (pair != null) {
                        c = GradientBuilder.LerpOpaque(ImGui.ColorConvertU32ToFloat4(pair.Begin.Colour), ImGui.ColorConvertU32ToFloat4(pair.End.Colour), (pos - pair.Begin.Position) / (pair.End.Position - pair.Begin.Position));
                    }
                    
                    var newColour = new GradientBuilder.FixedColour(posShort, ImGui.ColorConvertFloat4ToU32(c));
                    GradientBuilder.FixedColours.Add(newColour);
                    GradientBuilder.Editing = newColour.Guid;
                }
            }
        }
        ImGui.Dummy(new Vector2(32));
        ImGui.SameLine();
        using (ImRaii.Group()) {

            ImGui.Dummy(new Vector2(32));
            var editing = GradientBuilder.FixedColours.Find(f =>  f.Guid == GradientBuilder.Editing);
            
            if (editing == null) {
                GradientBuilder.Editing = Guid.Empty;
            }

            using (ImRaii.Disabled(editing == null)) {
                editing ??= new GradientBuilder.FixedColour(ushort.MaxValue / 2, 0x00000000);
                var position = editing.Position * 100f / ushort.MaxValue;
                var colour = ImGui.ColorConvertU32ToFloat4(editing?.Colour ?? 0xFFFFFFFF);
            
                var edited = false;
                ImGui.SetNextItemWidth(300);

                if (position is 0 or ushort.MaxValue) {
                    using (ImRaii.Disabled()) { 
                        edited |= ImGui.SliderFloat("Position", ref position, 0, 100, "%.1f");
                    }
                } else {
                    edited |= ImGui.SliderFloat("Position", ref position, 0, 100, "%.1f");
                }
            
            
                ImGui.SetNextItemWidth(300);
                edited |= ImGui.ColorPicker4("Colour", ref colour, ImGuiColorEditFlags.NoAlpha);
        
                if (edited && editing != null && GradientBuilder.Editing != Guid.Empty) {
                    GradientBuilder.FixedColours.Remove(editing);

                    if (editing.Position == 0) {
                        GradientBuilder.FixedColours.RemoveAll(f => f.Position == ushort.MaxValue);
                    }
                    
                    
                    var newPos = (ushort) (position / 100f * ushort.MaxValue);
                    GradientBuilder.FixedColours.Add(new GradientBuilder.FixedColour(newPos, ImGui.ColorConvertFloat4ToU32(colour)) { Guid = editing.Guid });
                    GradientBuilder.GenerateStyle();
                }
            }
        }
        
        ImGui.SameLine();
        ImGui.Dummy(new Vector2(32));
        ImGui.SameLine();

        using (ImRaii.Group()) {
            ImGui.Dummy(new Vector2(32));
            foreach (var a in GradientBuilder.FixedColours.OrderBy(f => f.Position)) {
                using (ImRaii.PushColor(ImGuiCol.ButtonActive, a.Colour & 0x80FFFFFF)) 
                using (ImRaii.PushColor(ImGuiCol.ButtonHovered, a.Colour & 0x40FFFFFF)) 
                using (ImRaii.PushColor(ImGuiCol.Button, a.Colour)) {
                    if (ImGui.Button($"##color_{a.Guid}", new Vector2(ImGui.GetTextLineHeightWithSpacing()))) {
                        GradientBuilder.Editing = a.Guid;
                    } 
                }
                
                ImGui.SameLine();
                ImGui.Text($"@ {MathF.Round(a.Position * 100 / (float)ushort.MaxValue, 1)}%");
            }
        }
        
        ImGui.Separator();

        if (GradientBuilder.GeneratedStyle != null) {
            var previewTitle = new CustomTitle() { Title = "Preview Title", RainbowMode = 1, CustomRainbowStyle = GradientBuilder.GeneratedStyle};
            ImGuiHelpers.SeStringWrapped(previewTitle.ToSeString(false).Encode(), new SeStringDrawParams { Color = 0xFF000000, WrapWidth = float.MaxValue, FontSize = 24});
            ImGuiHelpers.SeStringWrapped(previewTitle.ToSeString(false).Encode(), new SeStringDrawParams { Color = 0xFFFFFFFF, WrapWidth = float.MaxValue, FontSize = 24});

            if (ImGui.Button("Export Style")) {
                var bytes = GradientBuilder.GeneratedStyle.Colours.Cast<byte>().ToArray();
                var b64 = Convert.ToBase64String(bytes);
                ImGui.SetClipboardText(b64);
            }
            
        } else {
            GradientBuilder.GenerateStyle();
        }
    }
}
global using RGB = (byte R, byte G, byte B);
using System;
using System.Collections.Generic;
using System.Diagnostics;
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

    private static readonly List<(string Name, string Base64)> ColourSets = [
        ("Black & Red", "/wAA9QAA6wAA4QAA1wAAzAAAwgAAuAAArgAApAAAmgAAkAAAhgAAewAAcQAAZwAAXQAAUwAASQAAPwAANQAAKwAAIAAAFgAADAAAAgAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAgAADAAAFgAAIAAAKgAANQAAPwAASQAAUwAAXQAAZwAAcQAAewAAhgAAkAAAmgAApAAArgAAuAAAwgAAzAAA1wAA4QAA6wAA9QAA/wAA"),
        ("Black & Blue", "AAD/AAD1AADrAADhAADXAADMAADCAAC4AACuAACkAACaAACQAACGAAB7AABxAABnAABdAABTAABJAAA/AAA1AAArAAAgAAAWAAAMAAACAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAACAAAMAAAWAAAgAAAqAAA1AAA/AABJAABTAABdAABnAABxAAB7AACGAACQAACaAACkAACuAAC4AADCAADMAADXAADhAADrAAD1AAD/"),
        ("Black & Yellow", "//8A9fUA6+sA4eEA19cAzMwAwsIAuLgArq4ApKQAmpoAkJAAhoYAe3sAcXEAZ2cAXV0AU1MASUkAPz8ANTUAKysAICAAFhYADAwAAgIAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAgIADAwAFhYAICAAKioANTUAPz8ASUkAU1MAXV0AZ2cAcXEAe3sAhoYAkJAAmpoApKQArq4AuLgAwsIAzMwA19cA4eEA6+sA9fUA//8A"),
        ("Black & Green", "AP8AAPUAAOsAAOEAANcAAMwAAMIAALgAAK4AAKQAAJoAAJAAAIYAAHsAAHEAAGcAAF0AAFMAAEkAAD8AADUAACsAACAAABYAAAwAAAIAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAIAAAwAABYAACAAACoAADUAAD8AAEkAAFMAAF0AAGcAAHEAAHsAAIYAAJAAAJoAAKQAAK4AALgAAMIAAMwAANcAAOEAAOsAAPUAAP8A"),
        ("Black & Pink", "/wD/9QD16wDr4QDh1wDXzADMwgDCuAC4rgCupACkmgCakACQhgCGewB7cQBxZwBnXQBdUwBTSQBJPwA/NQA1KwArIAAgFgAWDAAMAgACAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAgACDAAMFgAWIAAgKgAqNQA1PwA/SQBJUwBTXQBdZwBncQBxewB7hgCGkACQmgCapACkrgCuuAC4wgDCzADM1wDX4QDh6wDr9QD1/wD/"),
        ("Black & Cyan", "AP//APX1AOvrAOHhANfXAMzMAMLCALi4AK6uAKSkAJqaAJCQAIaGAHt7AHFxAGdnAF1dAFNTAElJAD8/ADU1ACsrACAgABYWAAwMAAICAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAICAAwMABYWACAgACoqADU1AD8/AElJAFNTAF1dAGdnAHFxAHt7AIaGAJCQAJqaAKSkAK6uALi4AMLCAMzMANfXAOHhAOvrAPX1AP//"),
        // ("", ""),
    ];
    
    static RainbowColour() {
        foreach (var s in ColourSets) {
            ColourLists.Add(new RainbowStyle($"{s.Name} (Wave)", s.Base64, 1));
            ColourLists.Add(new RainbowStyle($"{s.Name} (Pulse)", s.Base64, 0));
        }
    }
    
    
    public static int NumColourLists => ColourLists.Count;
    public static string GetName(int i) => i > ColourLists.Count ? "Invalid" : ColourLists[i - 1].Name;
    public static readonly RGB White = (255, 255, 255);

    public static RGB GetColourRGB(int rainbowMode, int chrIndex, int throttle, bool animate = true) {
        if (rainbowMode == 0 || rainbowMode > ColourLists.Count) return White;
        var style = ColourLists[rainbowMode - 1];
        return GetColourRGB(style, chrIndex, throttle, animate);
    }
    
    private static readonly Stopwatch TimeSinceStart = Stopwatch.StartNew();
    
    public static RGB GetColourRGB(RainbowStyle style, int chrIndex, int throttle, bool animate = true) {
        if (throttle < 1) throttle = 1;
        var rainbowColors = style.Colours;
        var animationOffset = animate ? TimeSinceStart.ElapsedMilliseconds / 15 : 0;
        var i = (animationOffset/ throttle + (chrIndex * style.ChrMultiplier)) % rainbowColors.GetLength(0);
        return (rainbowColors[i, 0], rainbowColors[i, 1], rainbowColors[i, 2]);
    }

    public static Vector3 GetColourVec3(int rainbowMode, int offset, int throttle) {
        var rgb = GetColourRGB(rainbowMode, offset, throttle);
        return new Vector3(rgb.R / 255f, rgb.G / 255f, rgb.B / 255f);
    }
    
    public static class GradientBuilder {
        public static int Length = 64;
        public static readonly List<FixedColour> FixedColours = [new(ushort.MaxValue / 2, 0xFF000000)];
        public static readonly List<Pair> Pairs = new();
        public static Guid Editing = Guid.Empty;
        public static int Mode = 0;
        public static int Multi = 1;
        public static string PreviewText = "Preview Title";
        public static Vector3 PreviewTextColour = Vector3.Zero;
        
        public class FixedColour(ushort position, uint colour) {
            public ushort Position = position;
            public uint Colour = colour;
            public Guid Guid { get; init; } = Guid.NewGuid();
        }

        public record Pair(FixedColour Begin, FixedColour End) {
            public int Length => End.Position - Begin.Position;
            public FixedColour ColourAt(float t) {
                var p = (ushort)MathF.Round(Begin.Position + t * Length);
                return new FixedColour(p, Mode switch {
                    0 => LerpOpaque(Begin.Colour, End.Colour, t),
                    1 => LerpHueOpaque(Begin.Colour, End.Colour, t),
                    _ => 0
                });
            }
        }
        
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

                uint c = 0;
                if (fixedColour != null) {
                    c = fixedColour.Colour;
                } else {
                    var pair = Pairs.Find(p => p.Begin.Position < pos &&  p.End.Position > pos);
                    if (pair == null) throw new Exception($"Failed to get pair at position: {pos}");
                    var pairPos = (pos - pair.Begin.Position) / (pair.End.Position - pair.Begin.Position);
                    c = pair.ColourAt(pairPos).Colour;
                }
                
                
                l.Add(UintToRGB(c));
            }

            byte[,] bytes = new byte[l.Count, 3];

            for (var i = 0; i < l.Count; i++) {
                bytes[i, 0] = l[i].R;
                bytes[i, 1] = l[i].G;
                bytes[i, 2] = l[i].B;
            }
            
            GeneratedStyle = new RainbowStyle("Generated Style", bytes, Multi);
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

        private static Vector3 GetHSV(Vector4 v) {
            var hsv = new Vector3();
            ImGui.ColorConvertRGBtoHSV(v.X, v.Y, v.Z, ref hsv.X, ref hsv.Y, ref hsv.Z);
            return hsv;
        }
        
        private static float DeltaAngle(float a, float b) {
            float diff = (b - a) % 360f;
            if (diff > 180f)  diff -= 360f;
            if (diff < -180f) diff += 360f;
            return diff;
        }
        
        public static float Lerp(float a, float b, float t) => a + (b - a) * t;

        public static uint LerpHueOpaque(uint start, uint end, float t) {
            return ImGui.ColorConvertFloat4ToU32(LerpHueOpaque(ImGui.ColorConvertU32ToFloat4(start),  ImGui.ColorConvertU32ToFloat4(end), t));
        }

        public static Vector4 LerpHueOpaque(Vector4 start, Vector4 end, float t) {
            Vector3 startHsv = GetHSV(start);
            Vector3 endHsv = GetHSV(end);
            
            float deltaH = DeltaAngle(startHsv.X * 360f, endHsv.X * 360f) / 360f;
            float h = startHsv.X + deltaH * t;
            
            if (h < 0f) h += 1f;
            else if (h > 1f) h -= 1f;
            
            float s = Lerp(startHsv.Y, endHsv.Y, t);
            float v = Lerp(startHsv.Z, endHsv.Z, t);

            return ImGui.HSV(h, s, v, 1).Value;
        }

        public static RGB UintToRGB(uint color) {
            return ((byte)(color & 0xFF), (byte)((color >> 8) & 0xFF), (byte)((color >> 16) & 0xFF));
        }

        public static void Draw() {
            using var _ = ImRaii.PushId("GradientBuilder");
            
            if (ImGui.SmallButton("Spread") && FixedColours.Count > 2) {
                var step = (double)ushort.MaxValue / (FixedColours.Count - 1);
                var i = 0;
                foreach (var a in FixedColours.OrderBy(f => f.Position)) {
                    a.Position = (ushort)Math.Round(step * i++);
                }
                
                UpdatePairs();
                GenerateStyle();
            }
            ImGui.SameLine();
            ImGui.SetNextItemWidth(100);
            if (ImGui.SliderInt("Mode", ref Mode, 0, 1)) {
                GenerateStyle();
            }
            
            
            
            ImGui.Dummy(new Vector2(ImGui.GetContentRegionAvail().X, 32));
            ImGui.Dummy(new Vector2(16));
            ImGui.SameLine();
            ImGui.Dummy(new Vector2(ImGui.GetContentRegionAvail().X - 16, 100));

            var dl = ImGui.GetWindowDrawList();
            var tl = ImGui.GetItemRectMin();
            var size = ImGui.GetItemRectSize();

            UpdatePairs();
            
            for (var i = 0; i < size.X; i++) {
                var pos = (ushort) MathF.Round((i / size.X) * ushort.MaxValue);
                var startPos = tl + new Vector2(i, 0);
                var endPos = tl + new Vector2(i, size.Y);
                var p = Pairs.Find(p => p.Begin.Position <= pos && p.End.Position > pos);
                if (p == null) continue;
                var pPct = (pos - p.Begin.Position) / (float)(p.End.Position - p.Begin.Position);
                dl.AddLine(startPos, endPos, p.ColourAt(pPct).Colour);
            }

            foreach (var f in FixedColours) {
                var pos = tl + new Vector2(size.X * f.Position / ushort.MaxValue, -16);
                var pos2 = pos + new Vector2(0, size.Y + 32);
                dl.AddLine(pos, pos2, f.Colour, 4);
                dl.AddCircleFilled(pos, 10,  f.Colour, 16);
                dl.AddCircleFilled(pos2, 10,  f.Colour, 16);
                
                if (ImGui.IsMouseHoveringRect(pos - new Vector2(10), pos + new Vector2(10)) || ImGui.IsMouseHoveringRect(pos2 - new Vector2(10), pos2 + new Vector2(10)) ) {
                    dl.AddCircle(pos, 10,  0xFFFFFF00, 16, 2);
                    dl.AddCircle(pos2, 10,  0xFFFFFF00, 16, 2);
                    if (ImGui.GetIO().MouseClicked[0]) {
                        if (Editing == f.Guid) {
                            Editing = Guid.Empty;
                        } else {
                            Editing = f.Guid;
                        }
                    }
                } else if (Editing == f.Guid) {
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
                    
                    if (pos is 0 or ushort.MaxValue || FixedColours.All(p => p.Position != posShort)) {
                        var pair = Pairs.Find(p => p.Begin.Position < pos &&  p.End.Position > pos);
                        if (pair != null) {
                            var pairPos = (pos - pair.Begin.Position) / (pair.End.Position - pair.Begin.Position);
                            var newColour = pair.ColourAt(pairPos);
                            FixedColours.Add(newColour);
                            Editing = newColour.Guid;
                        }
                    }
                }
            }
            ImGui.Dummy(new Vector2(32));
            ImGui.SameLine();
            using (ImRaii.Group()) {

                ImGui.Dummy(new Vector2(32));
                var editing = FixedColours.Find(f =>  f.Guid == Editing);
                
                if (editing == null) {
                    Editing = Guid.Empty;
                }

                using (ImRaii.Disabled(editing == null)) {
                    editing ??= new FixedColour(ushort.MaxValue / 2, 0x00000000);
                    var position = editing.Position * 100f / ushort.MaxValue;
                    var colour = ImGui.ColorConvertU32ToFloat4(editing?.Colour ?? 0xFFFFFFFF);
                
                    var edited = false;
                    ImGui.SetNextItemWidth(300);

                    using (ImRaii.Disabled(editing == null || editing.Position == 0 || editing.Position == ushort.MaxValue)) {
                        if (ImGui.SmallButton("Delete Node") && editing != null) {
                            FixedColours.Remove(editing);
                        }
                    }
                    
                    using (ImRaii.Disabled(editing == null || editing.Position is 0 or ushort.MaxValue)) { 
                        edited |= ImGui.SliderFloat("Position", ref position, 0, 100, "%.1f");
                    }
                    
                    ImGui.SetNextItemWidth(300);
                    edited |= ImGui.ColorPicker4("Colour", ref colour, ImGuiColorEditFlags.NoAlpha);
            
                    if (edited && editing != null && Editing != Guid.Empty) {
                        FixedColours.Remove(editing);

                        if (editing.Position == 0) {
                            FixedColours.RemoveAll(f => f.Position == ushort.MaxValue);
                        }
                        
                        
                        var newPos = (ushort) (position / 100f * ushort.MaxValue);
                        if (editing.Position is not (0  or ushort.MaxValue)) newPos = ushort.Clamp(newPos, 1, ushort.MaxValue - 1);
                        FixedColours.Add(new FixedColour(newPos, ImGui.ColorConvertFloat4ToU32(colour)) { Guid = editing.Guid });
                        GenerateStyle();
                    }
                }

                ImGui.Separator();
                using (ImRaii.Group()) {
                    ImGui.SetNextItemWidth(200);
                    if (ImGui.SliderInt("Export Steps", ref Length, 32, 512)) {
                        GenerateStyle();
                    }

                    ImGui.SetNextItemWidth(200);
                    if (ImGui.SliderInt("Preview Multi", ref Multi, 0, 5)) {
                        GenerateStyle();
                    }
            
                    ImGui.SetNextItemWidth(200);
                    if (ImGui.InputText("Preview Text", ref PreviewText, 32)) {
                        GenerateStyle();
                    }
                
                    ImGui.SetNextItemWidth(200);
                    if (ImGui.ColorEdit3("Preview Colour", ref PreviewTextColour, ImGuiColorEditFlags.NoInputs)) {
                        GenerateStyle();
                    }
                }
                
                if (GeneratedStyle != null) {
                    var previewTitle = new CustomTitle() {
                        Title = PreviewText, RainbowMode = 1, CustomRainbowStyle = GeneratedStyle,
                        Color = PreviewTextColour
                    };
                    
                    ImGui.SameLine();
                    ImGui.Dummy(new Vector2(48));
                    ImGui.SameLine();
                    using (ImRaii.Group()) {
                        ImGuiHelpers.SeStringWrapped(previewTitle.ToSeString(false).Encode(),
                            new SeStringDrawParams
                                { Color = 0xFFFFFFFF, WrapWidth = float.MaxValue, FontSize = 32 });
                    }

                    if (ImGui.Button("Export Style")) {
                        var bytes = GeneratedStyle.Colours.Cast<byte>().ToArray();
                        var b64 = Convert.ToBase64String(bytes);
                        ImGui.SetClipboardText(b64);
                    }

                    ImGui.SameLine();
                    if (ImGui.Button("Import Style")) {
                        try {
                            var s = new RainbowStyle("Import", ImGui.GetClipboardText(), 1);
                            FixedColours.Clear();
                            Length = s.Colours.GetLength(0);
                            for (var i = 0; i < Length; i++) {
                                var abgr = ((uint)0xFF << 24) | ((uint)s.Colours[i, 2] << 16) | ((uint)s.Colours[i,1] << 8)  | s.Colours[i,0];
                                FixedColours.Add(new FixedColour((ushort) MathF.Round(i / (float)Length * ushort.MaxValue) , abgr));
                            }
                                
                                
                            
                            UpdatePairs();
                            GenerateStyle();
                        } catch {
                            //
                        }
                    }
                    
                    var eid = PluginService.Objects.LocalPlayer?.EntityId;
                    if (eid != null) {
                        if (Plugin.IpcAssignedTitles.ContainsKey(eid.Value)) {
                                        
                            if (ImGui.Button("Clear Preview")) {
                                Plugin.IpcAssignedTitles.Remove(eid.Value);
                            }
                        } else {
                            if (ImGui.Button("Preview on Self")) {
                                Plugin.IpcAssignedTitles[eid.Value] = previewTitle;
                            }
                        }
                    }
                } else {
                    GenerateStyle();
                }
            }
            
            ImGui.SameLine();
            ImGui.Dummy(new Vector2(32));
            ImGui.SameLine();

            using (ImRaii.Group()) {
                ImGui.Dummy(new Vector2(32));
                foreach (var a in FixedColours.OrderBy(f => f.Position)) {
                    using (ImRaii.PushColor(ImGuiCol.ButtonActive, a.Colour & 0x80FFFFFF)) 
                    using (ImRaii.PushColor(ImGuiCol.ButtonHovered, a.Colour & 0x40FFFFFF)) 
                    using (ImRaii.PushColor(ImGuiCol.Button, a.Colour)) {
                        if (ImGui.Button($"##color_{a.Guid}", new Vector2(ImGui.GetTextLineHeightWithSpacing()))) {
                            Editing = a.Guid;
                        } 
                    }
                    
                    ImGui.SameLine();
                    ImGui.Text($"@ {MathF.Round(a.Position * 100 / (float)ushort.MaxValue, 1)}%");
                }
            }
        }
        
    }
    

    public static void DrawGradientBuilder() {
        GradientBuilder.Draw();
    }
}
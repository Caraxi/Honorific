using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.ImGuiSeStringRenderer;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Newtonsoft.Json;

namespace Honorific.Gradient;

public static class GradientBuilder {
    public static int Length = 64;
    public static readonly List<FixedColour> FixedColours = [new(ushort.MaxValue / 2, 0xFF000000)];
    public static readonly List<Pair> Pairs = new();
    public static Guid Editing = Guid.Empty;
    public static int Mode = 0;
    public static GradientAnimationStyle AnimationStyle = GradientAnimationStyle.Wave;
    public static string PreviewText = "Preview Title";
    public static Vector3 PreviewTextColour = Vector3.Zero;
    
    public class FixedColour(ushort position, uint colour) {
        public ushort Position = position;
        public uint Colour = colour;
        public Guid Guid { get; init; } = Guid.NewGuid();
    }

    public record Pair(FixedColour Begin, FixedColour End) {
        public int Length => End.Position - Begin.Position;
        public FixedColour ColourAt(float t) => ColourAt(t, Mode);
        public FixedColour ColourAt(float t, int mode) {
            var p = (ushort)MathF.Round(Begin.Position + t * Length);
            return new FixedColour(p, mode switch {
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

    public static GradientStyle? GeneratedStyle;
    
    public static GradientStyle GenerateStyle(GradientBuilderArgs args) {
        var steps = int.Clamp(args.Steps, 2, 1024);
        args.UpdatePairs();
        
        var l = new List<RGB>();
        var step = (double)ushort.MaxValue / (steps - 1);
        for (int i = 0; i < steps; i++) {
            var pos = (float) step * i;
            var fixedColour = args.FixedColours.Find(f => f.Position == (ushort)MathF.Round(pos));
            uint c = 0;
            if (fixedColour != null) {
                c = fixedColour.Colour;
            } else {
                var pair = args.Pairs.First(p => p.Begin.Position < pos &&  p.End.Position > pos);
                if (pair == null) throw new Exception($"Failed to get pair at position: {pos}");
                var pairPos = (pos - pair.Begin.Position) / (pair.End.Position - pair.Begin.Position);
                c = pair.ColourAt(pairPos, args.GradientMode).Colour;
            }
            
            l.Add(UintToRGB(c));
        }

        byte[,] bytes = new byte[l.Count, 3];
        for (var i = 0; i < l.Count; i++) {
            bytes[i, 0] = l[i].R;
            bytes[i, 1] = l[i].G;
            bytes[i, 2] = l[i].B;
        }
        
        return new GradientStyle(args.Name, bytes, args.AnimationStyle);
    }
    
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
        
        GeneratedStyle = new GradientStyle("Generated Style", bytes, AnimationStyle);
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
        return new((byte)(color & 0xFF), (byte)((color >> 8) & 0xFF), (byte)((color >> 16) & 0xFF));
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

                if (ImGui.BeginCombo("Preview Animation Style", $"{AnimationStyle}")) {
                    foreach (var e in Enum.GetValues<GradientAnimationStyle>()) {
                        if (ImGui.Selectable($"{e}##gradientAnimationStyle+{e}", AnimationStyle == e)) {
                            AnimationStyle = e;
                            GenerateStyle();
                        }
                    }
                    
                    ImGui.EndCombo();
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
                    Title = PreviewText, CustomRainbowStyle = GeneratedStyle,
                    Color = PreviewTextColour
                };
                
                ImGui.SameLine();
                ImGui.Dummy(new Vector2(48));
                ImGui.SameLine();
                using (ImRaii.Group()) {
                    ImGuiHelpers.SeStringWrapped(previewTitle.ToSeString(false).Encode(),
                        new SeStringDrawParams
                            { Color = 0xFFFFFFFF, WrapWidth = float.MaxValue, Font = UiBuilder.DefaultFont, FontSize = UiBuilder.DefaultFontSizePx });
                }


                
                
                if (ImGui.Button("Export JSON")) {
                    var data = new {
                        Colours = FixedColours.GroupBy(c => c.Position).Where(g => g.Key != ushort.MaxValue).OrderBy(g => g.Key).ToDictionary(g => g.Key.ToString("00000"), c => string.Join(',', c.Select(colour => UintToRGB(colour.Colour).ToHexColorCode()))),
                        Mode
                    };

                    var json = JsonConvert.SerializeObject(data, Formatting.Indented);
                    ImGui.SetClipboardText(json);
                }

                ImGui.SameLine();
                
                if (ImGui.Button("Import Style")) {
                    try {
                        var clipboard = ImGui.GetClipboardText().Trim();
                        if (clipboard.StartsWith('{') && clipboard.EndsWith('}')) {
                            var data = new {
                                Colours = FixedColours.GroupBy(c => c.Position).Where(g => g.Key != ushort.MaxValue).OrderBy(g => g.Key).ToDictionary(g => g.Key.ToString("00000"), c => string.Join(',', c.Select(colour => UintToRGB(colour.Colour).ToHexColorCode()))),
                                Mode
                            };
                            
                            data = JsonConvert.DeserializeAnonymousType(clipboard, data);
                            if (data != null) {
                                Mode = data.Mode;
                                FixedColours.Clear();
                                foreach (var (positionStr, coloursStr) in data.Colours) {
                                    if (!ushort.TryParse(positionStr, out var position)) continue;
                                    if (position == ushort.MaxValue) continue;
                                    var colours = coloursStr.Split(',').Select(RGB.FromHexColourCode);

                                    foreach (var colour in colours) {
                                        if (colour == null) continue;
                                        FixedColours.Add(new FixedColour(position, colour.ToUInt()));
                                        if (position == 0) {
                                            FixedColours.Add(new FixedColour(ushort.MaxValue, colour.ToUInt()));
                                        }
                                    }
                                }
                            }
                        } else {
                            var s = new GradientStyle("Import", ImGui.GetClipboardText(), GradientAnimationStyle.Wave);
                            FixedColours.Clear();
                            Length = s.Colours.GetLength(0);
                            for (var i = 0; i < Length; i++) {
                                var abgr = ((uint)0xFF << 24) | ((uint)s.Colours[i, 2] << 16) | ((uint)s.Colours[i,1] << 8)  | s.Colours[i,0];
                                FixedColours.Add(new FixedColour((ushort) MathF.Round(i / (float)Length * ushort.MaxValue) , abgr));
                            }
                        }
                        
                        UpdatePairs();
                        GenerateStyle();
                    } catch {
                        //
                    }
                }
                
                if (ImGui.GetIO().KeyShift && ImGui.Button("Export for Production")) {
                    var bytes = GeneratedStyle.Colours.Cast<byte>().ToArray();
                    var b64 = Convert.ToBase64String(bytes);
                    ImGui.SetClipboardText(b64);
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
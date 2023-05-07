using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Numerics;
using System.Text;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using Dalamud.Interface.Windowing;
using Dalamud.Logging;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.FFXIV.Client.UI;
using ImGuiNET;
using Lumina.Excel.GeneratedSheets;
using Newtonsoft.Json;
using World = Lumina.Excel.GeneratedSheets.World;

namespace Honorific; 

public class ConfigWindow : Window {
    private PluginConfig config;
    private Plugin plugin;

    public ConfigWindow(string name, Plugin plugin, PluginConfig config) : base(name) {
        this.config = config;
        this.plugin = plugin;
        
        SizeConstraints = new WindowSizeConstraints {
            MinimumSize = ImGuiHelpers.ScaledVector2(800, 400),
            MaximumSize = new Vector2(float.MaxValue)
        };

        Size = ImGuiHelpers.ScaledVector2(800, 500);
        SizeCondition = ImGuiCond.Once;
        
        BuildColorList();
        
    }
    
    private Vector2 iconButtonSize = new(16);
    private float checkboxSize = 36;

    public void DrawCharacterList() {

        foreach (var (worldId, characters) in config.WorldCharacterDictionary.ToArray()) {
            var world = PluginService.Data.GetExcelSheet<World>()?.GetRow(worldId);
            if (world == null) continue;
            
            ImGui.TextDisabled($"{world.Name.RawString}");
            ImGui.Separator();

            foreach (var (name, characterConfig) in characters.ToArray()) {
                if (ImGui.Selectable($"{name}##{world.Name.RawString}", selectedCharacter == characterConfig)) {
                    selectedCharacter = characterConfig;
                    selectedName = name;
                    selectedWorld = world.RowId;
                }
                
                if (ImGui.BeginPopupContextItem()) {
                    if (ImGui.Selectable($"Remove '{name} @ {world.Name.RawString}' from Config")) {
                        characters.Remove(name);
                        if (selectedCharacter == characterConfig) selectedCharacter = null;
                        if (characters.Count == 0) {
                            config.WorldCharacterDictionary.Remove(worldId);
                        }
                    }
                    ImGui.EndPopup();
                }
            }

            ImGuiHelpers.ScaledDummy(10);
        }

        if (Plugin.IsDebug && Plugin.IpcAssignedTitles.Count > 0) {
            ImGui.TextDisabled("[DEBUG] IPC Assignments");
            ImGui.Separator();

            foreach (var (name, worldId) in Plugin.IpcAssignedTitles.Keys.ToArray()) {
                var world = PluginService.Data.GetExcelSheet<World>()?.GetRow(worldId);
                if (world == null) continue;
                if (ImGui.Selectable($"{name}##{world.Name.RawString}##ipc", selectedName == name && selectedWorld == worldId)) {
                    config.TryGetCharacterConfig(name, worldId, out selectedCharacter);
                    selectedCharacter ??= new CharacterConfig();
                    selectedName = name;
                    selectedWorld = world.RowId;
                }
                ImGui.SameLine();
                ImGui.TextDisabled(world.Name.ToDalamudString().TextValue);
            }
            
            ImGuiHelpers.ScaledDummy(10);
        }

    }

    private CharacterConfig? selectedCharacter;
    private string selectedName = string.Empty;
    private uint selectedWorld;

    public override void Draw() {
        ImGui.BeginGroup();
        {
            if (ImGui.BeginChild("character_select", ImGuiHelpers.ScaledVector2(240, 0) - iconButtonSize with { X = 0 }, true)) {
                DrawCharacterList();
            }
            ImGui.EndChild();

            var charListSize = ImGui.GetItemRectSize();

            if (PluginService.ClientState.LocalPlayer != null) {
                if (ImGuiComponents.IconButton(FontAwesomeIcon.User)) {
                    if (PluginService.ClientState.LocalPlayer != null) {
                        config.TryAddCharacter(PluginService.ClientState.LocalPlayer.Name.TextValue, PluginService.ClientState.LocalPlayer.HomeWorld.Id);
                    }
                }
                
                if (ImGui.IsItemHovered()) ImGui.SetTooltip("Add current character");
                
                ImGui.SameLine();
                if (ImGuiComponents.IconButton(FontAwesomeIcon.DotCircle)) {
                    if (PluginService.Targets.Target is PlayerCharacter pc) {
                        config.TryAddCharacter(pc.Name.TextValue, pc.HomeWorld.Id);
                    }
                }
                if (ImGui.IsItemHovered()) ImGui.SetTooltip("Add targeted character");
                ImGui.SameLine();
            }
            
            if (ImGuiComponents.IconButton(FontAwesomeIcon.Cog)) {
                selectedCharacter = null;
                selectedName = string.Empty;
                selectedWorld = 0;
            }
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Plugin Options");
            iconButtonSize = ImGui.GetItemRectSize() + ImGui.GetStyle().ItemSpacing;
        }
        ImGui.EndGroup();
        
        ImGui.SameLine();
        if (ImGui.BeginChild("character_view", ImGuiHelpers.ScaledVector2(0), true)) {
            if (selectedCharacter != null) {

                if (Plugin.IsDebug && ImGui.TreeNode("DEBUG INFO")) {
                    var activePlayer = PluginService.Objects.FirstOrDefault(t => t is PlayerCharacter playerCharacter && playerCharacter.Name.TextValue == selectedName && playerCharacter.HomeWorld.Id == selectedWorld);
                    if (activePlayer is PlayerCharacter pc && plugin.TryGetTitle(pc, out var expectedTitle) && expectedTitle != null) {
                        ImGui.TextDisabled($"ObjectID: {activePlayer:X8}");
                        unsafe {
                            var raptureAtkModule = Framework.Instance()->GetUiModule()->GetRaptureAtkModule();

                            var npi = &raptureAtkModule->NamePlateInfoArray;
                            for (var i = 0; i < 50 && i < raptureAtkModule->NameplateInfoCount; i++, npi++) {
                                if (npi->ObjectID.ObjectID == pc.ObjectId) {
                                    ImGui.Text($"NamePlateStruct: [{(ulong)npi:X}]");
                                    Util.ShowStruct(*npi, (ulong) npi, true, new string[] { $"{(ulong)npi:X}"});

                                    var expectedTitleSeString = expectedTitle.ToSeString(true, config.ShowColoredTitles);
                                    
                                    var currentTitle = npi->DisplayTitle.ToSeString();
                                    ImGui.Text($"Current Title:");
                                    ImGui.Indent();
                                    foreach(var p in currentTitle.Payloads) ImGui.Text($"{p}");
                                    ImGui.Unindent();
                                    ImGui.Text($"Expected Title:");
                                    ImGui.Indent();
                                    foreach(var p in expectedTitleSeString.Payloads) ImGui.Text($"{p}");
                                    ImGui.Unindent();
                                    
                                    ImGui.Text($"Titles Match?: {currentTitle.IsSameAs(expectedTitleSeString, out _)}");
                                    
                                }
                            }
                        }
                    } else {
                        ImGui.TextDisabled("Character is not currently in world.");
                    }

                    ImGui.TreePop();
                }
                
                
                

                if (Plugin.IpcAssignedTitles.TryGetValue((selectedName, selectedWorld), out var title)) {

                    ImGui.Text("This character's title is currently assigned by another plugin.");
                    if (Plugin.IsDebug && ImGui.Button("Clear IPC Assignment")) {
                        Plugin.IpcAssignedTitles.Remove((selectedName, selectedWorld));
                    }
                    
                    ImGui.BeginDisabled();
                    
                    if (ImGui.BeginTable("TitlesTable", config.ShowColoredTitles ? 5 : 3)) {
                        ImGui.TableSetupColumn("##enable", ImGuiTableColumnFlags.WidthFixed, checkboxSize * 4 + 3);
                        ImGui.TableSetupColumn("Title", ImGuiTableColumnFlags.WidthFixed, 150 * ImGuiHelpers.GlobalScale);
                        ImGui.TableSetupColumn("Prefix", ImGuiTableColumnFlags.WidthFixed, checkboxSize * 2);
                        if (config.ShowColoredTitles) {
                            ImGui.TableSetupColumn("Colour", ImGuiTableColumnFlags.WidthFixed, checkboxSize * 2);
                            ImGui.TableSetupColumn("Glow", ImGuiTableColumnFlags.WidthFixed, checkboxSize * 2);
                        }
                        ImGui.TableSetupColumn("##condition", ImGuiTableColumnFlags.WidthStretch);
                        ImGui.TableHeadersRow();


                        ImGui.TableNextColumn();
                        DrawTitleCommon(title);

                        ImGui.EndTable();
                    }
                    
                    ImGui.EndDisabled();
                    return;
                }
                
                
                
                if (ImGui.Button("Export Titles to Clipboard")) {
                    var export = JsonConvert.SerializeObject(selectedCharacter);
                    using var str = new MemoryStream();
                    using (var gzStr = new GZipStream(str, CompressionMode.Compress)) {
                        gzStr.Write(Encoding.UTF8.GetBytes(export));
                    }
                    ImGui.SetClipboardText(Convert.ToBase64String(str.ToArray()));
                }
                
                ImGui.SameLine();
                if (ImGui.Button("Import Titles from Clipboard")) {
                    try {
                        var b64 = ImGui.GetClipboardText();
                        var bytes = Convert.FromBase64String(b64);
                        using var str = new MemoryStream(bytes);
                        using var gzStream = new GZipStream(str, CompressionMode.Decompress);
                        using var outStr = new MemoryStream();
                        gzStream.CopyTo(outStr);
                        var outBytes = outStr.ToArray();
                        var json = Encoding.UTF8.GetString(outBytes);

                        var importedCharacter = JsonConvert.DeserializeObject<CharacterConfig>(json);
                        if (importedCharacter is { CustomTitles: { }, DefaultTitle: { } }) {
                            selectedCharacter.CustomTitles.Clear();
                            selectedCharacter.CustomTitles.AddRange(importedCharacter.CustomTitles);
                            selectedCharacter.DefaultTitle = importedCharacter.DefaultTitle;
                        }
                    } catch (Exception ex) {
                        PluginLog.Error(ex, "Error decoding clipboard text");
                    }
                }

                DrawCharacterView(selectedCharacter);
            } else {
                
                ImGui.Text("Honorific Options");
                ImGui.Separator();

                ImGui.Checkbox("Display Coloured Titles", ref config.ShowColoredTitles);
                
                #if DEBUG
                ImGui.Checkbox("[DEBUG] Open config window on startup", ref config.DebugOpenOnStatup);
                #endif

                if (Plugin.IsDebug && ImGui.TreeNode("Debugging")) {
                    PerformanceMonitors.DrawTable();
                    ImGui.Separator();

                    ImGui.Text("Modified Nameplates:");
                    foreach (var (m, o) in plugin.ModifiedNamePlates) {
                        unsafe {
                            var npi = (RaptureAtkModule.NamePlateInfo*)m;
                            ImGui.PushID(new nint(npi));
                            Util.ShowStruct(npi);
                            ImGui.PopID();
                        }
                        


                    }
                    
                }
                
                
                

            }
            
        }
        ImGui.EndChild();
    }

    private void DrawCharacterView(CharacterConfig? characterConfig) {
        if (characterConfig == null) return;
        
        if (ImGui.BeginTable("TitlesTable", config.ShowColoredTitles ? 6 : 4)) {
            ImGui.TableSetupColumn("Enable", ImGuiTableColumnFlags.WidthFixed, checkboxSize * 4 + 3);
            ImGui.TableSetupColumn("Title", ImGuiTableColumnFlags.WidthFixed, 150 * ImGuiHelpers.GlobalScale);
            ImGui.TableSetupColumn("Prefix", ImGuiTableColumnFlags.WidthFixed, checkboxSize * 2);
            if (config.ShowColoredTitles) {
                ImGui.TableSetupColumn("Colour", ImGuiTableColumnFlags.WidthFixed, checkboxSize * 2);
                ImGui.TableSetupColumn("Glow", ImGuiTableColumnFlags.WidthFixed, checkboxSize * 2);
            }
            ImGui.TableSetupColumn("Condition", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableHeadersRow();

            var deleteIndex = -1;
            var moveUp = -1;
            for (var i = 0; i < characterConfig.CustomTitles.Count; i++) {
                ImGui.PushID($"title_{i}");
                var title = characterConfig.CustomTitles[i];
                ImGui.TableNextRow();
                ImGui.TableNextColumn();

                ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, Vector2.One);
                ImGui.PushFont(UiBuilder.IconFont);

                if (ImGui.Button($"{(char)FontAwesomeIcon.Trash}##delete", new Vector2(checkboxSize)) && ImGui.GetIO().KeyShift) {
                    deleteIndex = i;
                }

                if (ImGui.IsItemHovered() && !ImGui.GetIO().KeyShift) {
                    ImGui.PopFont();
                    ImGui.SetTooltip("Hold SHIFT to delete.");
                    ImGui.PushFont(UiBuilder.IconFont);
                }
                ImGui.SameLine();
                
                if (i > 0) {
                    if (ImGui.Button($"{(char)FontAwesomeIcon.ArrowUp}##up", new Vector2(checkboxSize))) {
                        moveUp = i;
                    }
                } else {
                    ImGui.Dummy(new Vector2(checkboxSize));
                }
                
                ImGui.SameLine();

                if (i < characterConfig.CustomTitles.Count - 1) {
                    if (ImGui.Button($"{(char)FontAwesomeIcon.ArrowDown}##down", new Vector2(checkboxSize))) {
                        moveUp = i + 1;
                    }
                } else {
                    ImGui.Dummy(new Vector2(checkboxSize));
                }
                
                ImGui.SameLine();
                ImGui.PopFont();
                ImGui.PopStyleVar();
                if (ImGui.Checkbox("##enable", ref title.Enabled)) {
                    if (title.Enabled) {
                        foreach (var t in characterConfig.CustomTitles.Where (t => t.TitleCondition == title.TitleCondition && t.ConditionParam0 == title.ConditionParam0)) {
                            t.Enabled = false;
                        }
                        title.Enabled = true;
                    }
                }
                
                DrawTitleCommon(title);

                ImGui.SetNextItemWidth(150);
                if (ImGui.BeginCombo("##conditionType", title.TitleCondition.GetAttribute<DescriptionAttribute>()?.Description ?? $"{title.TitleCondition}")) {
                    foreach (var v in Enum.GetValues<TitleConditionType>()) {
                        if (ImGui.Selectable(v.GetAttribute<DescriptionAttribute>()?.Description ?? $"{v}", v == title.TitleCondition)) {
                            if (title.TitleCondition != v) {
                                title.TitleCondition = v;
                                title.ConditionParam0 = 0;
                            }
                        }
                    }
                    ImGui.EndCombo();
                }

                switch (title.TitleCondition) {
                    case TitleConditionType.None: {
                        break;
                    }
                    case TitleConditionType.ClassJob: {
                        var sheet = PluginService.Data.GetExcelSheet<ClassJob>();
                        if (sheet != null) {
                            ImGui.SameLine();
                            ImGui.SetNextItemWidth(-1);
                            var selected = sheet.GetRow((uint)title.ConditionParam0);
                            if (ImGui.BeginCombo("##conditionClassJob", title.ConditionParam0 == 0 ? "Select..." : selected?.Abbreviation.RawString ?? $"Unknown({title.ConditionParam0}")) {
                                foreach (var cj in sheet) {
                                    if (cj.RowId == 0) continue;
                                    if (ImGui.Selectable(cj.Abbreviation.RawString, title.ConditionParam0 == cj.RowId)) {
                                        title.ConditionParam0 = (int)cj.RowId;
                                    }
                                }
                                ImGui.EndCombo();
                            }
                            
                        }
                        break;
                    }

                    case TitleConditionType.JobRole: {
                        var selected = (ClassJobRole)title.ConditionParam0;
                        ImGui.SameLine();
                        ImGui.SetNextItemWidth(-1);
                        if (ImGui.BeginCombo("##conditionRole", selected.GetAttribute<DescriptionAttribute>()?.Description ?? $"{selected}")) {
                            foreach (var v in Enum.GetValues<ClassJobRole>()) {
                                if (v == ClassJobRole.None) continue;
                                if (ImGui.Selectable(v.GetAttribute<DescriptionAttribute>()?.Description ?? $"{v}", v == selected)) {
                                    title.ConditionParam0 = (int) v;
                                }
                            }
                            ImGui.EndCombo();
                        }
                        
                        break;
                    }
                }

                ImGui.PopID();
            }

            if (deleteIndex >= 0) {
                characterConfig.CustomTitles.RemoveAt(deleteIndex);
            } else if (moveUp > 0) {
                var move = characterConfig.CustomTitles[moveUp];
                characterConfig.CustomTitles.RemoveAt(moveUp);
                characterConfig.CustomTitles.Insert(moveUp - 1, move);
            }
            
            ImGui.PushID($"title_default");
            // Default Title
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, Vector2.One);
            
            ImGui.PushFont(UiBuilder.IconFont);
            if (ImGui.Button($"{(char)FontAwesomeIcon.Plus}##add", new Vector2(checkboxSize))) {
                characterConfig.CustomTitles.Add(new CustomTitle());
            }
            
            ImGui.PopFont();
            
            ImGui.SameLine();
            ImGui.Dummy(new Vector2(checkboxSize));
            ImGui.SameLine();
            ImGui.Dummy(new Vector2(checkboxSize));
            ImGui.SameLine();
            ImGui.PopStyleVar();
            ImGui.Checkbox("##enable", ref characterConfig.DefaultTitle.Enabled);
            
            checkboxSize = ImGui.GetItemRectSize().X;
            DrawTitleCommon(characterConfig.DefaultTitle);
            
            ImGui.TextDisabled("Default Title");
            
            ImGui.PopID();
            
            ImGui.EndTable();
        }
    }



    private static List<UIColor>? _foregroundColours = null;
    private static List<UIColor>? _glowColours = null;
    
    public static Vector4 UiColorToVector4(uint col) {
        var fa = col & 255;
        var fb = (col >> 8) & 255;
        var fg = (col >> 16) & 255;
        var fr = (col >> 24) & 255;
        return new Vector4(fr / 255f, fg / 255f, fb / 255f, fa / 255f);
    }

    private static void BuildColorList() {
        _foregroundColours = new List<UIColor>();
        _glowColours = new List<UIColor>();
        var s = PluginService.Data.GetExcelSheet<UIColor>();
        if (s == null) return;
        foreach (var c in s) {
            if (c.RowId == 0) continue;
            if (_foregroundColours.All(u => u.UIForeground != c.UIForeground)) {
                _foregroundColours.Add(c);
            }
            if (_glowColours.All(u => u.UIGlow != c.UIGlow)) {
                _glowColours.Add(c);
            }
        }

        _foregroundColours.Sort((a, b) => {
            var aRgb = UiColorToVector4(a.UIForeground);
            var bRgb = UiColorToVector4(b.UIForeground);
            ImGui.ColorConvertRGBtoHSV(aRgb.X, aRgb.Y, aRgb.Z, out var aH, out var aS, out var aV);
            ImGui.ColorConvertRGBtoHSV(bRgb.X, bRgb.Y, bRgb.Z, out var bH, out var bS, out var bV);
            if (aH < bH) return -1;
            if (aH > bH) return 1;
            if (aS < bS) return -1;
            if (aS > bS) return 1;
            if (aV < bV) return -1;
            if (aV > bV) return 1;
            return 0;
        });
        
        _glowColours.Sort((a, b) => {
            var aRgb = UiColorToVector4(a.UIGlow);
            var bRgb = UiColorToVector4(b.UIGlow);
            ImGui.ColorConvertRGBtoHSV(aRgb.X, aRgb.Y, aRgb.Z, out var aH, out var aS, out var aV);
            ImGui.ColorConvertRGBtoHSV(bRgb.X, bRgb.Y, bRgb.Z, out var bH, out var bS, out var bV);
            if (aH < bH) return -1;
            if (aH > bH) return 1;
            if (aS < bS) return -1;
            if (aS > bS) return 1;
            if (aV < bV) return -1;
            if (aV > bV) return 1;
            return 0;
        });

    }


    private void DrawColorPicker(string label, ref ushort colorKey, bool glow, IEnumerable<UIColor>? colors = null) {
        colors ??= PluginService.Data.GetExcelSheet<UIColor>();

        if (colors == null) {
            ImGui.TextColored(ImGuiColors.DalamudRed, "Error");
            return;
        }
        
        var comboOpen = false;
        var displayColor = PluginService.Data.GetExcelSheet<UIColor>()?.GetRow(colorKey);
        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
        if (displayColor == null || displayColor.RowId == 0) {
            ImGui.PushStyleColor(ImGuiCol.FrameBg, 0xFFFFFFFF);
            ImGui.PushStyleColor(ImGuiCol.FrameBgActive, 0xFFFFFFFF);
            ImGui.PushStyleColor(ImGuiCol.FrameBgHovered, 0xFFFFFFFF);
            var p = ImGui.GetCursorScreenPos();
            var dl = ImGui.GetWindowDrawList();
            comboOpen = ImGui.BeginCombo($"##color_{ImGui.GetID(label)}", " ", ImGuiComboFlags.HeightLargest);
            dl.AddLine(p, p + new Vector2(checkboxSize), 0xFF0000FF, 3f * ImGuiHelpers.GlobalScale);
            ImGui.PopStyleColor(3);
        } else {
            var vec4 = UiColorToVector4((glow ? displayColor.UIGlow : displayColor.UIForeground) | 0x000000FF);
            ImGui.PushStyleColor(ImGuiCol.FrameBg, vec4);
            ImGui.PushStyleColor(ImGuiCol.FrameBgActive, vec4);
            ImGui.PushStyleColor(ImGuiCol.FrameBgHovered, vec4);
            comboOpen = ImGui.BeginCombo(label, "  ", ImGuiComboFlags.HeightLargest);
            ImGui.PopStyleColor(3);
        }
        
        if (comboOpen) {
            if (ImGui.ColorButton($"##ColorPick_clear", Vector4.One, ImGuiColorEditFlags.NoTooltip)) {
                colorKey = 0;
                ImGui.CloseCurrentPopup();
            }
            var dl = ImGui.GetWindowDrawList();
            dl.AddLine(ImGui.GetItemRectMin(), ImGui.GetItemRectMax(), 0xFF0000FF, 3f * ImGuiHelpers.GlobalScale);
            ImGui.SameLine();

            var i = 1;
            foreach (var c in colors) {
                if (ImGui.ColorButton($"##ColorPick_{i}_{c.RowId}", UiColorToVector4(glow ? c.UIGlow : c.UIForeground), ImGuiColorEditFlags.NoTooltip)) {
                    colorKey = (ushort)c.RowId;
                    ImGui.CloseCurrentPopup();
                }

                if (Plugin.IsDebug && ImGui.IsItemHovered()) {
                    ImGui.SetTooltip($"ColorKey#{c.RowId}");
                }
                if ( i++ % 10 != 9) ImGui.SameLine();
            }


            ImGui.EndCombo();
        }
    }
    
    
    private void DrawTitleCommon(CustomTitle title) {
        ImGui.TableNextColumn();
        ImGui.SetNextItemWidth(-1);
        ImGui.InputText($"##title", ref title.Title, 25);
        ImGui.TableNextColumn();
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + ImGui.GetContentRegionAvail().X / 2 - checkboxSize / 2);
        ImGui.Checkbox($"##prefix", ref title.IsPrefix);
        checkboxSize = ImGui.GetItemRectSize().X;
        if (config.ShowColoredTitles) {
            ImGui.TableNextColumn();
            DrawColorPicker("##colour", ref title.ColorKey, false, _foregroundColours);
            ImGui.TableNextColumn();
            DrawColorPicker("##glow", ref title.GlowKey, true, _glowColours); 
        }

        ImGui.TableNextColumn();
    }

    public override void OnClose() {
        PluginService.PluginInterface.SavePluginConfig(config);
        base.OnClose();
    }
}

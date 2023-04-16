using System;
using System.ComponentModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Numerics;
using System.Text;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Interface.Windowing;
using Dalamud.Logging;
using Dalamud.Utility;
using ImGuiNET;
using Lumina.Excel.GeneratedSheets;
using Newtonsoft.Json;
using World = Lumina.Excel.GeneratedSheets.World;

namespace Honorific; 

public class ConfigWindow : Window {
    private PluginConfig config;

    public ConfigWindow(string name, PluginConfig config) : base(name) {
        this.config = config;
        
        SizeConstraints = new WindowSizeConstraints {
            MinimumSize = ImGuiHelpers.ScaledVector2(800, 400),
            MaximumSize = new Vector2(float.MaxValue)
        };

        Size = ImGuiHelpers.ScaledVector2(800, 500);
        SizeCondition = ImGuiCond.Once;
    }


    private Vector2 IconButtonSize = new Vector2(16);

    private float checkboxSize = 36;
    

    public void DrawCharacterList() {

        foreach (var (worldId, characters) in config.WorldCharacterDictionary.ToArray()) {
            var world = PluginService.Data.GetExcelSheet<World>()?.GetRow(worldId);
            if (world == null) continue;
            
            ImGui.TextDisabled($"{world.Name.RawString}");
            ImGui.Separator();

            foreach (var (name, characterConfig) in characters.ToArray()) {

                if (Plugin.IpcAssignedTitles.ContainsKey((name, worldId))) {
                    if (selectedCharacter == characterConfig) selectedCharacter = null;
                    ImGui.BeginGroup();
                    ImGui.TextDisabled($"{name}");
                    ImGui.SameLine();
                    ImGui.Dummy(new Vector2(ImGui.GetContentRegionAvail().X, ImGui.GetItemRectSize().Y));
                    ImGui.EndGroup();
                    if (ImGui.IsItemHovered()) {
                        ImGui.SetTooltip($"{name}'s title has been assigned by another plugin.");
                    }
                } else {
                    if (ImGui.Selectable($"{name}##{world.Name.RawString}", selectedCharacter == characterConfig)) {
                        selectedCharacter = characterConfig;
                    }
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

    }

    private CharacterConfig? selectedCharacter;

    public override void Draw() {
        ImGui.BeginGroup();
        {
            if (ImGui.BeginChild("character_select", ImGuiHelpers.ScaledVector2(240, 0) - IconButtonSize with { X = 0 }, true)) {
                DrawCharacterList();
            }
            ImGui.EndChild();


            if (PluginService.ClientState.LocalPlayer != null) {
                if (ImGuiComponents.IconButton(FontAwesomeIcon.User)) {
                    if (PluginService.ClientState.LocalPlayer != null) {
                        config.TryAddCharacter(PluginService.ClientState.LocalPlayer.Name.TextValue, PluginService.ClientState.LocalPlayer.HomeWorld.Id);
                    }
                }
                IconButtonSize = ImGui.GetItemRectSize() + ImGui.GetStyle().ItemSpacing;
                if (ImGui.IsItemHovered()) ImGui.SetTooltip("Add current character");
                
                ImGui.SameLine();
                if (ImGuiComponents.IconButton(FontAwesomeIcon.DotCircle)) {
                    if (PluginService.Targets.Target is PlayerCharacter pc) {
                        config.TryAddCharacter(pc.Name.TextValue, pc.HomeWorld.Id);
                    }
                }
                if (ImGui.IsItemHovered()) ImGui.SetTooltip("Add targeted character");
            }
        }
        ImGui.EndGroup();
        
        ImGui.SameLine();
        if (ImGui.BeginChild("character_view", ImGuiHelpers.ScaledVector2(0), true)) {
            if (selectedCharacter != null) {
                
                
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
            }
            
        }
        ImGui.EndChild();

    }

    private void DrawCharacterView(CharacterConfig? characterConfig) {
        if (characterConfig == null) return;
        
        if (ImGui.BeginTable("TitlesTable", 4)) {
            ImGui.TableSetupColumn("Enable", ImGuiTableColumnFlags.WidthFixed, checkboxSize * 4 + 3);
            ImGui.TableSetupColumn("Title", ImGuiTableColumnFlags.WidthFixed, 150 * ImGuiHelpers.GlobalScale);
            ImGui.TableSetupColumn("Prefix", ImGuiTableColumnFlags.WidthFixed, checkboxSize * 2);
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
                ImGui.Checkbox("##enable", ref title.Enabled);
                
                DrawTitleCommon(title);

                ImGui.SetNextItemWidth(150);
                if (ImGui.BeginCombo("##conditionType", title.TitleCondition.GetAttribute<DescriptionAttribute>()?.Description ?? $"{title.TitleCondition}")) {
                    foreach (var v in Enum.GetValues<TitleConditionType>()) {
                        if (v == TitleConditionType.None) continue;
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

    private bool DrawTitleCommon(CustomTitle title) {
        var modified = false;
        
        
        ImGui.TableNextColumn();
        ImGui.SetNextItemWidth(-1);
        modified |= ImGui.InputText($"##title", ref title.Title, 25);
        
        ImGui.TableNextColumn();

        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + ImGui.GetContentRegionAvail().X / 2 - checkboxSize / 2);
        modified |= ImGui.Checkbox($"##prefix", ref title.IsPrefix);

        ImGui.TableNextColumn();
        
        return modified;
    }

    public override void OnClose() {
        PluginService.PluginInterface.SavePluginConfig(config);
        base.OnClose();
    }
}

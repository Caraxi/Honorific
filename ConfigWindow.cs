using System;
using System.ComponentModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Numerics;
using System.Text;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.Config;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Dalamud.Memory;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using FFXIVClientStructs.Interop;
using ImGuiNET;
using Lumina.Excel.GeneratedSheets;
using Newtonsoft.Json;
using Title = Lumina.Excel.GeneratedSheets2.Title;
using World = Lumina.Excel.GeneratedSheets.World;

namespace Honorific; 

public class ConfigWindow : Window {
    private readonly PluginConfig config;
    private readonly Plugin plugin;

    public ConfigWindow(string name, Plugin plugin, PluginConfig config) : base(name, ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse) {
        this.config = config;
        this.plugin = plugin;
    }

    public override void PreDraw() {
        SizeConstraints = new WindowSizeConstraints {
            MinimumSize = new Vector2(800, 400),
            MaximumSize = ImGuiHelpers.MainViewport.Size * 1 / ImGuiHelpers.GlobalScale * 0.95f
        };
    }
    
    private Vector2 iconButtonSize = new(16);
    private float checkboxSize = 36;

    private string gameTitleSearch = string.Empty;

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

            foreach (var objectId in Plugin.IpcAssignedTitles.Keys.ToArray()) {
                var chr = PluginService.Objects.FirstOrDefault(c => c.EntityId == objectId);
                if (chr is not IPlayerCharacter pc) continue;
                

                var world = PluginService.Data.GetExcelSheet<World>()?.GetRow(pc.HomeWorld.Id);
                if (world == null) continue;
                if (ImGui.Selectable($"{chr.Name}##{world.Name.RawString}##ipc", selectedName == chr.Name.TextValue && selectedWorld == pc.HomeWorld.Id)) {
                    config.TryGetCharacterConfig(chr.Name.TextValue, pc.HomeWorld.Id, out selectedCharacter);
                    selectedCharacter ??= new CharacterConfig();
                    selectedName = pc.Name.TextValue;
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
    private float kofiButtonOffset;

    private CustomTitle forcedTitleCommandGeneratorTitle = new();
    
    public override void Draw() {
        var modified = false;
        ImGui.BeginGroup();
        {
            if (ImGui.BeginChild("character_select", ImGuiHelpers.ScaledVector2(240, 0) - iconButtonSize with { X = 0 }, true)) {
                DrawCharacterList();
            }
            ImGui.EndChild();

            var charListSize = ImGui.GetItemRectSize().X;

            if (PluginService.ClientState.LocalPlayer != null) {
                if (ImGuiComponents.IconButton(FontAwesomeIcon.User)) {
                    if (PluginService.ClientState.LocalPlayer != null) {
                        config.TryAddCharacter(PluginService.ClientState.LocalPlayer.Name.TextValue, PluginService.ClientState.LocalPlayer.HomeWorld.Id);
                    }
                }
                
                if (ImGui.IsItemHovered()) ImGui.SetTooltip("Add current character");
                
                ImGui.SameLine();
                if (ImGuiComponents.IconButton(FontAwesomeIcon.DotCircle)) {
                    if (PluginService.Targets.Target is IPlayerCharacter pc) {
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
            
            if (!config.HideKofi) {
                ImGui.SameLine();
                if (kofiButtonOffset > 0) ImGui.SetCursorPosX(MathF.Max(ImGui.GetCursorPosX(), charListSize - kofiButtonOffset + ImGui.GetStyle().WindowPadding.X));
                if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Coffee, "Support", new Vector4(1, 0.35f, 0.35f, 1f), new Vector4(1, 0.35f, 0.35f, 0.9f), new Vector4(1, 0.35f, 0.35f, 75f))) {
                    Util.OpenLink("https://ko-fi.com/Caraxi");
                }
                if (ImGui.IsItemHovered()) {
                    ImGui.SetTooltip("Support on Ko-fi");
                }
                kofiButtonOffset = ImGui.GetItemRectSize().X;
            }
            
            
        }
        ImGui.EndGroup();
        
        ImGui.SameLine();
        if (ImGui.BeginChild("character_view", ImGuiHelpers.ScaledVector2(0), true)) {
            if (selectedCharacter != null) {
                var activePlayer = PluginService.Objects.FirstOrDefault(t => t is IPlayerCharacter playerCharacter && playerCharacter.Name.TextValue == selectedName && playerCharacter.HomeWorld.Id == selectedWorld);

                if (activePlayer is IPlayerCharacter player) {
                    var option = UiConfigOption.NamePlateNameTitleTypeOther;
                    var optionGroupNameId = 7712U;
                    if (player.StatusFlags.HasFlag(StatusFlags.PartyMember)) {
                        option = UiConfigOption.NamePlateNameTitleTypeParty;
                        optionGroupNameId = 7710;
                    } else if (player.StatusFlags.HasFlag(StatusFlags.AllianceMember)) {
                        option = UiConfigOption.NamePlateNameTitleTypeAlliance;
                        optionGroupNameId = 7711;
                    } else if (player.StatusFlags.HasFlag(StatusFlags.Friend)) {
                        option = UiConfigOption.NamePlateNameTitleTypeFriend;
                        optionGroupNameId = 7719;
                    } else if (player == PluginService.ClientState.LocalPlayer) {
                        option = UiConfigOption.NamePlateNameTitleTypeSelf;
                        optionGroupNameId = 7700;
                    }
                    if (PluginService.GameConfig.TryGet(option, out bool isTitleVisible)) {
                        if (!isTitleVisible) {
                            var groupName = PluginService.Data.GetExcelSheet<Addon>()!.GetRow(optionGroupNameId)!.Text.ToDalamudString().TextValue;
                            var settingName = PluginService.Data.GetExcelSheet<Addon>()!.GetRow(7706)!.Text.ToDalamudString().TextValue;
                            var text = $"The title of this character is currently hidden by the game options:\n\t{groupName} / {settingName}";
                            var textSize = ImGui.CalcTextSize(text);
                            ImGui.PushStyleVar(ImGuiStyleVar.ChildBorderSize, 3);
                            ImGui.PushStyleColor(ImGuiCol.Border, 0x880000FF);
                            if (ImGui.BeginChild("warning", new Vector2(ImGui.GetContentRegionAvail().X, textSize.Y + ImGui.GetStyle().WindowPadding.Y * 2), true)) {
                                ImGui.Text($"{text}");
                            }
                            ImGui.EndChild();
                            ImGui.PopStyleVar();
                            ImGui.PopStyleColor();
                        }
                    }
                }
                
                if (Plugin.IsDebug && ImGui.TreeNode("DEBUG INFO")) {
                    
                    if (activePlayer is IPlayerCharacter pc && plugin.TryGetTitle(pc, out var expectedTitle) && expectedTitle != null) {
                        ImGui.TextDisabled($"ObjectID: {activePlayer:X8}");
                        unsafe {
                            var raptureAtkModule = Framework.Instance()->GetUIModule()->GetRaptureAtkModule();

                            
                            for (var i = 0; i < 50 && i < raptureAtkModule->NameplateInfoCount; i++) {
                                var npi = raptureAtkModule->NamePlateInfoEntries.GetPointer(i);
                                if (npi->ObjectId.ObjectId == pc.EntityId) {
                                    ImGui.Text($"NamePlateStruct: [{(ulong)npi:X}] for {npi->ObjectId.ObjectId:X8}");
                                    Util.ShowStruct(*npi, (ulong) npi, true, new string[] { $"{(ulong)npi:X}"});

                                    var expectedTitleSeString = expectedTitle.ToSeString(true, config.ShowColoredTitles);

                                    var currentTitle = MemoryHelper.ReadSeString(&npi->DisplayTitle);
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
                
                
                

                if (activePlayer != null && Plugin.IpcAssignedTitles.TryGetValue(activePlayer.EntityId, out var title)) {

                    ImGui.Text("This character's title is currently assigned by another plugin.");
                    if (Plugin.IsDebug && ImGui.Button("Clear IPC Assignment")) {
                        Plugin.IpcAssignedTitles.Remove(activePlayer.EntityId);
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
                        DrawTitleCommon(title, ref modified);

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
                        PluginService.Log.Error(ex, "Error decoding clipboard text");
                    }
                }

                DrawCharacterView(selectedCharacter, activePlayer, ref modified);
            } else {
                
                ImGui.Text("Honorific Options");
                ImGui.Separator();

                ImGui.Checkbox("Display Coloured Titles", ref config.ShowColoredTitles);
                ImGui.Checkbox("Display titles in 'Examine' window.", ref config.ApplyToInspect);
                
                if (ImGuiExt.TriStateCheckbox("##HideVanillaAll", out var setAll, config.HideVanillaSelf, config.HideVanillaParty, config.HideVanillaAlliance, config.HideVanillaFriends, config.HideVanillaOther)) {
                    if (setAll != null) {
                        config.HideVanillaSelf = setAll.Value;
                        config.HideVanillaParty = setAll.Value;
                        config.HideVanillaAlliance = setAll.Value;
                        config.HideVanillaFriends = setAll.Value;
                        config.HideVanillaOther = setAll.Value;
                    }
                }
                
                ImGui.SameLine();
                if (ImGui.TreeNode("Hide Vanilla Titles")) {
                    ImGui.Checkbox("Self##HideVanillaSelf", ref config.HideVanillaSelf);
                    ImGui.Checkbox("Party Members##HideVanillaParty", ref config.HideVanillaParty);
                    ImGui.Checkbox("Alliance Members##HideVanillaAlliance", ref config.HideVanillaAlliance);
                    ImGui.Checkbox("Friends##HideVanillaFriends", ref config.HideVanillaFriends);
                    ImGui.Checkbox("Other Players##HideVanillaOther", ref config.HideVanillaOther);
                    
                    ImGui.TreePop();
                }
                
                ImGui.SameLine();
                ImGuiComponents.HelpMarker("Hides titles that were not set by honorific.");
                
                
                
                ImGui.Checkbox("Hide Ko-fi Support button", ref config.HideKofi);
                
                #if DEBUG
                ImGui.Checkbox("[DEBUG] Open config window on startup", ref config.DebugOpenOnStatup);
                #endif

                if (Plugin.IsDebug && ImGui.TreeNode("Debugging")) {
                    PerformanceMonitors.DrawTable();
                    ImGui.Separator();

                    var target = PluginService.Targets.SoftTarget ?? PluginService.Targets.Target;
                    if (target is IPlayerCharacter pc) {
                        if (ImGui.Button($"Test SET IPC for '{target.Name.TextValue}'")) {
                            PluginService.PluginInterface.GetIpcSubscriber<ICharacter, string, object>("Honorific.SetCharacterTitle").InvokeAction(pc, JsonConvert.SerializeObject(new TitleData {Color = new Vector3(1, 0, 0), Glow = new Vector3(0, 1, 0), Title = "Test Title", IsPrefix = true}));
                        }
                        if (ImGui.Button($"Test CLEAR IPC for '{target.Name.TextValue}'")) {
                            PluginService.PluginInterface.GetIpcSubscriber<ICharacter, object>("Honorific.ClearCharacterTitle").InvokeAction(pc);
                        }
                        ImGui.Separator();
                    }


                    if (ImGui.BeginTable("nameplates", 5)) {
                        
                        ImGui.TableSetupColumn("Index", ImGuiTableColumnFlags.WidthFixed, ImGuiHelpers.GlobalScale * 40);
                        ImGui.TableSetupColumn("ObjectID", ImGuiTableColumnFlags.WidthFixed, ImGuiHelpers.GlobalScale * 120);
                        ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthFixed, ImGuiHelpers.GlobalScale * 180);
                        ImGui.TableSetupColumn("Title", ImGuiTableColumnFlags.WidthFixed, ImGuiHelpers.GlobalScale * 180);
                        ImGui.TableSetupColumn("Data", ImGuiTableColumnFlags.WidthStretch);

                        
                        ImGui.TableHeadersRow();
                        unsafe {
                            var ratkm = Framework.Instance()->GetUIModule()->GetRaptureAtkModule();
                            for (var i = 0; i < 50 && i < ratkm->NameplateInfoCount; i++) {
                                var npi = ratkm->NamePlateInfoEntries.GetPointer(i);
                                if (npi->ObjectId.ObjectId == 0 && !ImGui.GetIO().KeyShift) continue;
                                var color = plugin.ModifiedNamePlates.ContainsKey((ulong)npi);
                                ImGui.PushID($"namePlateInfo_{i}");
                                if (color) ImGui.PushStyleColor(ImGuiCol.Text, ImGuiColors.DalamudOrange);
                                ImGui.TableNextColumn();
                                ImGui.Text($"{i:00}");
                                ImGui.TableNextColumn();
                                ImGui.Text($"{npi->ObjectId.ObjectId:X8}:{npi->ObjectId.Type:X2}");
                                ImGui.TableNextColumn();
                                var name = MemoryHelper.ReadSeString(&npi->Name);
                                ImGui.Text($"{name.TextValue}");
                                ImGui.TableNextColumn();
                                var title = MemoryHelper.ReadSeString(&npi->DisplayTitle);
                                ImGui.Text($"{title.TextValue}");
                                if (color) ImGui.PopStyleColor();
                                ImGui.TableNextColumn();
                                Util.ShowStruct(npi);
                                ImGui.PopID();
                            }
                        }
                        
                        ImGui.EndTable();
                    }
                }


                if (ImGui.CollapsingHeader("Commands")) {
                    
                    using (ImRaii.PushIndent()) {
                        if (ImGui.CollapsingHeader("Toggle Titles", ImGuiTreeNodeFlags.DefaultOpen)) {
                            using (ImRaii.PushIndent()) {
                                using (ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, Vector2.Zero)) {
                                    ImGui.TextColored(ImGuiColors.DalamudWhite2, "/honorific title");
                                    ImGui.SameLine();
                                    ImGui.TextColored(ImGuiColors.DalamudOrange, " <enable|disable|toggle>");
                                    if (ImGui.IsItemHovered()) ImGui.SetTooltip("Any one of Enable, Disable, or Toggle");
                                    ImGui.SameLine();
                                    ImGui.TextColored(ImGuiColors.DalamudViolet, " <title>");
                                    if (ImGui.IsItemHovered()) ImGui.SetTooltip("Any configured title on the active character.\nIf multiple titles are configured with the same text, only the first listed will be used.\n\nA titles Unique ID may also be used, which can be obtained by right clicking the enable checkbox.");
                                }
                        
                                ImGui.Spacing();
                                ImGui.Text("Enable, Disable, or Toggle a title on the current character.");
                            }
                        }
                        
                        if (ImGui.CollapsingHeader("Forced Titles", ImGuiTreeNodeFlags.DefaultOpen)) {
                            using (ImRaii.PushIndent()) {
                                
                                
                                using (ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, Vector2.Zero)) {
                                    
                                    ImGui.TextColored(ImGuiColors.DalamudWhite2, "/honorific force set");
                                    ImGui.SameLine();
                                    ImGui.TextColored(ImGuiColors.DalamudViolet, " <title>");
                                    if (ImGui.IsItemHovered()) ImGui.SetTooltip("Title Text");
                                    ImGui.SameLine();
                                    ImGui.TextColored(ImGuiColors.DalamudWhite2, " | ");
                                    ImGui.SameLine();
                                    ImGui.TextColored(ImGuiColors.ParsedPink, "[prefix|suffix]");
                                    if (ImGui.IsItemHovered()) ImGui.SetTooltip("(Optional) Prefix or Suffix\nSets the title to prefix or suffix position.");
                                    ImGui.SameLine();
                                    ImGui.TextColored(ImGuiColors.DalamudWhite2, " | ");
                                    ImGui.SameLine();
                                    ImGui.TextColored(ImGuiColors.ParsedPink, "#<HexColor>");
                                    if (ImGui.IsItemHovered()) ImGui.SetTooltip("(Optional) #RRGGBB hex colour code for the main colour of the title.");
                                    ImGui.SameLine();
                                    ImGui.TextColored(ImGuiColors.DalamudWhite2, " | ");
                                    ImGui.SameLine();
                                    ImGui.TextColored(ImGuiColors.ParsedPink, "#<HexGlow>");
                                    if (ImGui.IsItemHovered()) ImGui.SetTooltip("(Optional) #RRGGBB hex colour code for the glow colour of the title.\nCan only be used if the main colour is set.");
                                }
                                ImGui.TextColored(ImGuiColors.DalamudWhite2, "/honorific force clear");
                                
                                ImGui.Spacing();

                                ImGui.Text("Sets a title that will override all other configured titles.");


                                if (ImGui.TreeNode("Command Generator")) {
                                    
                                    var b = false;
                                
                                    if (ImGui.BeginTable("CommandGeneratorTitleTable", 5)) {
                                        var t = forcedTitleCommandGeneratorTitle;
                                        ImGui.TableSetupColumn("Title", ImGuiTableColumnFlags.WidthFixed, 150 * ImGuiHelpers.GlobalScale);
                                        ImGui.TableSetupColumn("Prefix", ImGuiTableColumnFlags.WidthFixed, checkboxSize * 2);
                                        ImGui.TableSetupColumn("Colour", ImGuiTableColumnFlags.WidthFixed, checkboxSize * 2);
                                        ImGui.TableSetupColumn("Glow", ImGuiTableColumnFlags.WidthFixed, checkboxSize * 2);
                                        ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthStretch);
                                        ImGui.TableHeadersRow();
                                        DrawTitleCommon(t, ref b);


                                        string c;
                                        if (string.IsNullOrWhiteSpace(t.Title)) {
                                            c = "/honorific force clear";
                                        } else {
                                            c = $"/honorific force set {t.Title}";
                                            if (t.IsPrefix) c += " | prefix";
                                            if (t.Color != null) {
                                                c += " | #";
                                                c += $"{(byte)(t.Color.Value.X * 255):X2}";
                                                c += $"{(byte)(t.Color.Value.Y * 255):X2}";
                                                c += $"{(byte)(t.Color.Value.Z * 255):X2}";
                                                
                                                if (t.Glow != null) {
                                                    c += " | #";
                                                    c += $"{(byte)(t.Glow.Value.X * 255):X2}";
                                                    c += $"{(byte)(t.Glow.Value.Y * 255):X2}";
                                                    c += $"{(byte)(t.Glow.Value.Z * 255):X2}";
                                                }
                                            }
                                        }

                                        if (ImGui.GetContentRegionAvail().X > ImGui.CalcTextSize(c).X + ImGui.GetStyle().FramePadding.X * 2) {
                                            ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
                                            ImGui.InputText("##commandOutput", ref c, 255, ImGuiInputTextFlags.ReadOnly | ImGuiInputTextFlags.AutoSelectAll);
                                            ImGui.EndTable();
                                        } else {
                                            ImGui.EndTable();
                                            ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
                                            ImGui.InputText("##commandOutput", ref c, 255, ImGuiInputTextFlags.ReadOnly | ImGuiInputTextFlags.AutoSelectAll);
                                        }
                                    }

                                    ImGui.TreePop();
                                }
                            }
                        }
                    }
                }
            }
            
        }
        ImGui.EndChild();
    }

    private void DrawCharacterView(CharacterConfig? characterConfig, IGameObject? activeCharacter, ref bool modified) {
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
            
            if (characterConfig.Override.Enabled) {
                ImGui.PushID($"title_override");
                // Override Title
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                if (ImGui.Button("Clear##ClearOverride", new Vector2(ImGui.GetContentRegionAvail().X, checkboxSize))) {
                    characterConfig.Override.Enabled = false;
                    characterConfig.Override.Title = string.Empty;
                }

                if (ImGui.IsItemHovered()) {
                    ImGui.SetTooltip("Clear the forced title.");
                }
                
                DrawTitleCommon(characterConfig.Override, ref modified);
                ImGui.TextDisabled("Override Title");
                if (ImGui.IsItemHovered()) {
                    ImGui.SetTooltip("Title has been assigned using \"/honorific force set\" command.");
                }
                ImGui.PopID();
                ImGui.TableNextRow();
                ImGui.TableNextRow();
            }
            
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


                var isDuplicateEnabled = characterConfig.UseRandom == false && i > 0 && characterConfig.CustomTitles.Take(i).Any(t => t.Enabled && t.TitleCondition == title.TitleCondition && t.ConditionParam0 == title.ConditionParam0);
                var isActive = characterConfig.UseRandom && !isDuplicateEnabled && characterConfig.ActiveTitle == title;
                
                using (ImRaii.PushColor(ImGuiCol.CheckMark, ImGui.GetColorU32(ImGuiCol.CheckMark) & 0x40FFFFFF, isDuplicateEnabled)) {
                    using (ImRaii.PushColor(ImGuiCol.CheckMark, ImGuiColors.ParsedGreen, isActive)) {
                        if (ImGui.Checkbox("##enable", ref title.Enabled)) {
                            modified = true;
                        }
                    }
                }

                if (ImGui.IsItemHovered()) {
                    ImGui.BeginTooltip();
                    if (isDuplicateEnabled) ImGui.TextColored(ImGuiColors.DalamudRed, "This title will not be used as another title with the same condition precedes it.");
                    if (isActive) ImGui.TextColored(ImGuiColors.ParsedGreen, "This is the current active title.");
                    
                    ImGui.Text("Toggle this title");
                    ImGui.Spacing();
                    ImGui.TextDisabled("Right click to copy toggle command.");
                    ImGui.EndTooltip();
                }
                if (ImGui.IsItemClicked(ImGuiMouseButton.Right)) {
                    ImGui.SetClipboardText($"/honorific title toggle {title.GetUniqueId(characterConfig)}");
                }
                
                DrawTitleCommon(title, ref modified);

                ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X < 250 * ImGuiHelpers.GlobalScale ? ImGui.GetContentRegionAvail().X : 150 * ImGuiHelpers.GlobalScale);
                if (ImGui.BeginCombo("##conditionType", title.TitleCondition.GetAttribute<DescriptionAttribute>()?.Description ?? $"{title.TitleCondition}")) {
                    foreach (var v in Enum.GetValues<TitleConditionType>()) {
                        if (ImGui.Selectable(v.GetAttribute<DescriptionAttribute>()?.Description ?? $"{v}", v == title.TitleCondition)) {
                            if (title.TitleCondition != v) {
                                title.TitleCondition = v;
                                title.ConditionParam0 = 0;
                                modified = true;
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
                            if (ImGui.GetContentRegionAvail().X < 90 * ImGuiHelpers.GlobalScale) ImGui.NewLine();
                            ImGui.SetNextItemWidth(-1);
                            var selected = sheet.GetRow((uint)title.ConditionParam0);
                            if (ImGui.BeginCombo("##conditionClassJob", title.ConditionParam0 == 0 ? "Select..." : selected?.Abbreviation.RawString ?? $"Unknown({title.ConditionParam0}")) {
                                foreach (var cj in sheet) {
                                    if (cj.RowId == 0) continue;
                                    if (ImGui.Selectable(cj.Abbreviation.RawString, title.ConditionParam0 == cj.RowId)) {
                                        title.ConditionParam0 = (int)cj.RowId;
                                        modified = true;
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
                        if (ImGui.GetContentRegionAvail().X < 90 * ImGuiHelpers.GlobalScale) ImGui.NewLine();
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
                    case TitleConditionType.GearSet: {
                        unsafe {
                            var gearSetModule = RaptureGearsetModule.Instance();

                            var currentGearSetName = string.Empty;
                            var currentGearSet = gearSetModule->GetGearset(title.ConditionParam0);
                            if (currentGearSet != null && currentGearSet->Flags.HasFlag(RaptureGearsetModule.GearsetFlag.Exists)) currentGearSetName = currentGearSet->NameString;

                            if (string.IsNullOrWhiteSpace(currentGearSetName)) currentGearSetName = $"Gear Set #{title.ConditionParam0+1:00}";

                            ImGui.SameLine();
                            if (ImGui.GetContentRegionAvail().X < 90 * ImGuiHelpers.GlobalScale) ImGui.NewLine();
                            ImGui.SetNextItemWidth(-1);
                            if (ImGui.BeginCombo("##conditionGearset", $"[{title.ConditionParam0+1}] {currentGearSetName}")) {
                                for (var gearSetIndex = 0; gearSetIndex < 100; gearSetIndex++) {
                                    var name = string.Empty;
                                    var gearSet = gearSetModule->GetGearset(gearSetIndex);
                                    if (gearSet != null && gearSet->Flags.HasFlag(RaptureGearsetModule.GearsetFlag.Exists)) name = gearSet->NameString;
                                    if (string.IsNullOrWhiteSpace(name)) name = $"Gear Set #{gearSetIndex+1:00}";

                                    if (ImGui.Selectable($"[{gearSetIndex+1:00}] {name}", gearSetIndex == title.ConditionParam0)) {
                                        title.ConditionParam0 = gearSetIndex;
                                    }
                                }
                                
                                ImGui.EndCombo();
                            }
                        }
                        break;
                    }
                    case TitleConditionType.Title: {
                        var titleSheet = PluginService.Data.GetExcelSheet<Title>();
                        if (titleSheet == null) break;
                        
                        string GetDisplayTitle(Title? t) {
                            if (t == null || t.RowId == 0) return "No Title";
                            var masc = t.Masculine?.RawString ?? "No Title";
                            var fem = t.Feminine?.RawString ?? "No Title";
                            
                            var display = string.Equals(masc, fem, StringComparison.InvariantCultureIgnoreCase) ? masc : $"{masc} / {fem}";
                            return display;
                        }
                        
                        
                        var currentSetTitle = titleSheet.GetRow((uint)title.ConditionParam0);
                        var titleDisplay = GetDisplayTitle(currentSetTitle);
                        ImGui.SameLine();
                        if (ImGui.GetContentRegionAvail().X < 180 * ImGuiHelpers.GlobalScale) ImGui.NewLine();
                        
                        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X - 45 * ImGuiHelpers.GlobalScale);
                        if (ImGui.BeginCombo("##conditionGearset", $"{titleDisplay}", ImGuiComboFlags.HeightLargest)) {

                            if (ImGui.IsWindowAppearing()) {
                                gameTitleSearch = string.Empty;
                                ImGui.SetKeyboardFocusHere();
                            }
                            
                            ImGui.SetNextItemWidth(-1);
                            ImGui.InputTextWithHint("##gameTitleSearch", "Search...", ref gameTitleSearch, 50);
                            
                            if (ImGui.BeginChild("gameTitleScroll", new Vector2(ImGui.GetItemRectSize().X, 250 * ImGuiHelpers.GlobalScale))) {
                                foreach (var (row, display) in titleSheet.Select(t => ((int)t.RowId, GetDisplayTitle(t))).Where(t => string.IsNullOrWhiteSpace(gameTitleSearch) || t.Item2.Contains(gameTitleSearch, StringComparison.InvariantCultureIgnoreCase)).OrderBy(t => t.Item1 == 0 ? 0 : 1).ThenBy(t => t.Item2, StringComparer.OrdinalIgnoreCase)) {
                                    if (string.IsNullOrWhiteSpace(display)) continue;
                                    using (ImRaii.PushColor(ImGuiCol.Text, ImGui.GetColorU32(ImGuiCol.TextDisabled), row == 0)) {
                                        if (ImGui.Selectable( $"{display}##title_{row}", row == title.ConditionParam0)) {
                                            title.ConditionParam0 = row;
                                            ImGui.CloseCurrentPopup();
                                        }
                                    }
                                    
                                    if (ImGui.IsWindowAppearing() && row == title.ConditionParam0) ImGui.SetScrollHereY(0.5f);
                                } 
                            }
                            ImGui.EndChild();
                            ImGui.EndCombo();
                        }
                        
                        ImGui.SameLine();
                        using (ImRaii.Disabled(activeCharacter == null)) {
                            using (ImRaii.PushFont(UiBuilder.IconFont)) {
                                if (ImGui.Button($"{(char)FontAwesomeIcon.PersonBurst}", new Vector2(ImGui.GetContentRegionAvail().X, ImGui.GetItemRectSize().Y)) && activeCharacter is IPlayerCharacter activePlayerCharacter) {
                                    unsafe {
                                        var c = (FFXIVClientStructs.FFXIV.Client.Game.Character.Character*)activePlayerCharacter.Address;
                                        title.ConditionParam0 = c->CharacterData.TitleId;
                                    }
                                }
                            }

                            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled)) {
                                ImGui.BeginTooltip();
                                ImGui.Text("Set to current title");

                                if (activeCharacter is IPlayerCharacter activePlayerCharacter) {
                                    unsafe {
                                        var c = (FFXIVClientStructs.FFXIV.Client.Game.Character.Character*)activePlayerCharacter.Address;

                                        var activeTitle = titleSheet.GetRow(c->CharacterData.TitleId);
                                        
                                        ImGui.Text($"\t{GetDisplayTitle(activeTitle)}\t");
                                    }
                                    
                                } else {
                                    ImGui.TextDisabled("Player not visible");
                                }
                                
                                ImGui.EndTooltip();
                            }
                        }
                            
                        break;
                    }
                }

                ImGui.PopID();
            }

            if (deleteIndex >= 0) {
                characterConfig.CustomTitles.RemoveAt(deleteIndex);
                modified = true;
            } else if (moveUp > 0) {
                var move = characterConfig.CustomTitles[moveUp];
                characterConfig.CustomTitles.RemoveAt(moveUp);
                characterConfig.CustomTitles.Insert(moveUp - 1, move);
                modified = true;
            }
            
            ImGui.PushID($"title_default");
            // Default Title
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, Vector2.One);
            
            ImGui.PushFont(UiBuilder.IconFont);
            if (ImGui.Button($"{(char)FontAwesomeIcon.Plus}##add", new Vector2(checkboxSize))) {
                characterConfig.CustomTitles.Add(new CustomTitle());
                modified = true;
            }
            
            ImGui.PopFont();
            
            ImGui.SameLine();
            ImGui.Dummy(new Vector2(checkboxSize));
            ImGui.SameLine();
            ImGui.Dummy(new Vector2(checkboxSize));
            ImGui.SameLine();
            ImGui.PopStyleVar();
            modified |= ImGui.Checkbox("##enable", ref characterConfig.DefaultTitle.Enabled);
            
            checkboxSize = ImGui.GetItemRectSize().X;
            
            
            if (ImGui.IsItemHovered()) {
                ImGui.BeginTooltip();
                ImGui.Text("Toggle this title");
                ImGui.Spacing();
                ImGui.TextDisabled("Right click to copy toggle command.");
                ImGui.EndTooltip();
            }
            if (ImGui.IsItemClicked(ImGuiMouseButton.Right)) {
                ImGui.SetClipboardText($"/honorific title toggle {characterConfig.DefaultTitle.UniqueId}");
            }
            
            DrawTitleCommon(characterConfig.DefaultTitle, ref modified);
            
            ImGui.TextDisabled("Default Title");
            
            ImGui.PopID();
            
            ImGui.EndTable();
        }

        
        
        // For some reason separators are broken here... I'll just draw my own
        ImGui.GetWindowDrawList().AddLine(ImGui.GetCursorScreenPos(), ImGui.GetCursorScreenPos() + ImGui.GetContentRegionAvail() * Vector2.UnitX, ImGui.GetColorU32(ImGuiCol.Separator));
        ImGui.Spacing();
        
        ImGui.Checkbox("Use Random Titles", ref characterConfig.UseRandom);
        ImGui.SameLine();

        
        using (ImRaii.PushFont(UiBuilder.IconFont)) {
            ImGui.Text($"{(char)FontAwesomeIcon.InfoCircle}");
        }

        if (ImGui.IsItemHovered()) {
            ImGui.BeginTooltip();
            ImGui.Text("Using random titles allows your title to be picked from all titles that currently meet their conditions.");
            
            ImGui.Text("The selected random title will be locked in until its condition is no longer met, allowing another title to be picked.");
            using (ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, Vector2.Zero)) {
                ImGui.Text("Using the '");
                ImGui.SameLine();
                ImGui.TextColored(ImGuiColors.DalamudViolet, "Next Random");
                ImGui.SameLine();
                ImGui.Text("' button or command '");
                ImGui.SameLine();
                ImGui.TextColored(ImGuiColors.DalamudViolet, "/honorific random");
                ImGui.SameLine();
                ImGui.Text("' will force it to pick a new random title.");
            }
           
            ImGui.TextDisabled("Note: The default title will never be included in the random title selection.");
            ImGui.EndTooltip();
        }
        
        if (characterConfig is { UseRandom: true, ActiveTitle: not null }) {
            ImGui.SameLine();
            if (ImGui.Button("Next Random")) characterConfig.ActiveTitle = null;
        }

        if (characterConfig.UseRandom) {
            using (ImRaii.PushIndent()) {
                ImGui.Checkbox("Select a new random title on zone change", ref characterConfig.RandomOnZoneChange);
            }
        }
    }

    private Vector3 editingColour = Vector3.One;
    private bool DrawColorPicker(string label, ref Vector3? color) {
        var modified = false;
        bool comboOpen;
        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
        if (color == null) {
            ImGui.PushStyleColor(ImGuiCol.FrameBg, 0xFFFFFFFF);
            ImGui.PushStyleColor(ImGuiCol.FrameBgActive, 0xFFFFFFFF);
            ImGui.PushStyleColor(ImGuiCol.FrameBgHovered, 0xFFFFFFFF);
            var p = ImGui.GetCursorScreenPos();
            var dl = ImGui.GetWindowDrawList();
            comboOpen = ImGui.BeginCombo($"##color_{ImGui.GetID(label)}", " ", ImGuiComboFlags.HeightLargest);
            dl.AddLine(p, p + new Vector2(checkboxSize), 0xFF0000FF, 3f * ImGuiHelpers.GlobalScale);
            ImGui.PopStyleColor(3);
        } else {
            ImGui.PushStyleColor(ImGuiCol.FrameBg, new Vector4(color.Value, 1));
            ImGui.PushStyleColor(ImGuiCol.FrameBgActive, new Vector4(color.Value, 1));
            ImGui.PushStyleColor(ImGuiCol.FrameBgHovered, new Vector4(color.Value, 1));
            comboOpen = ImGui.BeginCombo(label, "  ", ImGuiComboFlags.HeightLargest);
            ImGui.PopStyleColor(3);
        }
        
        if (comboOpen) {
            if (ImGui.IsWindowAppearing()) {
                editingColour = color ?? Vector3.One;
            }
            if (ImGui.ColorButton($"##ColorPick_clear", Vector4.One, ImGuiColorEditFlags.NoTooltip)) {
                color = null;
                modified = true;
                ImGui.CloseCurrentPopup();
            }

            if (ImGui.IsItemHovered()) {
                ImGui.SetTooltip("Clear selected colour");
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }
            var dl = ImGui.GetWindowDrawList();
            dl.AddLine(ImGui.GetItemRectMin(), ImGui.GetItemRectMax(), 0xFF0000FF, 3f * ImGuiHelpers.GlobalScale);

            if (color != null) {
                ImGui.SameLine();
                if (ImGui.ColorButton($"##ColorPick_old", new Vector4(color.Value, 1), ImGuiColorEditFlags.NoTooltip)) {
                    ImGui.CloseCurrentPopup();
                }
                if (ImGui.IsItemHovered()) {
                    ImGui.SetTooltip("Revert to previous selection");
                    ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                }
            }
            
            ImGui.SameLine();
            
            if (ImGui.ColorButton("Confirm", new Vector4(editingColour, 1), ImGuiColorEditFlags.NoTooltip, new Vector2(ImGui.GetContentRegionAvail().X, ImGui.GetItemRectSize().Y))) {
                color = editingColour;
                modified = true;
                ImGui.CloseCurrentPopup();
            }
            var size = ImGui.GetItemRectSize();

            if (ImGui.IsItemHovered()) {
                dl.AddRectFilled(ImGui.GetItemRectMin(), ImGui.GetItemRectMax(), 0x33333333);
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }
            
            var textSize = ImGui.CalcTextSize("Confirm");
            dl.AddText(ImGui.GetItemRectMin() + size / 2 - textSize / 2, ImGui.ColorConvertFloat4ToU32(new Vector4(editingColour, 1)) ^ 0x00FFFFFF, "Confirm");
            ImGui.ColorPicker3($"##ColorPick", ref editingColour, ImGuiColorEditFlags.NoSidePreview | ImGuiColorEditFlags.NoSmallPreview);

            ImGui.EndCombo();
        }

        return modified;
    }
    
    
    private void DrawTitleCommon(CustomTitle title, ref bool modified) {
        ImGui.TableNextColumn();
        ImGui.SetNextItemWidth(-1);
        modified |= ImGui.InputText($"##title", ref title.Title, Plugin.MaxTitleLength);
        ImGui.TableNextColumn();
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + ImGui.GetContentRegionAvail().X / 2 - checkboxSize / 2);
        modified |= ImGui.Checkbox($"##prefix", ref title.IsPrefix);
        checkboxSize = ImGui.GetItemRectSize().X;
        if (config.ShowColoredTitles) {
            ImGui.TableNextColumn();
            modified |= DrawColorPicker("##colour", ref title.Color);
            ImGui.TableNextColumn();
            modified |= DrawColorPicker("##glow", ref title.Glow); 
        }

        ImGui.TableNextColumn();
    }

    public override void OnClose() {
        PluginService.PluginInterface.SavePluginConfig(config);
        base.OnClose();
    }
}

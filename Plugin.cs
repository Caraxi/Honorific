using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Threading;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.Command;
using Dalamud.Game.Gui.NamePlate;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Hooking;
using Dalamud.Interface.Windowing;
using Dalamud.Memory;
using Dalamud.Plugin;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Excel.Sheets;
using Lumina.Extensions;
using BattleChara = FFXIVClientStructs.FFXIV.Client.Game.Character.BattleChara;
using ObjectKind = FFXIVClientStructs.FFXIV.Client.Game.Object.ObjectKind;
using ValueType = FFXIVClientStructs.FFXIV.Component.GUI.ValueType;

namespace Honorific;

public unsafe class Plugin : IDalamudPlugin {
    public const byte MaxTitleLength = 32;
    public const string Name = "Honorific";
    
    public PluginConfig Config { get; }
    
    [Signature("40 53 55 57 41 56 48 81 EC ?? ?? ?? ?? 48 8B 84 24", DetourName = nameof(UpdateNameplateDetour))]
    private Hook<UpdateNameplateDelegate>? updateNameplateHook;    
    
    [Signature("48 89 5C 24 ?? 48 89 6C 24 ?? 48 89 74 24 ?? 4C 89 44 24 ?? 57 41 54 41 55 41 56 41 57 48 83 EC 20 48 8B 7C 24", DetourName = nameof(UpdateNameplateNpcDetour))]
    private Hook<UpdateNameplateNpcDelegate>? updateNameplateHookNpc;

    private delegate void* UpdateNameplateDelegate(RaptureAtkModule* raptureAtkModule, RaptureAtkModule.NamePlateInfo* namePlateInfo, NumberArrayData* numArray, StringArrayData* stringArray, BattleChara* battleChara, int numArrayIndex, int stringArrayIndex);
    private delegate void* UpdateNameplateNpcDelegate(RaptureAtkModule* raptureAtkModule, RaptureAtkModule.NamePlateInfo* namePlateInfo, NumberArrayData* numArray, StringArrayData* stringArray, GameObject* gameObject, int numArrayIndex, int stringArrayIndex);
    
    private readonly ConfigWindow configWindow;
    private readonly WindowSystem windowSystem;
    
    private readonly Stopwatch runTime = Stopwatch.StartNew();
    internal static bool IsDebug;

    public Dictionary<ulong, uint> ModifiedNamePlates = new();
    private readonly CancellationTokenSource pluginLifespan;

    public Plugin(IDalamudPluginInterface pluginInterface) {
        pluginLifespan = new CancellationTokenSource();
        pluginInterface.Create<PluginService>();
        
        Config = pluginInterface.GetPluginConfig() as PluginConfig ?? new PluginConfig();

        foreach (var (_, worlds) in Config.WorldCharacterDictionary) {
            foreach (var (_, character) in worlds) {
                foreach (var t in character.CustomTitles) {
                    t.UpdateWarning();
                }
            }
        }

        windowSystem = new WindowSystem(Assembly.GetExecutingAssembly().FullName);
        configWindow = new ConfigWindow($"{Name} | Config", this, Config) {
            #if DEBUG
            IsOpen = Config.DebugOpenOnStatup
            #endif
        };
        windowSystem.AddWindow(configWindow);
        
        PluginService.HookProvider.InitializeFromAttributes(this);
        PluginService.AddonLifecycle.RegisterListener(AddonEvent.PostRefresh, "CharacterInspect", RefreshCharacterInspect);
        updateNameplateHook?.Enable();
        updateNameplateHookNpc?.Enable();

        pluginInterface.UiBuilder.Draw += windowSystem.Draw;
        pluginInterface.UiBuilder.OpenConfigUi += () => OnCommand(string.Empty, string.Empty);

        PluginService.Commands.AddHandler("/honorific", new CommandInfo(OnCommand) {
            HelpMessage = $"Open the {Name} config window.",
            ShowInHelp = true
        });
        IpcProvider.Init(this);
        IpcProvider.NotifyReady();
        PluginService.Framework.RunOnTick(DoIpcCleanup, delay: TimeSpan.FromSeconds(5), cancellationToken: pluginLifespan.Token);
        PluginService.ClientState.TerritoryChanged += OnTerritoryChanged;
        PluginService.NamePlateGui.OnPostNamePlateUpdate += UpdateDisplayedPlateList;
        PluginService.AddonLifecycle.RegisterListener(AddonEvent.PreRequestedUpdate, "NamePlate", NameplateRequestedUpdate);
        #if DEBUG
        IsDebug = true;
        #endif
    }
    
    private void NameplateRequestedUpdate(AddonEvent type, AddonArgs args) {
        if (!Config.EnableAntiFlashing) return; 
        var addon = (AtkUnitBase*) args.Addon.Address;
        var mode = &AtkStage.Instance()->GetNumberArrayData()[5]->IntArray[3];
        if (*mode == 1) return;
        *mode = 1;
        foreach (var n in addon->UldManager.Nodes) {
            if (n.Value == null) continue;
            if (n.Value->Type != (NodeType) 1001) continue;
            if (!n.Value->IsVisible()) continue;
            var componentNode = (AtkComponentNode*)n.Value;
            var component = componentNode->Component;
            var textNode = component->GetTextNodeById(3);
            if (textNode == null || textNode->IsVisible() == false) continue;
            textNode->TextFlags = TextFlags.Edge | TextFlags.Glare | TextFlags.MultiLine | (TextFlags)0x1000 | (TextFlags)0x4000 | (TextFlags)0x8000;
        }
    }

    public static Dictionary<ulong, (SeString Title, bool Visible, Stopwatch Updated)> DisplayedTitles { get; } = new();
    private void UpdateDisplayedPlateList(INamePlateUpdateContext context, IReadOnlyList<INamePlateUpdateHandler> handlers) {
        using var _ = PerformanceMonitors.Run(nameof(UpdateDisplayedPlateList));
        foreach (var namePlateUpdateHandler in handlers) {
            DisplayedTitles[namePlateUpdateHandler.GameObjectId] = (namePlateUpdateHandler.Title, namePlateUpdateHandler.DisplayTitle, Stopwatch.StartNew());
        }
    }

    private void OnTerritoryChanged(ushort _) {
        DisplayedTitles.Clear();
        foreach (var (_, characters) in Config.WorldCharacterDictionary) {
            foreach (var (_, character) in characters) {
                if (character is { UseRandom: true, RandomOnZoneChange: true }) {
                    character.ActiveTitle = null;
                }
            }
        }
    }
    
    private void RefreshCharacterInspect(AddonEvent type, AddonArgs args) {
        if (!Config.ApplyToInspect) return;

        var atkUnitBase = (AtkUnitBase*)args.Addon.Address;
        
        
        SeString? GetString(int index) {
            var atkValues = new ReadOnlySpan<AtkValue>(atkUnitBase->AtkValues, atkUnitBase->AtkValuesCount);
            if (atkValues.Length <= index) return null;
            if (atkValues[index].Type is not (ValueType.String or ValueType.String8 or ValueType.ManagedString)) return null;
            return MemoryHelper.ReadSeStringNullTerminated(new nint(atkValues[index].String));
        }


        var name = GetString(3);
        if (name == null || string.IsNullOrWhiteSpace(name.TextValue)) return;

        var server = GetString(101);
        if (server == null || string.IsNullOrWhiteSpace(server.TextValue)) return;

        if (!PluginService.Data.GetExcelSheet<World>().TryGetFirst(w => w.IsPublic && w.Name.ExtractText() == server.TextValue, out var world)) return;

        var obj = PluginService.Objects.FirstOrDefault(c => c is IPlayerCharacter pc && c.Name.TextValue == name.TextValue && pc.HomeWorld.RowId == world.RowId);
        if (obj is not IPlayerCharacter playerCharacter) return;

        if (!TryGetTitle(playerCharacter, out var title) || title == null) return;
        var nameNode = atkUnitBase->GetTextNodeById(title.IsPrefix ? 12U : 11U);
        var titleNode = atkUnitBase->GetTextNodeById(title.IsPrefix ? 11U : 12U);
        if (nameNode == null || titleNode == null) return;
        nameNode->SetText(name.Encode());
        titleNode->SetText(title.ToSeString(false, Config.ShowColoredTitles, Config.EnableAnimation).Encode());
    }

    private void OnCommand(string command, string args) {
        if (args == "enableDebug") {
            IsDebug = true;
            return;
        }

        var splitArgs = args.Trim().Split(' ', 3, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        void HelpForceSet() {
            PluginService.Chat.Print(new SeStringBuilder().AddText("/honorific force set ").AddUiForeground("<title>", 35).AddText(" | ").AddUiForeground("[prefix/suffix]", 52).AddText(" | ").AddUiForeground("#<HexColor>", 52).AddText(" | ").AddUiForeground("#<HexGlow>", 52).Build(), Name);
        }
        void HelpForceClear() {
            PluginService.Chat.Print(new SeStringBuilder().AddText("/honorific force clear").Build(), Name);
        }
        void HelpToggle() {
            PluginService.Chat.Print(new SeStringBuilder().AddText("/honorific title ").AddUiForeground("<enable|disable|toggle>", 52).AddText(" ").AddUiForeground("<title>", 35).Build(), Name);
        }

        void HelpIdentitySet() {
            PluginService.Chat.Print(new SeStringBuilder().AddText("/honorific identity set ").AddUiForeground("<name>", 35).AddText(" | ").AddUiForeground("[server]", 52).Build(), Name);
        }
        void HelpIdentity() {
            HelpIdentitySet();
            PluginService.Chat.Print(new SeStringBuilder().AddText("/honorific identity reset").Build(), Name);
        }
        
        if (splitArgs.Length > 0) {
            switch (splitArgs[0]) {
                case "title": {
                    var character = PluginService.ClientState.LocalPlayer;
                    if (character == null) {
                        PluginService.Chat.PrintError($"Unable to use command. Character not found.", Name);
                        return;
                    }

                    var characterName = character.Name.TextValue;
                    var homeWorld = character.HomeWorld.RowId;
                    if (Config.IdentifyAs.TryGetValue(PluginService.ClientState.LocalContentId, out var identifyAs)) {
                        (characterName, homeWorld) = identifyAs;
                    }

                    if (!Config.TryGetCharacterConfig(characterName, homeWorld, out var characterConfig) || characterConfig == null) {
                        PluginService.Chat.PrintError($"Unable to use command. This character has not been configured.", Name);
                        return;
                    }
                    
                    if (splitArgs.Length != 3) {
                        HelpToggle();
                        return;
                    }
                    
                    var titleText = splitArgs[2];
                    var titles = characterConfig.GetTitlesBySearchString(titleText);
                    if (titles.Count == 0) {
                        PluginService.Chat.PrintError($"'{titleText}' is not setup on this character.", Name);
                        return;
                    }
                    
                    PluginService.Chat.Print($"{splitArgs[1].ToLower()} {titles.Count} titles.");
                    List<CustomTitle> disabled = [];
                    List<CustomTitle> enabled = [];
                    foreach (var title in titles) {
                        switch (splitArgs[1].ToLower().Trim('<', '>', '[', ']')) {
                            case "toggle" or "t" when !title.Enabled:
                            case "enable" or "e" or "on": {
                                if (!title.Enabled) {
                                    title.Enabled = true;
                                    enabled.Add(title);
                                    if (titles.Count == 1) PluginService.Chat.Print(new SeStringBuilder().Append(title.ToSeString(animate: false)).AddText(" has been enabled.").Build(), Name);
                                }

                                break;
                            }
                            case "toggle" or "t" when title.Enabled:
                            case "disable" or "d" or "off": {
                                if (title.Enabled) {
                                    title.Enabled = false;
                                    disabled.Add(title);
                                    if (titles.Count == 1) PluginService.Chat.Print(new SeStringBuilder().Append(title.ToSeString(animate: false)).AddText(" has been disabled.").Build(), Name);
                                }
                                break;
                            }
                            default: {
                                PluginService.Chat.PrintError($"'{splitArgs[1]}' is not a valid action.", Name);
                                HelpToggle();
                                break;
                            }
                        }
                    }

                    if (titles.Count > 1 && enabled.Count > 0) {
                        var message = new SeStringBuilder();
                        message.AddText("Enabled Titles: ");
                        foreach (var t in enabled) {
                            message.Append(t.ToSeString(animate: false));
                        }

                        PluginService.Chat.Print(message.Build(), Name);
                    }
                    
                    if (titles.Count > 1 && disabled.Count > 0) {
                        var message = new SeStringBuilder();
                        message.AddText("Disabled Titles: ");
                        foreach (var t in disabled) {
                            message.Append(t.ToSeString(animate: false));
                        }

                        PluginService.Chat.Print(message.Build(), Name);
                    }
                    
                    return;
                }
                case "random": {
                    var character = PluginService.ClientState.LocalPlayer;
                    if (character == null) {
                        PluginService.Chat.PrintError($"Unable to use command. Character not found.", Name);
                        return;
                    }
                    
                    var characterName = character.Name.TextValue;
                    var homeWorld = character.HomeWorld.RowId;
                    if (Config.IdentifyAs.TryGetValue(PluginService.ClientState.LocalContentId, out var identifyAs)) {
                        (characterName, homeWorld) = identifyAs;
                    }
                    
                    if (!Config.TryGetCharacterConfig(characterName, homeWorld, out var characterConfig) || characterConfig == null) {
                        PluginService.Chat.PrintError($"Unable to use command. This character has not been configured.", Name);
                        return;
                    }

                    if (!characterConfig.UseRandom) {
                        PluginService.Chat.PrintError("This character is not configured to use random titles.", Name);
                        return;
                    }

                    characterConfig.ActiveTitle = null;
                    return;
                }
                case "force" when splitArgs.Length > 1 && splitArgs[1].ToLower() is "clear": {
                    var character = PluginService.ClientState.LocalPlayer;
                    if (character == null) {
                        PluginService.Chat.PrintError($"Unable to use set command. Character not found.", Name);
                        return;
                    }
                    
                    var characterName = character.Name.TextValue;
                    var homeWorld = character.HomeWorld.RowId;
                    if (Config.IdentifyAs.TryGetValue(PluginService.ClientState.LocalContentId, out var identifyAs)) {
                        (characterName, homeWorld) = identifyAs;
                    }

                    if (!Config.TryGetOrAddCharacter(characterName, homeWorld, out var characterConfig) || characterConfig == null) {
                        PluginService.Chat.PrintError($"Unable to use set command. Config failure.", Name);
                        return;
                    }

                    characterConfig.Override.Enabled = false;
                    characterConfig.Override.Title = string.Empty;
                    return;
                }
                
                case "force" when splitArgs.Length > 1 && splitArgs[1].ToLower() is "set": {
                    var character = PluginService.ClientState.LocalPlayer;
                    if (character == null) {
                        PluginService.Chat.PrintError($"Unable to use set command. Character not found.", Name);
                        return;
                    }
                    
                    var characterName = character.Name.TextValue;
                    var homeWorld = character.HomeWorld.RowId;
                    if (Config.IdentifyAs.TryGetValue(PluginService.ClientState.LocalContentId, out var identifyAs)) {
                        (characterName, homeWorld) = identifyAs;
                    }

                    if (!Config.TryGetOrAddCharacter(characterName, homeWorld, out var characterConfig) || characterConfig == null) {
                        PluginService.Chat.PrintError($"Unable to use set command. Config failure.", Name);
                        return;
                    }
                    
                    if (splitArgs.Length != 3) {
                        HelpForceSet();
                        return;
                    }

                    var setArgs = splitArgs[2].Split('|', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

                    if (setArgs.Length == 0) {
                        HelpForceSet();
                        return;
                    }
                    
                    string? titleText = null;
                    bool? prefix = null;
                    Vector3? color = null;
                    Vector3? glow = null;
                    var silent = false;
                    
                    foreach (var a in setArgs) {
                        if (titleText == null) {
                            titleText = a;

                            if (titleText.Length > MaxTitleLength) {
                                PluginService.Chat.PrintError($"Title is too long: '{a}'. (Max {MaxTitleLength})", Name);
                                return;
                            }
                            
                            continue;
                        }

                        var arg = a.ToLower();

                        var colorArg = arg.Skip(arg.StartsWith('#') ? 1 : 0).ToArray();
                        
                        if (colorArg.Length == 6 && colorArg.All(chr => chr is >= '0' and <= '9' or >= 'a' and <= 'f')) {

                            if (color != null && glow != null) {
                                PluginService.Chat.PrintError($"Duplicate Option in Set: '{a}'", Name);
                                HelpForceSet();
                                return;
                            }
                            
                            var rHex = string.Join(null, colorArg.Skip(0).Take(2));
                            var gHex = string.Join(null, colorArg.Skip(2).Take(2));
                            var bHex = string.Join(null, colorArg.Skip(4).Take(2));
                            
                            
                            if (byte.TryParse(rHex, NumberStyles.HexNumber, NumberFormatInfo.InvariantInfo, out var r) && 
                                byte.TryParse(gHex, NumberStyles.HexNumber, NumberFormatInfo.InvariantInfo, out var g) && 
                                byte.TryParse(bHex, NumberStyles.HexNumber, NumberFormatInfo.InvariantInfo,  out var b)) {

                                var c = new Vector3(r / 255f, g / 255f, b / 255f);
                                if (color == null) {
                                    color = c;
                                    continue;
                                }

                                glow = c;
                                continue;
                            }
                        }
                        
                        if (arg is "p" or "prefix" or "pre") {
                            if (prefix != null) {
                                PluginService.Chat.PrintError($"Duplicate Option in Set: '{a}'", Name);
                                HelpForceSet();
                                return;
                            }
                            
                            prefix = true;
                            continue;
                        }

                        if (arg is "s" or "suffix") {
                            if (prefix != null) {
                                PluginService.Chat.PrintError($"Duplicate Option in Set: '{a}'", Name);
                                HelpForceSet();
                                return;
                            }
                            prefix = false;
                            continue;
                        }

                        if (arg is "silent") {
                            silent = true;
                            continue;
                        }
                        
                        PluginService.Chat.PrintError($"Invalid Option in Set: '{a}'", Name);
                        HelpForceSet();
                        return;
                    }
                    
                    characterConfig.Override.Title = titleText;
                    characterConfig.Override.Color = color;
                    characterConfig.Override.Glow = glow;
                    characterConfig.Override.IsPrefix = prefix ?? false;
                    characterConfig.Override.Enabled = true;

                    if (!silent) {
                        PluginService.Chat.Print(new SeStringBuilder().AddText($"Set {character.Name.TextValue}'s title to ").Append(characterConfig.Override.ToSeString(animate: false)).Build());
                    }
                    
                    return;
                }
                case "identity":
                    if (PluginService.ClientState.LocalContentId == 0 || PluginService.ClientState.LocalPlayer == null) return;
                    if (splitArgs.Length < 2) {
                        HelpIdentity();
                        return;
                    }

                    switch (splitArgs[1]) {
                        case "reset":
                            Config.IdentifyAs.Remove(PluginService.ClientState.LocalContentId);
                            PluginService.PluginInterface.SavePluginConfig(Config);
                            return;
                        case "set":

                            if (splitArgs.Length < 3) {
                                HelpIdentitySet();
                                return;
                            }

                            var nameServerSplit = splitArgs[2].Split('|', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

                            var name = nameServerSplit[0];
                            var serverName = nameServerSplit.Length > 1 ? nameServerSplit[1] : string.Empty;
                            var serverId = 0U;
                            if (string.IsNullOrWhiteSpace(serverName)) {
                                serverId = PluginService.ClientState.LocalPlayer.HomeWorld.RowId;
                            } else {
                                if (!uint.TryParse(serverName, out serverId)) {

                                    var worldRow = PluginService.Data.GetExcelSheet<World>().FirstOrNull(w => w.Name.ExtractText().Equals(serverName, StringComparison.InvariantCultureIgnoreCase));
                                    if (worldRow is { } world) {
                                        serverId = world.RowId;
                                    } else {
                                        PluginService.Chat.PrintError($"World not found: '{serverName}'", Name);
                                        return;
                                    }
                                }
                            }

                            if (PluginService.Data.GetExcelSheet<World>().GetRowOrDefault(serverId) == null) {
                                PluginService.Chat.PrintError($"World not found: 'World#{serverId}'", Name);
                                return;
                            }
                            
                            Config.IdentifyAs[PluginService.ClientState.LocalContentId] = (name, serverId);
                            PluginService.PluginInterface.SavePluginConfig(Config);
                            
                            return;
                        default:
                            HelpIdentity();
                            return;
                    }
                case "help":
                    goto ShowHelp;
                default: 
                    PluginService.Chat.PrintError($"Invalid Subcommand: '{args}'", Name);
                    ShowHelp:
                    HelpToggle();
                    HelpForceSet();
                    HelpForceClear();
                    HelpIdentity();
                    return;
            }
        }

        configWindow.IsOpen = !configWindow.IsOpen;
    }
    
    public void* UpdateNameplateDetour(RaptureAtkModule* raptureAtkModule, RaptureAtkModule.NamePlateInfo* namePlateInfo, NumberArrayData* numArray, StringArrayData* stringArray, BattleChara* battleChara, int numArrayIndex, int stringArrayIndex) {
        try {
            CleanupNamePlate(namePlateInfo);
        } catch (Exception ex) {
            PluginService.Log.Error(ex, "Error in Cleanup of BattleChara Nameplate");
        }
        var r = updateNameplateHook!.Original(raptureAtkModule, namePlateInfo, numArray, stringArray, battleChara, numArrayIndex, stringArrayIndex);
        try {
            if (PluginService.ClientState.IsPvPExcludingDen) return r;
            if (PluginService.Condition[ConditionFlag.BetweenAreas] || 
                PluginService.Condition[ConditionFlag.BetweenAreas51] || 
                PluginService.Condition[ConditionFlag.LoggingOut] || 
                PluginService.Condition[ConditionFlag.OccupiedInCutSceneEvent] || 
                PluginService.Condition[ConditionFlag.WatchingCutscene] || 
                PluginService.Condition[ConditionFlag.WatchingCutscene78]) return r;
            
            var gameObject = &battleChara->Character.GameObject;
            if (gameObject->ObjectKind == ObjectKind.Pc && gameObject->SubKind == 4) {
                AfterNameplateUpdate(namePlateInfo, battleChara);
            }
        } catch (Exception ex) {
            PluginService.Log.Error(ex, "Error in AfterNameplateUpdate");
        }

        return r;
    }

    private void CleanupNamePlate(RaptureAtkModule.NamePlateInfo* namePlateInfo, bool force = false) {
        
        if (ModifiedNamePlates.TryGetValue((ulong)namePlateInfo, out var owner) && (force || owner != namePlateInfo->ObjectId.ObjectId)) {
            using var _ = PerformanceMonitors.Run("Cleanup");
            PluginService.Log.Verbose($"Cleanup NamePlate: {MemoryHelper.ReadSeString(&namePlateInfo->Name).TextValue}");
            var title = MemoryHelper.ReadSeString(&namePlateInfo->Title);
            if (title.TextValue.Length > 0) {
                title.Payloads.Insert(0, new TextPayload("《"));
                title.Payloads.Add( new TextPayload("》"));
            }
            namePlateInfo->DisplayTitle.SetString(title.EncodeNullTerminated());
            namePlateInfo->IsDirty = true;
            ModifiedNamePlates.Remove((ulong)namePlateInfo);
        }
    }
    
    public void* UpdateNameplateNpcDetour(RaptureAtkModule* raptureAtkModule, RaptureAtkModule.NamePlateInfo* namePlateInfo, NumberArrayData* numArray, StringArrayData* stringArray, GameObject* gameObject, int numArrayIndex, int stringArrayIndex) {
        try {
            CleanupNamePlate(namePlateInfo, true);
        } catch (Exception ex) {
            PluginService.Log.Error(ex, "Error in Cleanup of NPC Nameplate");
        }
        return updateNameplateHookNpc!.Original(raptureAtkModule, namePlateInfo, numArray, stringArray, gameObject, numArrayIndex, stringArrayIndex);
    }

    public void AfterNameplateUpdate(RaptureAtkModule.NamePlateInfo* namePlateInfo, BattleChara* battleChara) {
        if (namePlateInfo->ObjectId.ObjectId == 0) return;
        using var fp = PerformanceMonitors.Run("AfterNameplateUpdate");
        var gameObject = &battleChara->Character.GameObject;
        if (gameObject->ObjectKind != ObjectKind.Pc || gameObject->SubKind != 4) return;
        var player = PluginService.Objects.CreateObjectReference((nint)gameObject) as IPlayerCharacter;
        if (player == null) return;
        
        var titleChanged = false;

        if (!TryGetTitle(player, out var title) || title == null) {
            title = GetOriginalTitle(player);
        }
        
        var currentDisplayTitle = MemoryHelper.ReadSeString(&namePlateInfo->DisplayTitle);
        var displayTitle = title.ToSeString(true, Config.ShowColoredTitles, animate: Config.EnableAnimation);

        if (!displayTitle.IsSameAs(currentDisplayTitle, out var encoded)) {
            if (encoded == null || encoded.Length == 0) {
                namePlateInfo->DisplayTitle.SetString(string.Empty);
            } else {
                namePlateInfo->DisplayTitle.SetString(encoded);
            }
            titleChanged = true;
        }

        var isPrefix = namePlateInfo->IsPrefixTitle;
        namePlateInfo->IsPrefix = title.IsPrefix;
        if (isPrefix != title.IsPrefix) titleChanged = true;

        if (titleChanged) {
            namePlateInfo->IsDirty = true;
            if (ModifiedNamePlates.ContainsKey((ulong)namePlateInfo)) {
                ModifiedNamePlates[(ulong)namePlateInfo] = namePlateInfo->ObjectId.ObjectId;
            } else {
                ModifiedNamePlates.Add((ulong)namePlateInfo, namePlateInfo->ObjectId.ObjectId); 
            }

            if (battleChara->Character.GameObject.ObjectIndex == 0) {
                IpcProvider.ChangedLocalCharacterTitle(title);
            }
        }
    }
    
    public static Dictionary<uint, CustomTitle> IpcAssignedTitles { get; } = new();
    

    public CustomTitle GetOriginalTitle(IPlayerCharacter playerCharacter) {
        var title = new CustomTitle();
        
        bool hideVanilla;
        if (playerCharacter.ObjectIndex == 0) hideVanilla = Config.HideVanillaSelf;
        else if (playerCharacter.StatusFlags.HasFlag(StatusFlags.PartyMember)) hideVanilla = Config.HideVanillaParty;
        else if (playerCharacter.StatusFlags.HasFlag(StatusFlags.AllianceMember)) hideVanilla = Config.HideVanillaAlliance;
        else if (playerCharacter.StatusFlags.HasFlag(StatusFlags.Friend)) hideVanilla = Config.HideVanillaFriends;
        else hideVanilla = Config.HideVanillaOther;

        if (hideVanilla) {
            title.Title = string.Empty;
            title.IsOriginal = true;
            return title;
        }
        
        var character = (Character*) playerCharacter.Address;
        var titleId = character->CharacterData.TitleId;
        var titleData = PluginService.Data.GetExcelSheet<Title>().GetRowOrDefault(titleId);
        if (titleData == null) return title;
        var genderedTitle = character->GameObject.Sex == 0 ? titleData.Value.Masculine : titleData.Value.Feminine;
        title.Title = genderedTitle.ExtractText();
        title.IsPrefix = titleData.Value.IsPrefix;
        title.IsOriginal = true;
        return title;
    }
    
    public bool TryGetTitle(IPlayerCharacter playerCharacter, out CustomTitle? title, bool allowOriginal = true) {
        using var _ = PerformanceMonitors.Run("TryGetTitle");
        title = null;
        if (isDisposing || runTime.ElapsedMilliseconds < 1000) {
            if (!allowOriginal) return false;
            title = GetOriginalTitle(playerCharacter);
            return true;
        }
        if (IpcAssignedTitles.TryGetValue(playerCharacter.EntityId, out title) && title.IsValid()) return true;


        var playerName = playerCharacter.Name.TextValue;
        var homeWorld = playerCharacter.HomeWorld.RowId;

        if (playerCharacter.ObjectIndex == 0 && Config.IdentifyAs.TryGetValue(PluginService.ClientState.LocalContentId, out var identifyAs)) {
            (playerName, homeWorld) = identifyAs;
        }
        
        if (!Config.TryGetCharacterConfig(playerName, homeWorld, out var characterConfig) || characterConfig == null) {
            if (!allowOriginal) return false;
            title = GetOriginalTitle(playerCharacter);
            return true;
        }
        
        if (characterConfig.Override.Enabled) {
            title = characterConfig.Override;
            return true;
        }


        if (characterConfig.UseRandom) {
            var titles = characterConfig.CustomTitles.Where(t => t.Enabled && t.IsValid() && t.MatchesConditions(playerCharacter)).ToList();

            if (titles.Count > 0) {
                if (characterConfig.ActiveTitle != null && titles.Contains(characterConfig.ActiveTitle)) {
                    title = characterConfig.ActiveTitle;
                    return true;
                }

                var r = new Random().Next(0, titles.Count);
                characterConfig.ActiveTitle = titles[r];
                title = characterConfig.ActiveTitle;
                return true;
            }
            characterConfig.ActiveTitle = null;
        } else {
            characterConfig.ActiveTitle = null;
            title = characterConfig.CustomTitles.FirstOrDefault(t => t.Enabled && t.IsValid() && t.MatchesConditions(playerCharacter));
            if (title != null) return true;
        }
        
        if (characterConfig.DefaultTitle.Enabled) {
            title = characterConfig.DefaultTitle;
            return true;
        }

        if (!allowOriginal) {
            title = null;
            return false;
        }
        
        title = GetOriginalTitle(playerCharacter);
        return true;
    }

    private void DoIpcCleanup() {
        using var _ = PerformanceMonitors.Run("IPC Cleanup");
        PluginService.Framework.RunOnTick(DoIpcCleanup, delay: TimeSpan.FromSeconds(5), cancellationToken: pluginLifespan.Token);
        if (pluginLifespan.IsCancellationRequested) return;
        if (!PluginService.Framework.IsInFrameworkUpdateThread) return;
        
        if (IpcAssignedTitles.Count > 0) {
            PluginService.Log.Verbose("Performing IPC Cleanup");
            
            if (!PluginService.ClientState.IsLoggedIn) {
                IpcAssignedTitles.Clear();
                return;
            }
           
            var objectIds = IpcAssignedTitles.Keys.ToList();
            foreach (var chr in PluginService.Objects) {
                if (chr is IPlayerCharacter pc) {
                    if (objectIds.Remove(pc.EntityId)) {
                        PluginService.Log.Verbose($"Object#{pc.EntityId:X} is still visible. ({chr.Name.TextValue})");
                    }
                }
            }

            foreach (var o in objectIds) {
                PluginService.Log.Debug($"Removing Object#{o:X} from IPC. No longer visible.");
                IpcAssignedTitles.Remove(o);
            }
        }
    }

    private bool isDisposing;

    public void Dispose() {
        pluginLifespan.Cancel();
        IpcProvider.NotifyDisposing();
        PluginService.Log.Verbose($"Dispose");
        isDisposing = true;
        IpcProvider.DeInit();
        updateNameplateHook?.Disable();
        updateNameplateHook?.Dispose();
        updateNameplateHookNpc?.Disable();
        updateNameplateHookNpc?.Dispose();
        updateNameplateHook = null;
        updateNameplateHookNpc = null;

        PluginService.Commands.RemoveHandler("/honorific");
        windowSystem.RemoveAllWindows();

        PluginService.PluginInterface.SavePluginConfig(Config);
    }
}

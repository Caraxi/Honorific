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
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.Command;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Hooking;
using Dalamud.Interface.Windowing;
using Dalamud.Memory;
using Dalamud.Plugin;
using Dalamud.Utility;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Excel;
using Lumina.Excel.GeneratedSheets;
using BattleChara = FFXIVClientStructs.FFXIV.Client.Game.Character.BattleChara;
using ValueType = FFXIVClientStructs.FFXIV.Component.GUI.ValueType;

namespace Honorific;

public unsafe class Plugin : IDalamudPlugin {
    public string Name => "Honorific";
    
    public PluginConfig Config { get; }
    
    [Signature("40 55 56 57 41 56 48 81 EC ?? ?? ?? ?? 48 8B 84 24", DetourName = nameof(UpdateNameplateDetour))]
    private Hook<UpdateNameplateDelegate>? updateNameplateHook;    
    
    [Signature("48 89 5C 24 ?? 48 89 6C 24 ?? 48 89 74 24 ?? 4C 89 44 24 ?? 57 41 54 41 55 41 56 41 57 48 83 EC 20 48 8B 74 24 ??", DetourName = nameof(UpdateNameplateNpcDetour))]
    private Hook<UpdateNameplateNpcDelegate>? updateNameplateHookNpc;

    private delegate void* UpdateNameplateDelegate(RaptureAtkModule* raptureAtkModule, RaptureAtkModule.NamePlateInfo* namePlateInfo, NumberArrayData* numArray, StringArrayData* stringArray, BattleChara* battleChara, int numArrayIndex, int stringArrayIndex);
    private delegate void* UpdateNameplateNpcDelegate(RaptureAtkModule* raptureAtkModule, RaptureAtkModule.NamePlateInfo* namePlateInfo, NumberArrayData* numArray, StringArrayData* stringArray, GameObject* gameObject, int numArrayIndex, int stringArrayIndex);
    
    private readonly ConfigWindow configWindow;
    private readonly WindowSystem windowSystem;

    private readonly ExcelSheet<Title>? titleSheet;
    
    private readonly Stopwatch runTime = Stopwatch.StartNew();
    internal static bool IsDebug;

    public Dictionary<ulong, uint> ModifiedNamePlates = new();
    private readonly CancellationTokenSource pluginLifespan;

    public Plugin(DalamudPluginInterface pluginInterface) {
        pluginLifespan = new CancellationTokenSource();
        pluginInterface.Create<PluginService>();

        titleSheet = PluginService.Data.GetExcelSheet<Title>();
        if (titleSheet == null) throw new Exception("Failed to load ExcelSheet<Title>");
        
        Config = pluginInterface.GetPluginConfig() as PluginConfig ?? new PluginConfig();

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

        #if DEBUG
        IsDebug = true;
        #endif
    }
    
    private void RefreshCharacterInspect(AddonEvent type, AddonArgs args) {
        if (!Config.ApplyToInspect) return;

        var atkUnitBase = (AtkUnitBase*)args.Addon;
        
        
        SeString? GetString(int index) {
            var atkValues = new ReadOnlySpan<AtkValue>(atkUnitBase->AtkValues, atkUnitBase->AtkValuesCount);
            if (atkValues.Length <= index) return null;
            if (atkValues[index].Type is not (ValueType.String or ValueType.String8 or ValueType.AllocatedString)) return null;
            return MemoryHelper.ReadSeStringNullTerminated(new nint(atkValues[index].String));
        }


        var name = GetString(3);
        if (name == null || string.IsNullOrWhiteSpace(name.TextValue)) return;

        var server = GetString(36);
        if (server == null || string.IsNullOrWhiteSpace(server.TextValue)) return;

        var world = PluginService.Data.GetExcelSheet<World>()?.FirstOrDefault(w => w.IsPublic && w.Name.RawString == server.TextValue);
        if (world == null) return;

        var obj = PluginService.Objects.FirstOrDefault(c => c is PlayerCharacter pc && c.Name.TextValue == name.TextValue && pc.HomeWorld.Id == world.RowId);
        if (obj is not PlayerCharacter playerCharacter) return;

        if (!TryGetTitle(playerCharacter, out var title) || title == null) return;
        var nameNode = atkUnitBase->GetTextNodeById(title.IsPrefix ? 7U : 6U);
        var titleNode = atkUnitBase->GetTextNodeById(title.IsPrefix ? 6U : 7U);
        if (nameNode == null || titleNode == null) return;
        nameNode->SetText(name.Encode());
        titleNode->SetText(title.ToSeString(false, Config.ShowColoredTitles).Encode());
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
        
        
        if (splitArgs.Length > 0) {
            switch (splitArgs[0]) {
                case "title": {
                    var character = PluginService.ClientState.LocalPlayer;
                    if (character == null) {
                        PluginService.Chat.PrintError($"Unable to use command. Character not found.", Name);
                        return;
                    }
                    
                    if (!Config.TryGetCharacterConfig(character.Name.TextValue, character.HomeWorld.Id, out var characterConfig) || characterConfig == null) {
                        PluginService.Chat.PrintError($"Unable to use command. This character has not been configured.", Name);
                        return;
                    }
                    
                    if (splitArgs.Length != 3) {
                        HelpToggle();
                        return;
                    }
                    
                    var titleText = splitArgs[2];

                    var title = characterConfig.GetTitleByUniqueId(titleText);
                    if (title == null) title = characterConfig.CustomTitles.FirstOrDefault(t => t.Title?.Equals(titleText, StringComparison.InvariantCultureIgnoreCase) == true);
                    if (title == null && characterConfig.DefaultTitle.Title?.Equals(titleText, StringComparison.InvariantCultureIgnoreCase) == true) {
                        title = characterConfig.DefaultTitle;
                    }

                    if (title == null) {
                        PluginService.Chat.PrintError($"'{titleText}' is not setup on this character.", Name);
                        return;
                    }
                    
                    switch (splitArgs[1].ToLower().Trim('<', '>', '[', ']')) {
                        case "toggle" or "t" when !title.Enabled:
                        case "enable" or "e" or "on": {
                            if (!title.Enabled) {
                                title.Enabled = true;
                                PluginService.Chat.Print(new SeStringBuilder().Append(title.ToSeString()).AddText(" has been enabled.").Build(), Name);
                            }
                            
                            return;
                        }
                        case "toggle" or "t" when title.Enabled:
                        case "disable" or "d" or "off": {
                            if (title.Enabled) {
                                title.Enabled = false;
                                PluginService.Chat.Print(new SeStringBuilder().Append(title.ToSeString()).AddText(" has been disabled.").Build(), Name);
                            }
                            return;
                        }
                        default: {
                            PluginService.Chat.PrintError($"'{splitArgs[1]}' is not a valid action.", Name);
                            HelpToggle();
                            return;
                        }
                    }
                }
                case "random": {
                    var character = PluginService.ClientState.LocalPlayer;
                    if (character == null) {
                        PluginService.Chat.PrintError($"Unable to use command. Character not found.", Name);
                        return;
                    }
                    
                    if (!Config.TryGetCharacterConfig(character.Name.TextValue, character.HomeWorld.Id, out var characterConfig) || characterConfig == null) {
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
                    
                    if (!Config.TryGetOrAddCharacter(character.Name.TextValue, character.HomeWorld.Id, out var characterConfig) || characterConfig == null) {
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
                    
                    if (!Config.TryGetOrAddCharacter(character.Name.TextValue, character.HomeWorld.Id, out var characterConfig) || characterConfig == null) {
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

                            if (titleText.Length > 25) {
                                PluginService.Chat.PrintError($"Title is too long: '{a}'. (Max 25)", Name);
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
                        PluginService.Chat.Print(new SeStringBuilder().AddText($"Set {character.Name.TextValue}'s title to ").Append(characterConfig.Override.ToSeString()).Build());
                    }
                    
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
            if (gameObject->ObjectKind == 1 && gameObject->SubKind == 4) {
                AfterNameplateUpdate(namePlateInfo, battleChara);
            }
        } catch (Exception ex) {
            PluginService.Log.Error(ex, "Error in AfterNameplateUpdate");
        }

        return r;
    }

    private void CleanupNamePlate(RaptureAtkModule.NamePlateInfo* namePlateInfo, bool force = false) {
        
        if (ModifiedNamePlates.TryGetValue((ulong)namePlateInfo, out var owner) && (force || owner != namePlateInfo->ObjectID.ObjectID)) {
            using var _ = PerformanceMonitors.Run("Cleanup");
            PluginService.Log.Verbose($"Cleanup NamePlate: {namePlateInfo->Name.ToSeString().TextValue}");
            var title = namePlateInfo->Title.ToSeString();
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
        if (namePlateInfo->ObjectID.ObjectID == 0) return;
        using var fp = PerformanceMonitors.Run("AfterNameplateUpdate");
        var gameObject = &battleChara->Character.GameObject;
        if (gameObject->ObjectKind != 1 || gameObject->SubKind != 4) return;
        var player = PluginService.Objects.CreateObjectReference((nint)gameObject) as PlayerCharacter;
        if (player == null) return;
        
        var titleChanged = false;

        if (!TryGetTitle(player, out var title) || title == null) {
            title = GetOriginalTitle(player);
        }
        
        var currentDisplayTitle = namePlateInfo->DisplayTitle.ToSeString();
        var displayTitle = title.ToSeString(true, Config.ShowColoredTitles);

        if (!displayTitle.IsSameAs(currentDisplayTitle, out var encoded)) {
            if (encoded == null || encoded.Length == 0) {
                namePlateInfo->DisplayTitle.SetString(string.Empty);
            } else {
                namePlateInfo->DisplayTitle.SetString(encoded);
            }
            titleChanged = true;
        }

        var isPrefix = (namePlateInfo->Flags & 0x1000000) == 0x1000000;
        namePlateInfo->Flags &= ~0x1000000;
        if (title.IsPrefix) namePlateInfo->Flags |= 0x1000000;
        if (isPrefix != title.IsPrefix) titleChanged = true;

        if (titleChanged) {
            namePlateInfo->IsDirty = true;
            if (ModifiedNamePlates.ContainsKey((ulong)namePlateInfo)) {
                ModifiedNamePlates[(ulong)namePlateInfo] = namePlateInfo->ObjectID.ObjectID;
            } else {
                ModifiedNamePlates.Add((ulong)namePlateInfo, namePlateInfo->ObjectID.ObjectID); 
            }

            if (battleChara->Character.GameObject.ObjectIndex == 0) {
                IpcProvider.ChangedLocalCharacterTitle(title);
            }
        }
    }
    
    public static Dictionary<uint, CustomTitle> IpcAssignedTitles { get; } = new();
    

    public CustomTitle GetOriginalTitle(PlayerCharacter playerCharacter) {
        var title = new CustomTitle();
        var character = (Character*) playerCharacter.Address;
        var titleId = character->CharacterData.TitleID;
        var titleData = titleSheet!.GetRow(titleId);
        if (titleData == null) return title;
        var genderedTitle = character->GameObject.Gender == 0 ? titleData.Masculine : titleData.Feminine;
        title.Title = genderedTitle.ToDalamudString().TextValue;
        title.IsPrefix = titleData.IsPrefix;
        title.IsOriginal = true;
        return title;
    }
    
    public bool TryGetTitle(PlayerCharacter playerCharacter, out CustomTitle? title, bool allowOriginal = true) {
        using var _ = PerformanceMonitors.Run("TryGetTitle");
        title = null;
        if (isDisposing || runTime.ElapsedMilliseconds < 1000) {
            if (!allowOriginal) return false;
            title = GetOriginalTitle(playerCharacter);
            return true;
        }
        if (IpcAssignedTitles.TryGetValue(playerCharacter.ObjectId, out title) && title.IsValid()) return true;
        if (!Config.TryGetCharacterConfig(playerCharacter.Name.TextValue, playerCharacter.HomeWorld.Id, out var characterConfig) || characterConfig == null) {
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
                if (chr is PlayerCharacter pc) {
                    if (objectIds.Remove(pc.ObjectId)) {
                        PluginService.Log.Verbose($"Object#{pc.ObjectId:X} is still visible. ({chr.Name.TextValue})");
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

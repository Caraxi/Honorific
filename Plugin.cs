using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.Command;
using Dalamud.Hooking;
using Dalamud.Interface.Windowing;
using Dalamud.Logging;
using Dalamud.Plugin;
using Dalamud.Utility;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Excel;
using Lumina.Excel.GeneratedSheets;
using BattleChara = FFXIVClientStructs.FFXIV.Client.Game.Character.BattleChara;

namespace Honorific;

public unsafe class Plugin : IDalamudPlugin {
    public string Name => "Honorific";
    
    public PluginConfig Config { get; }
    
    [Signature("40 53 55 56 41 56 48 81 EC ?? ?? ?? ?? 48 8B 84 24", DetourName = nameof(UpdateNameplateDetour))]
    private Hook<UpdateNameplateDelegate>? updateNameplateHook;
    private delegate void* UpdateNameplateDelegate(RaptureAtkModule* raptureAtkModule, RaptureAtkModule.NamePlateInfo* namePlateInfo, NumberArrayData* numArray, StringArrayData* stringArray, BattleChara* battleChara, int numArrayIndex, int stringArrayIndex);
    
    private readonly ConfigWindow configWindow;
    private readonly WindowSystem windowSystem;

    private readonly ExcelSheet<Title>? titleSheet;


    private readonly Stopwatch runTime = Stopwatch.StartNew();
    internal static bool IsDebug;
    
    public Plugin(DalamudPluginInterface pluginInterface) {
        pluginInterface.Create<PluginService>();

        titleSheet = PluginService.Data.GetExcelSheet<Title>();
        if (titleSheet == null) throw new Exception("Failed to load ExcelSheet<Title>");
        
        Config = pluginInterface.GetPluginConfig() as PluginConfig ?? new PluginConfig();

        windowSystem = new WindowSystem(Assembly.GetExecutingAssembly().FullName);
        configWindow = new ConfigWindow($"{Name} | Config", Config) {
            #if DEBUG
            IsOpen = true
            #endif
        };
        windowSystem.AddWindow(configWindow);
        
        SignatureHelper.Initialise(this);
        updateNameplateHook?.Enable();

        pluginInterface.UiBuilder.Draw += windowSystem.Draw;
        pluginInterface.UiBuilder.OpenConfigUi += () => OnCommand(string.Empty, string.Empty);

        PluginService.Commands.AddHandler("/honorific", new CommandInfo(OnCommand) {
            HelpMessage = $"Open the {Name} config window.",
            ShowInHelp = true
        });
        IpcProvider.Init(this);
        IpcProvider.NotifyReady();
    }

    private void OnCommand(string command, string args) {
        if (args == "enableDebug") {
            IsDebug = true;
            return;
        }
        configWindow.IsOpen = !configWindow.IsOpen;
    }
    
    public void* UpdateNameplateDetour(RaptureAtkModule* raptureAtkModule, RaptureAtkModule.NamePlateInfo* namePlateInfo, NumberArrayData* numArray, StringArrayData* stringArray, BattleChara* battleChara, int numArrayIndex, int stringArrayIndex) {
        var r = updateNameplateHook!.Original(raptureAtkModule, namePlateInfo, numArray, stringArray, battleChara, numArrayIndex, stringArrayIndex);
        if (PluginService.ClientState.IsPvPExcludingDen) return r;
        if (PluginService.Condition[ConditionFlag.BetweenAreas] || 
            PluginService.Condition[ConditionFlag.BetweenAreas51] || 
            PluginService.Condition[ConditionFlag.LoggingOut] || 
            PluginService.Condition[ConditionFlag.OccupiedInCutSceneEvent] || 
            PluginService.Condition[ConditionFlag.WatchingCutscene] || 
            PluginService.Condition[ConditionFlag.WatchingCutscene78]) return r;

        try {
            var gameObject = &battleChara->Character.GameObject;
            if (gameObject->ObjectKind == 1 && gameObject->SubKind == 4) {
                AfterNameplateUpdate(namePlateInfo, battleChara);
            }
        } catch (Exception ex) {
            PluginLog.Error(ex, "Error in AfterNameplateUpdate");
        }

        return r;
    }

    public void AfterNameplateUpdate(RaptureAtkModule.NamePlateInfo* namePlateInfo, BattleChara* battleChara) {
        var gameObject = &battleChara->Character.GameObject;
              
        if (gameObject->ObjectKind != 1 || gameObject->SubKind != 4) return;
        var player = PluginService.Objects.CreateObjectReference((nint)gameObject) as PlayerCharacter;
        if (player == null) return;

        string titleText;
        bool titlePrefix;
        var titleChanged = false;

        if (!TryGetTitle(player, out var title) || title == null) {
            var assignedTitleId = (short)namePlateInfo->Flags;

            if (assignedTitleId <= 0) {
                titleText = string.Empty;
                titlePrefix = false;
            } else {
                var titleData = titleSheet!.GetRow((uint)assignedTitleId);
                if (titleData == null) {
                    // Uh... ?
                    titleText = string.Empty;
                    titlePrefix = false;
                } else {
                    var genderedTitle = gameObject->Gender == 0 ? titleData.Masculine : titleData.Feminine;
                    titleText = genderedTitle.ToDalamudString().TextValue;
                    titlePrefix = titleData.IsPrefix;
                }
            }
        } else {
            titleText = title.Title ?? string.Empty;
            titlePrefix = title.IsPrefix;
        }
        
        if (namePlateInfo->Title.ToString() != titleText) {
            PluginLog.Verbose($"Change '{player.Name.TextValue}' title: <{titleText}> (was <{namePlateInfo->Title.ToString()}>)");
            namePlateInfo->Title.SetString(titleText);
            titleChanged = true;
        }

        var displayTitle = string.IsNullOrEmpty(titleText) ? string.Empty : $"《{titleText}》";
        if (namePlateInfo->DisplayTitle.ToString() != displayTitle) {
            PluginLog.Verbose($"Change '{player.Name.TextValue}' display title: <{displayTitle}> (was <{namePlateInfo->DisplayTitle.ToString()}>");
            namePlateInfo->DisplayTitle.SetString(displayTitle);
            titleChanged = true;
        }
        
        var isPrefix = (namePlateInfo->Flags & 0x1000000) == 0x1000000;
        namePlateInfo->Flags &= ~0x1000000;
        if (titlePrefix) namePlateInfo->Flags |= 0x1000000;
        if (isPrefix != titlePrefix) titleChanged = true;
        
        if (titleChanged && (nint) battleChara == PluginService.ClientState.LocalPlayer?.Address) {
            IpcProvider.ChangedLocalCharacterTitle(titleText, titlePrefix);
        }
    }
    
    public static Dictionary<(string, uint), CustomTitle> IpcAssignedTitles { get; } = new();

    public CustomTitle GetOriginalTitle(PlayerCharacter playerCharacter) {
        var title = new CustomTitle();
        var character = (Character*) playerCharacter.Address;
        var titleId = (ushort)Marshal.ReadInt16(playerCharacter.Address, 0x1AF8);
        if (titleId == 0) return title;
        var titleData = titleSheet!.GetRow(titleId);
        if (titleData == null) return title;
        var genderedTitle = character->GameObject.Gender == 0 ? titleData.Masculine : titleData.Feminine;
        title.Title = genderedTitle.ToDalamudString().TextValue;
        title.IsPrefix = titleData.IsPrefix;
        return title;
    }
    
    public bool TryGetTitle(PlayerCharacter playerCharacter, out CustomTitle? title) {
        if (isDisposing || runTime.ElapsedMilliseconds < 1000) {
            title = GetOriginalTitle(playerCharacter);
            return true;
        }
        if (IpcAssignedTitles.TryGetValue((playerCharacter.Name.TextValue, playerCharacter.HomeWorld.Id), out title) && title.IsValid()) return true;
        if (!Config.TryGetCharacterConfig(playerCharacter.Name.TextValue, playerCharacter.HomeWorld.Id, out var characterConfig) || characterConfig == null) {
            title = GetOriginalTitle(playerCharacter);
            return true;
        }
        
        foreach (var cTitle in characterConfig.CustomTitles.Where(t => t.Enabled && t.IsValid())) {
            switch (cTitle.TitleCondition) {
                case TitleConditionType.None:
                    title = cTitle;
                    return true;
                case TitleConditionType.ClassJob:
                    if (cTitle.ConditionParam0 == playerCharacter.ClassJob.Id) {
                        title = cTitle;
                        return true;
                    }
                    break;
                case TitleConditionType.JobRole:
                    if (cTitle.ConditionParam0 == 0) continue;
                    if (playerCharacter.ClassJob.GameData?.IsRole((ClassJobRole)cTitle.ConditionParam0) ?? false) {
                        title = cTitle;
                        return true;
                    }
                    break;
            }
        }
        
        if (characterConfig.DefaultTitle.Enabled) {
            title = characterConfig.DefaultTitle;
            return true;
        }
        
        title = GetOriginalTitle(playerCharacter);
        return true;
    }


    private bool isDisposing;

    public void Dispose() {
        IpcProvider.NotifyDisposing();
        PluginLog.Verbose($"Dispose");
        isDisposing = true;
        IpcProvider.DeInit();
        updateNameplateHook?.Disable();
        updateNameplateHook?.Dispose();
        updateNameplateHook = null;

        PluginService.Commands.RemoveHandler("/honorific");
        windowSystem.RemoveAllWindows();
        
        PluginService.PluginInterface.SavePluginConfig(Config);
    }
}

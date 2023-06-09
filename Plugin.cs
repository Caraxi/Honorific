﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.Command;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Hooking;
using Dalamud.Interface.Windowing;
using Dalamud.Logging;
using Dalamud.Plugin;
using Dalamud.Utility;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Excel;
using Lumina.Excel.GeneratedSheets;
using BattleChara = FFXIVClientStructs.FFXIV.Client.Game.Character.BattleChara;

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
    public Plugin(DalamudPluginInterface pluginInterface) {
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
        
        SignatureHelper.Initialise(this);
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

        #if DEBUG
        IsDebug = true;
        #endif

        PluginService.Framework.Update += PerformanceMonitors.LogFramePerformance;
    }

    private void OnCommand(string command, string args) {
        if (args == "enableDebug") {
            IsDebug = true;
            return;
        }
        configWindow.IsOpen = !configWindow.IsOpen;
    }
    
    public void* UpdateNameplateDetour(RaptureAtkModule* raptureAtkModule, RaptureAtkModule.NamePlateInfo* namePlateInfo, NumberArrayData* numArray, StringArrayData* stringArray, BattleChara* battleChara, int numArrayIndex, int stringArrayIndex) {
        try {
            CleanupNamePlate(namePlateInfo);
        } catch (Exception ex) {
            PluginLog.LogError(ex, "Error in Cleanup of BattleChara Nameplate");
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
            PluginLog.Error(ex, "Error in AfterNameplateUpdate");
        }

        return r;
    }

    private void CleanupNamePlate(RaptureAtkModule.NamePlateInfo* namePlateInfo, bool force = false) {
        using var _ = PerformanceMonitors.CleanupProcessing.Start();
        if (ModifiedNamePlates.TryGetValue((ulong)namePlateInfo, out var owner) && (force || owner != namePlateInfo->ObjectID.ObjectID)) {
            PluginLog.Verbose($"Cleanup NamePlate: {namePlateInfo->Name.ToSeString().TextValue}");
            var title = namePlateInfo->Title.ToSeString();
            if (title.TextValue.Length > 0) {
                title.Payloads.Insert(0, new TextPayload("《"));
                title.Payloads.Add( new TextPayload("《"));
            }
            namePlateInfo->DisplayTitle.SetString(title.EncodeNullTerminated());
            ModifiedNamePlates.Remove((ulong)namePlateInfo);
        }
    }
    
    public void* UpdateNameplateNpcDetour(RaptureAtkModule* raptureAtkModule, RaptureAtkModule.NamePlateInfo* namePlateInfo, NumberArrayData* numArray, StringArrayData* stringArray, GameObject* gameObject, int numArrayIndex, int stringArrayIndex) {
        try {
            CleanupNamePlate(namePlateInfo, true);
        } catch (Exception ex) {
            PluginLog.LogError(ex, "Error in Cleanup of NPC Nameplate");
        }
        return updateNameplateHookNpc!.Original(raptureAtkModule, namePlateInfo, numArray, stringArray, gameObject, numArrayIndex, stringArrayIndex);
    }

    public void AfterNameplateUpdate(RaptureAtkModule.NamePlateInfo* namePlateInfo, BattleChara* battleChara) {
        var gameObject = &battleChara->Character.GameObject;
        if (gameObject->ObjectKind != 1 || gameObject->SubKind != 4) return;
        var player = PluginService.Objects.CreateObjectReference((nint)gameObject) as PlayerCharacter;
        if (player == null) return;
        using var fp = PerformanceMonitors.FrameProcessing.Start();
        using var pp = PerformanceMonitors.PlateProcessing.Start();
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
            if (ModifiedNamePlates.ContainsKey((ulong)namePlateInfo)) {
                ModifiedNamePlates[(ulong)namePlateInfo] = namePlateInfo->ObjectID.ObjectID;
            } else {
                ModifiedNamePlates.Add((ulong)namePlateInfo, namePlateInfo->ObjectID.ObjectID); 
            }
        }
        
        if (titleChanged && (nint) battleChara == PluginService.ClientState.LocalPlayer?.Address) {
            IpcProvider.ChangedLocalCharacterTitle(title);
        }
    }
    
    public static Dictionary<(string, uint), CustomTitle> IpcAssignedTitles { get; } = new();

    public CustomTitle GetOriginalTitle(PlayerCharacter playerCharacter) {
        var title = new CustomTitle();
        var character = (Character*) playerCharacter.Address;
        var titleId = character->CharacterData.TitleID;
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
        updateNameplateHookNpc?.Disable();
        updateNameplateHookNpc?.Dispose();
        updateNameplateHook = null;
        updateNameplateHookNpc = null;

        PluginService.Commands.RemoveHandler("/honorific");
        windowSystem.RemoveAllWindows();
        
        PluginService.PluginInterface.SavePluginConfig(Config);
    }
}

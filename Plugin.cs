﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.Command;
using Dalamud.Hooking;
using Dalamud.Interface.Windowing;
using Dalamud.Logging;
using Dalamud.Plugin;
using Dalamud.Utility;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Excel;
using Lumina.Excel.GeneratedSheets;
using BattleChara = FFXIVClientStructs.FFXIV.Client.Game.Character.BattleChara;

namespace Honorific;

public unsafe class Plugin : IDalamudPlugin {
    public string Name => "Honorific";
    
    public PluginConfig Config { get; private set; }
    
    [Signature("40 53 55 56 41 56 48 81 EC ?? ?? ?? ?? 48 8B 84 24", DetourName = nameof(UpdateNameplateDetour))]
    private Hook<UpdateNameplateDelegate>? updateNameplateHook;
    private delegate void* UpdateNameplateDelegate(RaptureAtkModule* raptureAtkModule, RaptureAtkModule.NamePlateInfo* namePlateInfo, NumberArrayData* numArray, StringArrayData* stringArray, BattleChara* battleChara, int numArrayIndex, int stringArrayIndex);
    
    private readonly ConfigWindow configWindow;
    private readonly WindowSystem windowSystem;

    private ExcelSheet<Title>? TitleSheet = null;
    
    
    public Plugin(DalamudPluginInterface pluginInterface) {
        pluginInterface.Create<PluginService>();

        TitleSheet = PluginService.Data.GetExcelSheet<Title>();
        if (TitleSheet == null) throw new Exception("Failed to load ExcelSheet<Title>");
        
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
        // pluginInterface.UiBuilder.Draw += UiBuilderOnDraw;
        
        PluginService.Commands.AddHandler("/honorific", new CommandInfo(OnCommand) {
            HelpMessage = $"Open the {Name} config window.",
            ShowInHelp = true
        });
        
        IpcProvider.Init(this);
    }

    private void OnCommand(string command, string args) {
        configWindow.IsOpen = !configWindow.IsOpen;
    }
    
    public void* UpdateNameplateDetour(RaptureAtkModule* raptureAtkModule, RaptureAtkModule.NamePlateInfo* namePlateInfo, NumberArrayData* numArray, StringArrayData* stringArray, BattleChara* battleChara, int numArrayIndex, int stringArrayIndex) {
        var r = updateNameplateHook!.Original(raptureAtkModule, namePlateInfo, numArray, stringArray, battleChara, numArrayIndex, stringArrayIndex);
        
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
        var needsUpdate = false;
        var gameObject = &battleChara->Character.GameObject;
              
        if (gameObject->ObjectKind != 1 || gameObject->SubKind != 4) return;
        var player = PluginService.Objects.CreateObjectReference((nint)gameObject) as PlayerCharacter;
        if (player == null) return;

        string titleText;
        bool titlePrefix;

        if (!TryGetTitle(player, out var title) || title == null) {
            var assignedTitleId = (short)namePlateInfo->Flags;

            if (assignedTitleId <= 0) {
                titleText = string.Empty;
                titlePrefix = false;
            } else {
                var titleData = TitleSheet!.GetRow((uint)assignedTitleId);
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
            needsUpdate = true;
            namePlateInfo->Title.SetString(titleText);
        }

        var displayTitle = string.IsNullOrEmpty(titleText) ? string.Empty : $"《{titleText}》";
        if (namePlateInfo->DisplayTitle.ToString() != displayTitle) {
            needsUpdate = true;
            namePlateInfo->DisplayTitle.SetString(displayTitle);
        }
        
        var isPrefix = (namePlateInfo->Flags & 0x1000000) == 0x1000000;
        namePlateInfo->Flags &= ~0x1000000;
        if (titlePrefix) namePlateInfo->Flags |= 0x1000000;
        if (isPrefix != titlePrefix) needsUpdate = true;
        
        if ((nint) battleChara == PluginService.ClientState.LocalPlayer?.Address) {
            IpcProvider.ChangedLocalCharacterTitle(titleText, titlePrefix);
        }
        
        if (needsUpdate) {
            // TODO: maybe find a way to improve this
            var unitManager = &Framework.Instance()->GetUiModule()->GetRaptureAtkModule()->RaptureAtkUnitManager;
            var addon = (AddonNamePlate*)unitManager->GetAddonByName("NamePlate");
            if (addon == null) return;
            addon->DoFullUpdate = 1;
            var arrayDataHolder = &Framework.Instance()->GetUiModule()->GetRaptureAtkModule()->AtkModule.AtkArrayDataHolder;
            var updateFunction = (delegate* unmanaged<RaptureAtkUnitManager*, ushort, NumberArrayData**, StringArrayData**, byte, void>)unitManager->AtkUnitManager.AtkEventListener.vfunc[11];
            updateFunction(unitManager, addon->AtkUnitBase.ID, arrayDataHolder->NumberArrays, arrayDataHolder->StringArrays, 1);
        }
    }
    
    public static Dictionary<(string, uint), CustomTitle> IpcAssignedTitles { get; } = new();

    public bool TryGetTitle(PlayerCharacter playerCharacter, out CustomTitle? title) {
        title = null;
        if (IpcAssignedTitles.TryGetValue((playerCharacter.Name.TextValue, playerCharacter.HomeWorld.Id), out title)) return true;
        if (!Config.TryGetCharacterConfig(playerCharacter.Name.TextValue, playerCharacter.HomeWorld.Id, out var characterConfig) || characterConfig == null) return false;
        
        foreach (var cTitle in characterConfig.CustomTitles.Where(t => t.Enabled && t.TitleCondition != TitleConditionType.None)) {
            switch (cTitle.TitleCondition) {
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
        
        return true;
    }
    
    
    public void Dispose() {
        IpcProvider.DeInit();
        updateNameplateHook?.Disable();
        updateNameplateHook?.Dispose();
        updateNameplateHook = null;

        PluginService.Commands.RemoveHandler("/honorific");
        windowSystem.RemoveAllWindows();
        
        PluginService.PluginInterface.SavePluginConfig(Config);
    }
}
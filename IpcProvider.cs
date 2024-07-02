using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Ipc;
using Newtonsoft.Json;

namespace Honorific; 

[SuppressMessage("ReSharper", "InconsistentNaming")]
public static class IpcProvider {
    public const uint MajorVersion = 2;
    public const uint MinorVersion = 1;

    public const string NameSpace = "Honorific";
    
    private static ICallGateProvider<(uint, uint)>? ApiVersion;

    private static ICallGateProvider<ICharacter, string, object>? SetCharacterTitle;
    private static ICallGateProvider<ICharacter, string>? GetCharacterTitle;
    private static ICallGateProvider<string>? GetLocalCharacterTitle;
    private static ICallGateProvider<ICharacter, object>? ClearCharacterTitle;
    private static ICallGateProvider<string, object>? LocalCharacterTitleChanged;
    private static ICallGateProvider<string, uint, TitleData[]>? GetCharacterTitleList;
    private static ICallGateProvider<object>? Ready;
    private static ICallGateProvider<object>? Disposing;

    internal static void Init(Plugin plugin) { 
        ApiVersion = PluginService.PluginInterface.GetIpcProvider<(uint, uint)>($"{NameSpace}.{nameof(ApiVersion)}");
        ApiVersion.RegisterFunc(() => (MajorVersion, MinorVersion));
        
        SetCharacterTitle = PluginService.PluginInterface.GetIpcProvider<ICharacter, string, object>($"{NameSpace}.{nameof(SetCharacterTitle)}");
        SetCharacterTitle.RegisterAction((character, titleDataJson) => {
            try {
                if (character is not IPlayerCharacter playerCharacter) return;
                Plugin.IpcAssignedTitles.Remove(playerCharacter.EntityId);
                if (titleDataJson == string.Empty) return;
                var titleData = JsonConvert.DeserializeObject<TitleData>(titleDataJson);
                if (titleData == null) return;
                Plugin.IpcAssignedTitles.Add(playerCharacter.EntityId, titleData);
            } catch (Exception ex) {
                PluginService.Log.Error(ex, $"Error handling {nameof(SetCharacterTitle)} IPC.");
            }
        });
        
        GetCharacterTitle = PluginService.PluginInterface.GetIpcProvider<ICharacter, string>($"{NameSpace}.{nameof(GetCharacterTitle)}");
        GetCharacterTitle.RegisterFunc(character => {
            if (character is not IPlayerCharacter playerCharacter) return string.Empty;
            if (!plugin.TryGetTitle(playerCharacter, out var title) || title == null) return string.Empty;
            return JsonConvert.SerializeObject((TitleData)title);
        });

        GetLocalCharacterTitle = PluginService.PluginInterface.GetIpcProvider<string>($"{NameSpace}.{nameof(GetLocalCharacterTitle)}");
        GetLocalCharacterTitle.RegisterFunc(() => {
            var player = PluginService.ClientState.LocalPlayer;
            if (player == null) return string.Empty;
            if (!plugin.TryGetTitle(player, out var title) || title == null) return string.Empty;
            return JsonConvert.SerializeObject((TitleData)title);
        });

        GetCharacterTitleList = PluginService.PluginInterface.GetIpcProvider<string, uint, TitleData[]>($"{NameSpace}.{nameof(GetCharacterTitleList)}");
        GetCharacterTitleList.RegisterFunc((name, world) => {
            if (!plugin.Config.TryGetCharacterConfig(name, world, out var characterConfig) || characterConfig == null) return Array.Empty<TitleData>();
            return new TitleData[] { characterConfig.DefaultTitle }.Union(characterConfig.CustomTitles.Select(x => (TitleData)x)).ToArray();
        });

        ClearCharacterTitle = PluginService.PluginInterface.GetIpcProvider<ICharacter, object>($"{NameSpace}.{nameof(ClearCharacterTitle)}");
        ClearCharacterTitle.RegisterAction(character => {
            if (character is not IPlayerCharacter playerCharacter) return;
            Plugin.IpcAssignedTitles.Remove(playerCharacter.EntityId);
        });

        LocalCharacterTitleChanged = PluginService.PluginInterface.GetIpcProvider<string, object>($"{NameSpace}.{nameof(LocalCharacterTitleChanged)}");
        Ready = PluginService.PluginInterface.GetIpcProvider<object>($"{NameSpace}.{nameof(Ready)}");
        Disposing = PluginService.PluginInterface.GetIpcProvider<object>($"{NameSpace}.{nameof(Disposing)}");
    }

    internal static void ChangedLocalCharacterTitle(TitleData? title) {
        var json = title == null || title.IsOriginal ? string.Empty : JsonConvert.SerializeObject(title);
        PluginService.Log.Verbose($"Report Local Title Changed: {json}");
        LocalCharacterTitleChanged?.SendMessage(json);
    }

    internal static void NotifyReady() {
        Ready?.SendMessage();
    }

    internal static void NotifyDisposing() {
        ChangedLocalCharacterTitle(null);
        Disposing?.SendMessage();
    }

    internal static void DeInit() {
        ApiVersion?.UnregisterFunc();
        SetCharacterTitle?.UnregisterAction();
        ClearCharacterTitle?.UnregisterAction();
        GetCharacterTitle?.UnregisterFunc();
        GetLocalCharacterTitle?.UnregisterFunc();
        GetCharacterTitleList?.UnregisterFunc();
        LocalCharacterTitleChanged = null;
        Ready = null;
        Disposing = null;
    }
}

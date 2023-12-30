using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Logging;
using Dalamud.Plugin.Ipc;
using Newtonsoft.Json;

namespace Honorific; 

[SuppressMessage("ReSharper", "InconsistentNaming")]
public static class IpcProvider {
    public const uint MajorVersion = 2;
    public const uint MinorVersion = 0;

    public const string NameSpace = "Honorific";
    
    private static ICallGateProvider<(uint, uint)>? ApiVersion;

    private static ICallGateProvider<Character, string, object>? SetCharacterTitle;
    private static ICallGateProvider<Character, string>? GetCharacterTitle;
    private static ICallGateProvider<string>? GetLocalCharacterTitle;
    private static ICallGateProvider<Character, object>? ClearCharacterTitle;
    private static ICallGateProvider<string, object>? LocalCharacterTitleChanged;
    private static ICallGateProvider<string, uint, TitleData[]>? GetCharacterTitleList;
    private static ICallGateProvider<object>? Ready;
    private static ICallGateProvider<object>? Disposing;

    internal static void Init(Plugin plugin) { 
        ApiVersion = PluginService.PluginInterface.GetIpcProvider<(uint, uint)>($"{NameSpace}.{nameof(ApiVersion)}");
        ApiVersion.RegisterFunc(() => (MajorVersion, MinorVersion));
        
        SetCharacterTitle = PluginService.PluginInterface.GetIpcProvider<Character, string, object>($"{NameSpace}.{nameof(SetCharacterTitle)}");
        SetCharacterTitle.RegisterAction((character, titleDataJson) => {
            try {
                if (character is not PlayerCharacter playerCharacter) return;
                Plugin.IpcAssignedTitles.Remove(playerCharacter.ObjectId);
                if (titleDataJson == string.Empty) return;
                var titleData = JsonConvert.DeserializeObject<TitleData>(titleDataJson);
                if (titleData == null) return;
                Plugin.IpcAssignedTitles.Add(playerCharacter.ObjectId, titleData);
            } catch (Exception ex) {
                PluginService.Log.Error(ex, $"Error handling {nameof(SetCharacterTitle)} IPC.");
            }
        });
        
        GetCharacterTitle = PluginService.PluginInterface.GetIpcProvider<Character, string>($"{NameSpace}.{nameof(GetCharacterTitle)}");
        GetCharacterTitle.RegisterFunc(character => {
            if (character is not PlayerCharacter playerCharacter) return string.Empty;
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
            if (!plugin.Config.TryGetCharacterConfig(name, world, out var characterConfig)) return Array.Empty<TitleData>();
            return new TitleData[] { characterConfig.DefaultTitle }.Union(characterConfig.CustomTitles.Select(x => (TitleData)x)).ToArray();
        });

        ClearCharacterTitle = PluginService.PluginInterface.GetIpcProvider<Character, object>($"{NameSpace}.{nameof(ClearCharacterTitle)}");
        ClearCharacterTitle.RegisterAction(character => {
            if (character is not PlayerCharacter playerCharacter) return;
            Plugin.IpcAssignedTitles.Remove(playerCharacter.ObjectId);
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

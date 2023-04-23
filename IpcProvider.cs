using System.Diagnostics.CodeAnalysis;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Ipc;

namespace Honorific; 

[SuppressMessage("ReSharper", "InconsistentNaming")]
public static class IpcProvider {
    public const uint MajorVersion = 1;
    public const uint MinorVersion = 1;

    public const string NameSpace = "Honorific";
    
    private static ICallGateProvider<(uint, uint)>? ApiVersion;

    private static ICallGateProvider<Character, string, bool, object>? SetCharacterTitle;
    private static ICallGateProvider<Character, (string, bool)>? GetCharacterTitle;
    private static ICallGateProvider<(string, bool)>? GetLocalCharacterTitle;
    private static ICallGateProvider<Character, object>? ClearCharacterTitle;
    private static ICallGateProvider<string, bool, object>? LocalCharacterTitleChanged;
    private static ICallGateProvider<object>? Ready;
    private static ICallGateProvider<object>? Disposing;

    internal static void Init(Plugin plugin) { 
        ApiVersion = PluginService.PluginInterface.GetIpcProvider<(uint, uint)>($"{NameSpace}.{nameof(ApiVersion)}");
        ApiVersion.RegisterFunc(() => (MajorVersion, MinorVersion));
        
        SetCharacterTitle = PluginService.PluginInterface.GetIpcProvider<Character, string, bool, object>($"{NameSpace}.{nameof(SetCharacterTitle)}");
        SetCharacterTitle.RegisterAction((character, title, isPrefix) => {
            if (character is not PlayerCharacter playerCharacter) return;
            Plugin.IpcAssignedTitles.Remove((playerCharacter.Name.TextValue, playerCharacter.HomeWorld.Id));
            Plugin.IpcAssignedTitles.Add((playerCharacter.Name.TextValue, playerCharacter.HomeWorld.Id), new CustomTitle() {
                Title = title,
                IsPrefix = isPrefix
            });
        });
        
        GetCharacterTitle = PluginService.PluginInterface.GetIpcProvider<Character, (string, bool)>($"{NameSpace}.{nameof(GetCharacterTitle)}");
        GetCharacterTitle.RegisterFunc(character => {
            if (character is not PlayerCharacter playerCharacter) return (string.Empty, false);
            if (!plugin.TryGetTitle(playerCharacter, out var title) || title == null) return (string.Empty, false);
            return (title.Title ?? string.Empty, title.IsPrefix);
        });

        GetLocalCharacterTitle = PluginService.PluginInterface.GetIpcProvider<(string, bool)>($"{NameSpace}.{nameof(GetLocalCharacterTitle)}");
        GetLocalCharacterTitle.RegisterFunc(() => {
            var player = PluginService.ClientState.LocalPlayer;
            if (player == null) return (string.Empty, false);
            if (!plugin.TryGetTitle(player, out var title) || title == null) return (string.Empty, false);
            return (title.Title ?? string.Empty, title.IsPrefix);
        });
        
        ClearCharacterTitle = PluginService.PluginInterface.GetIpcProvider<Character, object>($"{NameSpace}.{nameof(ClearCharacterTitle)}");
        ClearCharacterTitle.RegisterAction(character => {
            if (character is not PlayerCharacter playerCharacter) return;
            Plugin.IpcAssignedTitles.Remove((playerCharacter.Name.TextValue, playerCharacter.HomeWorld.Id));
        });

        LocalCharacterTitleChanged = PluginService.PluginInterface.GetIpcProvider<string, bool, object>($"{NameSpace}.{nameof(LocalCharacterTitleChanged)}");
        Ready = PluginService.PluginInterface.GetIpcProvider<object>($"{NameSpace}.{nameof(Ready)}");
        Disposing = PluginService.PluginInterface.GetIpcProvider<object>($"{NameSpace}.{nameof(Disposing)}");
    }

    internal static void ChangedLocalCharacterTitle(string title, bool isPrefix) {
        LocalCharacterTitleChanged?.SendMessage(title, isPrefix);
    }

    internal static void NotifyReady() {
        Ready?.SendMessage();
    }

    internal static void NotifyDisposing() {
        Disposing?.SendMessage();
    }

    internal static void DeInit() {
        ApiVersion?.UnregisterFunc();
        SetCharacterTitle?.UnregisterAction();
        ClearCharacterTitle?.UnregisterAction();
        GetCharacterTitle?.UnregisterFunc();
        GetLocalCharacterTitle?.UnregisterFunc();
        LocalCharacterTitleChanged = null;
        Ready = null;
        Disposing = null;
    }
}

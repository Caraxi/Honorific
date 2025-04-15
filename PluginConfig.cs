using System.Collections.Generic;
using Dalamud.Configuration;

namespace Honorific; 

public class PluginConfig : IPluginConfiguration {
    public int Version { get; set; } = 1;

    public Dictionary<uint, Dictionary<string, CharacterConfig>> WorldCharacterDictionary = new();

    public bool ShowColoredTitles = true;
    public bool DebugOpenOnStatup = true;
    public bool HideKofi = false;
    public bool ApplyToInspect = true;
    public bool DisplayPreviewInConfigWindow = true;

    public bool HideVanillaSelf;
    public bool HideVanillaParty;
    public bool HideVanillaAlliance;
    public bool HideVanillaFriends;
    public bool HideVanillaOther;

    public Dictionary<ulong, (string, uint)> IdentifyAs = new();
    
    public bool TryGetCharacterConfig(string name, uint world, out CharacterConfig? characterConfig) {
        characterConfig = null;
        if (!WorldCharacterDictionary.TryGetValue(world, out var w)) return false;
        return w.TryGetValue(name, out characterConfig);
    }

    public bool TryAddCharacter(string name, uint homeWorld) {
        if (!WorldCharacterDictionary.ContainsKey(homeWorld)) WorldCharacterDictionary.Add(homeWorld, new Dictionary<string, CharacterConfig>());
        if (WorldCharacterDictionary.TryGetValue(homeWorld, out var world)) {
            return world.TryAdd(name, new CharacterConfig());
        }

        return false;
    }

    public bool TryGetOrAddCharacter(string name, uint world, out CharacterConfig? characterConfig) {
        if (TryGetCharacterConfig(name, world, out characterConfig)) {
            return true;
        }
        return TryAddCharacter(name, world) && TryGetCharacterConfig(name, world, out characterConfig);
    }
}

﻿using System.Collections.Generic;
using Dalamud.Configuration;

namespace Honorific; 

public class PluginConfig : IPluginConfiguration {
    public int Version { get; set; } = 1;

    public Dictionary<uint, Dictionary<string, CharacterConfig>> WorldCharacterDictionary = new();

    public bool ShowColoredTitles = true;
    public bool DebugOpenOnStatup = true;
    public bool HideKofi = false;

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
}

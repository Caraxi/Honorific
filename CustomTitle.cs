using System.Linq;
using System;
using System.Numerics;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.Text.SeStringHandling;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using Newtonsoft.Json;

namespace Honorific;

public class TitleData {
    public string? Title = string.Empty;
    public bool IsPrefix;
    public bool IsOriginal;
    public Vector3? Color;
    public Vector3? Glow;

    public static implicit operator TitleData(CustomTitle title) => new() {
        Title = title.Title,
        IsPrefix = title.IsPrefix,
        Color = title.Color,
        Glow = title.Glow,
        IsOriginal = title.IsOriginal
    };
    public static implicit operator CustomTitle(TitleData data) => new() {
        Title = data.Title,
        IsPrefix = data.IsPrefix,
        Color = data.Color,
        Glow = data.Glow,
        IsOriginal = data.IsOriginal,
    };
}

public class CustomTitle {
    public string? Title = string.Empty;
    public bool IsPrefix;
    public bool IsOriginal;
    public string UniqueId = string.Empty;
    
    public bool Enabled;
    public TitleConditionType TitleCondition = TitleConditionType.None;
    public int ConditionParam0;

    public Vector3? Color;
    public Vector3? Glow;
    
    [JsonIgnore] public string DisplayTitle => $"《{Title}》";

    public bool IsValid() {
        if (Title == null) return false;
        if (Title.Length > Plugin.MaxTitleLength) return false;
        if (Title.Any(char.IsControl)) return false;
        return true;
    }

    public SeString ToSeString(bool includeQuotes = true, bool includeColor = true) {
        if (string.IsNullOrEmpty(Title)) return SeString.Empty;
        var builder = new SeStringBuilder();
        if (includeQuotes) builder.AddText("《");

        if (includeColor && Color != null) builder.Add(new ColorPayload(Color.Value));
        if (includeColor && Glow != null) builder.Add(new GlowPayload(Glow.Value));
        builder.AddText(Title);
        if (includeColor && Glow != null) builder.Add(new GlowEndPayload());
        if (includeColor && Color != null) builder.Add(new ColorEndPayload());
        if (includeQuotes) builder.AddText("》");
        return builder.Build().Cleanup();
    }
    
    public string GetUniqueId(CharacterConfig characterConfig) {
        if (string.IsNullOrEmpty(UniqueId) || UniqueId.Length < 6 || characterConfig.CustomTitles.Count(t => t.UniqueId == UniqueId) > 1) {
            string id;
            var c = 0;
            var r = new Random();
            do {
                id = "uid:";
                id += (char)r.Next('a', 'z');
                id += (char)r.Next('0', '9');
                while (id.Length < c % 10) {
                    id += (char)r.Next('a', 'z');
                    id += (char)r.Next('0', '9');
                }
                c++;
            } while (characterConfig.CustomTitles.Any(t => t.UniqueId == id));
            UniqueId = id;
        }
        return UniqueId;
    }
    
    public unsafe bool MatchesConditions(PlayerCharacter playerCharacter) {
        switch (TitleCondition) {
            case TitleConditionType.None:
                return true;
            case TitleConditionType.ClassJob:
                return ConditionParam0 == playerCharacter.ClassJob.Id;
            case TitleConditionType.JobRole:
                if (ConditionParam0 == 0) return false;
                return playerCharacter.ClassJob.GameData?.IsRole((ClassJobRole)ConditionParam0) ?? false;
            case TitleConditionType.GearSet:
                if (playerCharacter != PluginService.ClientState.LocalPlayer) return false;
                var gearSetModule = RaptureGearsetModule.Instance();
                if (gearSetModule == null) return false;
                return RaptureGearsetModule.Instance()->CurrentGearsetIndex == ConditionParam0;
            case TitleConditionType.Title:
                var c = (Character*)playerCharacter.Address;
                return c->CharacterData.TitleID == ConditionParam0;
            default:
                return false;
        }
    }
}

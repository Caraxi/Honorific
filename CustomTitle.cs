﻿using System.Linq;
using System;
using System.Numerics;
using System.Text;
using System.Text.RegularExpressions;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Interface.Colors;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using Newtonsoft.Json;

namespace Honorific;

public class TitleData {
    public string? Title = string.Empty;
    public bool IsPrefix;
    public bool IsOriginal;
    public Vector3? Color;
    public Vector3? Glow;
    public Palette? TitlePalette;

    public static implicit operator TitleData(CustomTitle title) => new() {
        Title = title.Title,
        IsPrefix = title.IsPrefix,
        Color = title.Color,
        Glow = title.Glow,
        IsOriginal = title.IsOriginal,
        TitlePalette = title.TitlePalette
    };
    public static implicit operator CustomTitle(TitleData data) => new() {
        Title = data.Title,
        IsPrefix = data.IsPrefix,
        Color = data.Color,
        Glow = data.Glow,
        IsOriginal = data.IsOriginal,
        TitlePalette = data.TitlePalette
    };
}

public partial class CustomTitle {
    public string? Title = string.Empty;
    public bool IsPrefix;
    public bool IsOriginal;
    public string UniqueId = string.Empty;
    public Palette? TitlePalette;

    public bool Enabled;
    public TitleConditionType TitleCondition = TitleConditionType.None;
    public int ConditionParam0;

    public bool ShouldSerializeLocationCondition() => TitleCondition == TitleConditionType.Location;
    public LocationCondition? LocationCondition;

    public Vector3? Color;
    public Vector3? Glow;
    
    [JsonIgnore] public string WarningMessage = string.Empty;
    [JsonIgnore] public Vector4 WarningColour = ImGuiColors.DalamudWhite;
    [JsonIgnore] public string DisplayTitle => $"《{Title}》";
    [JsonIgnore] public bool EditorActive;

    public bool IsValid() {
        if (Title == null) return false;
        if (Title.Length > Plugin.MaxTitleLength) return false;
        if (Title.Any(char.IsControl)) return false;
        return true;
    }

    public SeString ToSeString(bool includeQuotes = true, bool includeColor = true)
    {
        if (string.IsNullOrEmpty(Title)) return SeString.Empty;
        if (TitlePalette != null && includeColor)
        {
            return Palette.PaintSeString(Title, TitlePalette, Color, Glow, includeQuotes);
        }
        else
        {
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
    
    public unsafe bool MatchesConditions(IPlayerCharacter playerCharacter) {
        using var _ = PerformanceMonitors.Run($"MatchesConditionCheck:{TitleCondition}");
        switch (TitleCondition) {
            case TitleConditionType.None:
                return true;
            case TitleConditionType.ClassJob:
                return ConditionParam0 == playerCharacter.ClassJob.RowId;
            case TitleConditionType.JobRole:
                if (ConditionParam0 == 0) return false;
                return playerCharacter.ClassJob.Value.IsRole((ClassJobRole)ConditionParam0);
            case TitleConditionType.GearSet:
                if (PluginService.ClientState.LocalPlayer == null || playerCharacter.EntityId != PluginService.ClientState.LocalPlayer.EntityId) return false;
                var gearSetModule = RaptureGearsetModule.Instance();
                if (gearSetModule == null) return false;
                return RaptureGearsetModule.Instance()->CurrentGearsetIndex == ConditionParam0;
            case TitleConditionType.Title:
                var c = (Character*)playerCharacter.Address;
                return c->CharacterData.TitleId == ConditionParam0;
            case TitleConditionType.Location:
                if (LocationCondition == null) return false;
                if (LocationCondition.TerritoryType != PluginService.ClientState.TerritoryType) return false;
                if (LocationCondition.World != null && LocationCondition.World.Value != playerCharacter.CurrentWorld.RowId) return false;
                if (!LocationCondition.ShouldSerializeWard() || LocationCondition.Ward == null) return true;
                if (HousingManager.Instance()->GetCurrentWard() != LocationCondition.Ward) return false;
                if (!LocationCondition.ShouldSerializePlot() || LocationCondition.Plot == null) return true;
                if (HousingManager.Instance()->GetCurrentPlot() != LocationCondition.Plot) return false;
                if (!LocationCondition.ShouldSerializeRoom() || LocationCondition.Room == null) return true;
                return HousingManager.Instance()->GetCurrentRoom() == LocationCondition.Room;
            default:
                return false;
        }
    }
    
    [GeneratedRegex("^[-a-zA-Z0-9@:%._\\+~#=]{1,256}[\\.,][a-zA-Z0-9()]{1,6}\\b(?:[-a-zA-Z0-9()@:%_\\+.~#?&\\/=]*)$")]
    private static partial Regex UrlRegex();
    
    public void UpdateWarning() {
        WarningMessage = string.Empty;

        if (!IsValid()) {
            WarningMessage = "Title is invalid.\nThis title will not be displayed.";
            WarningColour = ImGuiColors.DalamudRed;
            return;
        }

        if (string.IsNullOrWhiteSpace(Title)) return;
        
        var mare = PluginService.PluginInterface.InstalledPlugins.FirstOrDefault(p => string.Equals(p.InternalName, "MareSynchronos", StringComparison.InvariantCultureIgnoreCase) && p.IsLoaded);
        if ( mare != null) {
            var title = Title.Normalize(NormalizationForm.FormKD);
            if (UrlRegex().IsMatch(title)) {
                WarningMessage = $"This title will not be accepted by {mare.Name} and will prevent syncing of your character.\nPlease do not use URLs in your title.";
                WarningColour = ImGuiColors.DalamudRed;
            }
        }
    }
}

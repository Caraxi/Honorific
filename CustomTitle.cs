using System.Linq;
using System;
using System.Numerics;
using System.Text;
using System.Text.RegularExpressions;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Interface.Colors;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using Honorific.Gradient;
using Newtonsoft.Json;
using SeString = Dalamud.Game.Text.SeStringHandling.SeString;
using SeStringBuilder = Lumina.Text.SeStringBuilder;

namespace Honorific;

public class TitleData {
    
    public string? Title = string.Empty;
    public bool IsPrefix;
    public bool IsOriginal;
    public Vector3? Color;
    public Vector3? Glow;
    public Vector3? Color3;
    public int? GradientColourSet;
    public GradientAnimationStyle? GradientAnimationStyle;

    public static implicit operator TitleData(CustomTitle title) => new() {
        Title = title.Title,
        IsPrefix = title.IsPrefix,
        Color = title.Color,
        Glow = title.Glow,
        Color3 = title.Color3,
        IsOriginal = title.IsOriginal,
        GradientColourSet = title.GradientColourSet,
        GradientAnimationStyle = title.GradientAnimationStyle
    };

    public static implicit operator CustomTitle(TitleData data) => new() {
        Title = data.Title,
        IsPrefix = data.IsPrefix,
        Color = data.Color,
        Glow = data.Glow,
        Color3 = data.Color3,
        IsOriginal = data.IsOriginal,
        GradientColourSet = data.GradientColourSet,
        GradientAnimationStyle = data.GradientAnimationStyle,
    };

    public override bool Equals(object? obj) {
        if (obj is not TitleData other) return false;
        return Title == other.Title
               && IsPrefix == other.IsPrefix
               && IsOriginal == other.IsOriginal
               && NullableVectorEquals(Color, other.Color)
               && NullableVectorEquals(Glow, other.Glow)
               && NullableVectorEquals(Color3, other.Color3)
               && GradientColourSet == other.GradientColourSet
               && GradientAnimationStyle == other.GradientAnimationStyle;
    }
               
    
    private static bool NullableVectorEquals(Vector3? a, Vector3? b) {
        if (!a.HasValue && !b.HasValue) return true;
        if (a.HasValue != b.HasValue) return false;
        return a!.Value == b!.Value;
    }
    
    public override int GetHashCode() => HashCode.Combine(Title, IsPrefix, IsOriginal, Color, Glow, GradientColourSet, GradientAnimationStyle, Color3);
}

public partial class CustomTitle {
    public string? Title = string.Empty;
    public bool IsPrefix;
    public bool IsOriginal;
    public string UniqueId = string.Empty;
    
    public bool Enabled;
    public TitleConditionType TitleCondition = TitleConditionType.None;
    public int ConditionParam0;
    
    public int? GradientColourSet;
    public GradientAnimationStyle? GradientAnimationStyle;

    public bool ShouldSerializeCustomRainbowStyle() => CustomRainbowStyle != null;
    public GradientStyle? CustomRainbowStyle;
    

    public bool ShouldSerializeLocationCondition() => TitleCondition == TitleConditionType.Location;
    public LocationCondition? LocationCondition;

    public Vector3? Color;
    public Vector3? Glow;
    public Vector3? Color3;
    
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

    public SeString ToSeString(bool includeQuotes = true, bool includeColor = true, bool animate = true) {
        if (string.IsNullOrEmpty(Title)) return SeString.Empty;
        var builder = new SeStringBuilder();
        if (includeQuotes) builder.Append("《");
        if (includeColor && Color != null) builder.PushColorRgba(new Vector4(Color.Value, 1));
        void AppendTitle() {

            if (!includeColor) {
                builder.Append(Title);
                return;
            }

            if (CustomRainbowStyle != null) {
                CustomRainbowStyle.Apply(builder, Title, animate);
                return;
            }
            
            if (GradientColourSet != null) {
                var style = GradientColourSet.Value switch {
                    -1 => GradientSystem.GetDualColourStyle(Glow, Color3, GradientAnimationStyle),
                    _ => GradientSystem.GetStyle(GradientColourSet.Value, GradientAnimationStyle)
                };

                if (style != null) {
                    style.Apply(builder, Title, animate);
                    return;
                }
            }
            
            if (Glow != null) {
                builder.PushEdgeColorRgba(new Vector4(Glow.Value, 1));
                builder.Append(Title);
                builder.PopEdgeColor();
                return;
            }

            builder.Append(Title);
        }
        AppendTitle();
        
        
        if (includeColor && Color != null) builder.PopColor();
        if (includeQuotes) builder.Append("》");
        return SeString.Parse(builder.GetViewAsSpan());
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
                if (PluginService.Objects.LocalPlayer == null || playerCharacter.EntityId != PluginService.Objects.LocalPlayer.EntityId) return false;
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
    
    public void UpdateWarning() {
        WarningMessage = string.Empty;
        if (!IsValid()) {
            WarningMessage = "Title is invalid.\nThis title will not be displayed.";
            WarningColour = ImGuiColors.DalamudRed;
            return;
        }
    }
}

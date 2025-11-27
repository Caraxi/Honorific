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
    public int RainbowMode;

    public static implicit operator TitleData(CustomTitle title) => new() {
        Title = title.Title,
        IsPrefix = title.IsPrefix,
        Color = title.Color,
        Glow = title.Glow,
        IsOriginal = title.IsOriginal,
        RainbowMode = title.RainbowMode
    };
    public static implicit operator CustomTitle(TitleData data) => new() {
        Title = data.Title,
        IsPrefix = data.IsPrefix,
        Color = data.Color,
        Glow = data.Glow,
        IsOriginal = data.IsOriginal,
        RainbowMode = data.RainbowMode,
    };

    public override bool Equals(object? obj) {
        if (obj is not TitleData other) return false;
        return Title == other.Title
               && IsPrefix == other.IsPrefix
               && IsOriginal == other.IsOriginal
               && NullableVectorEquals(Color, other.Color)
               && NullableVectorEquals(Glow, other.Glow)
               && RainbowMode == other.RainbowMode;
    }
               
    
    private static bool NullableVectorEquals(Vector3? a, Vector3? b) {
        if (!a.HasValue && !b.HasValue) return true;
        if (a.HasValue != b.HasValue) return false;
        return a!.Value == b!.Value;
    }
    
    public override int GetHashCode() => HashCode.Combine(Title, IsPrefix, IsOriginal, Color, Glow, RainbowMode);
}

public partial class CustomTitle {
    public string? Title = string.Empty;
    public bool IsPrefix;
    public bool IsOriginal;
    public string UniqueId = string.Empty;
    
    public bool Enabled;
    public TitleConditionType TitleCondition = TitleConditionType.None;
    public int ConditionParam0;

    public int RainbowMode;
    public RainbowColour.RainbowStyle? CustomRainbowStyle;
    
    

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

    public SeString ToSeString(bool includeQuotes = true, bool includeColor = true, bool animate = true) {
        if (string.IsNullOrEmpty(Title)) return SeString.Empty;
        var builder = new SeStringBuilder();
        if (includeQuotes) builder.Append("《");

        if (includeColor && Color != null) builder.PushColorRgba(new Vector4(Color.Value, 1));
        if (includeColor && RainbowMode <= 0 && Glow != null) builder.PushEdgeColorRgba(new Vector4(Glow.Value, 1));

        if (includeColor && RainbowMode > 0 && RainbowMode <= RainbowColour.NumColourLists) {
            if (Title.Length > 25) {
                for (var i = 0; i < Title.Length; i+=2) {
                    var glow = CustomRainbowStyle != null
                        ? RainbowColour.GetColourRGB(CustomRainbowStyle, i, 5, animate)
                        : RainbowColour.GetColourRGB(RainbowMode, i, 5, animate);
                    builder.PushEdgeColorRgba(glow.R, glow.G, glow.B, 255);
                    builder.Append(Title.Substring(i, Math.Min(2, Title.Length - i)));
                    builder.PopEdgeColor();
                }
            } else {
                var i = 0;
                foreach (var c in Title) {
                    var glow = CustomRainbowStyle != null
                        ? RainbowColour.GetColourRGB(CustomRainbowStyle, i++, 5, animate)
                        : RainbowColour.GetColourRGB(RainbowMode, i++, 5, animate);
                    builder.PushEdgeColorRgba(glow.R, glow.G, glow.B, 255);
                    builder.AppendChar(c);
                    builder.PopEdgeColor();
                }
            }
        } else {
            builder.Append(Title);
        }


        if (includeColor && RainbowMode <= 0 && Glow != null) builder.PopEdgeColor();
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

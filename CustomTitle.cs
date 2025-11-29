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
    public int RainbowMode;
    public int? GradientColourSet;
    public GradientAnimationStyle? GradientAnimationStyle;
    public string? GradientBase64;

    public static implicit operator TitleData(CustomTitle title) {
        var data = new TitleData {
            Title = title.Title,
            IsPrefix = title.IsPrefix,
            Color = title.Color,
            Glow = title.Glow,
            IsOriginal = title.IsOriginal,
            GradientColourSet = title.GradientColourSet,
            GradientAnimationStyle = title.GradientAnimationStyle,
            RainbowMode = GetRainbowMode(title.GradientColourSet, title.GradientAnimationStyle),
        };

        // Include base64 gradient data if a gradient is used
        if (title.CustomGradientStyle != null) {
            data.GradientBase64 = title.CustomGradientStyle.Encode();
        } else if (title.GradientColourSet != null && title.GradientAnimationStyle != null) {
            var style = GradientSystem.GetStyle(title.GradientColourSet.Value, title.GradientAnimationStyle);
            if (style != null) {
                data.GradientBase64 = style.Encode();
            }
        } else if (title.RainbowMode > 0) {
            var style = GradientSystem.GetStyle(title.RainbowMode);
            if (style != null) {
                data.GradientBase64 = style.Encode();
            }
        }

        return data;
    }

    private static int GetRainbowMode(int? titleGradientColourSet, GradientAnimationStyle? titleGradientAnimationStyle) {
        if (titleGradientColourSet == null) return 0;
        if (titleGradientAnimationStyle is null or Gradient.GradientAnimationStyle.Static) return 0;
        if (titleGradientColourSet >= 5) return 0;
        return ((titleGradientColourSet.Value) * 2) + (titleGradientAnimationStyle == Gradient.GradientAnimationStyle.Wave ? 1 : 2);
    }

    public static implicit operator CustomTitle(TitleData data) {
        var title = new CustomTitle {
            Title = data.Title,
            IsPrefix = data.IsPrefix,
            Color = data.Color,
            Glow = data.Glow,
            IsOriginal = data.IsOriginal,
            GradientColourSet = data.GradientColourSet ?? GetColourSet(data.RainbowMode),
            GradientAnimationStyle = data.GradientAnimationStyle ?? (data.GradientColourSet == null ? GetAnimationStyle(data.RainbowMode) : null),
        };

        // If base64 gradient data is provided, reconstruct the gradient style
        if (!string.IsNullOrEmpty(data.GradientBase64) && data.GradientAnimationStyle != null) {
            try {
                title.CustomGradientStyle = new GradientStyle("IPC Gradient", data.GradientBase64, data.GradientAnimationStyle.Value);
            } catch {
                // If base64 decoding fails, fall back to the ID-based approach
            }
        }

        return title;
    }

    public static GradientAnimationStyle? GetAnimationStyle(int dataRainbowMode) {
        if (dataRainbowMode == 0) return null;
        return (dataRainbowMode - 1) % 2 == 0 ? Gradient.GradientAnimationStyle.Wave : Gradient.GradientAnimationStyle.Pulse;
    }

    public static int? GetColourSet(int dataRainbowMode) {
        if (dataRainbowMode == 0) return null;
        return (dataRainbowMode - 1) / 2;
    }

    public override bool Equals(object? obj) {
        if (obj is not TitleData other) return false;
        return Title == other.Title
               && IsPrefix == other.IsPrefix
               && IsOriginal == other.IsOriginal
               && NullableVectorEquals(Color, other.Color)
               && NullableVectorEquals(Glow, other.Glow)
               && GradientColourSet == other.GradientColourSet
               && GradientAnimationStyle == other.GradientAnimationStyle
               && GradientBase64 == other.GradientBase64;
    }
               
    
    private static bool NullableVectorEquals(Vector3? a, Vector3? b) {
        if (!a.HasValue && !b.HasValue) return true;
        if (a.HasValue != b.HasValue) return false;
        return a!.Value == b!.Value;
    }
    
    public override int GetHashCode() => HashCode.Combine(Title, IsPrefix, IsOriginal, Color, Glow, GradientColourSet, GradientAnimationStyle, GradientBase64);
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
    public int? GradientColourSet;
    public GradientAnimationStyle? GradientAnimationStyle;

    public Guid? CustomGradientId;

    public bool ShouldSerializeCustomGradientStyle() => false;
    [Newtonsoft.Json.JsonIgnore]
    public GradientStyle? CustomGradientStyle;
    

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
    
    public void Update() {
        if (RainbowMode >= 0 && GradientColourSet == null) {
            var style = GradientSystem.GetStyle(RainbowMode);
            if (style != null) {
                GradientColourSet = style.ColourSet;
                GradientAnimationStyle = style.AnimationStyle;
            }
        }
    }

    public void ReconstructCustomGradient(PluginConfig config) {
        if (CustomGradientId != null && CustomGradientStyle == null) {
            var customGradient = config.CustomGradients.FirstOrDefault(g => g.Id == CustomGradientId);
            if (customGradient != null && GradientAnimationStyle != null) {
                CustomGradientStyle = customGradient.ToGradientStyle(GradientAnimationStyle.Value);
            }
        }
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

            if (CustomGradientStyle != null) {
                CustomGradientStyle.Apply(builder, Title, animate);
                return;
            }
            
            if (GradientColourSet != null) {
                var style = GradientSystem.GetStyle(GradientColourSet.Value, GradientAnimationStyle);
                if (style != null) {
                    style.Apply(builder, Title, animate);
                    return;
                }
            }
            
            if (RainbowMode > 0 && RainbowMode <= GradientSystem.NumColourLists) {
                var style = GradientSystem.GetStyle(RainbowMode);
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

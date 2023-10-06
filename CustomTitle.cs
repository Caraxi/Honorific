using System.Linq;
using System.Numerics;
using Dalamud.Game.Text.SeStringHandling;
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
    
    public bool Enabled;
    public TitleConditionType TitleCondition = TitleConditionType.None;
    public int ConditionParam0;

    public Vector3? Color;
    public Vector3? Glow;
    
    [JsonIgnore] public string DisplayTitle => $"《{Title}》";

    public bool IsValid() {
        if (Title == null) return false;
        if (Title.Length > 25) return false;
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
    
    
}

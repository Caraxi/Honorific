using System.Linq;
using Dalamud.Game.Text.SeStringHandling;
using Newtonsoft.Json;

namespace Honorific;

public class TitleData {
    public string? Title = string.Empty;
    public bool IsPrefix;
    public ushort ColorKey;
    public ushort GlowKey;

    public static implicit operator TitleData(CustomTitle title) => new() {
        Title = title.Title,
        IsPrefix = title.IsPrefix,
        ColorKey =  title.ColorKey,
        GlowKey = title.GlowKey,
    };
    public static implicit operator CustomTitle(TitleData data) => new() {
        Title = data.Title,
        IsPrefix = data.IsPrefix,
        ColorKey = data.ColorKey,
        GlowKey = data.GlowKey,
    };
}

public class CustomTitle {
    public string? Title = string.Empty;
    public bool IsPrefix;
    
    public bool Enabled;
    public TitleConditionType TitleCondition = TitleConditionType.None;
    public int ConditionParam0;
    
    public ushort ColorKey = 0;
    public ushort GlowKey = 0;
    
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
        if (includeColor && ColorKey != 0) builder.AddUiForeground(ColorKey);
        if (includeColor && GlowKey != 0) builder.AddUiGlow(GlowKey);
        builder.AddText(Title);
        if (includeColor && GlowKey != 0) builder.AddUiGlowOff();
        if (includeColor && ColorKey != 0) builder.AddUiForegroundOff();
        if (includeQuotes) builder.AddText("》");
        return builder.Build().Cleanup();
    }
    
    
}

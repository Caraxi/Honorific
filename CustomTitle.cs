using System.Linq;
using System.Numerics;
using Dalamud.Game.Text.SeStringHandling;
using Lumina.Excel.GeneratedSheets;
using Newtonsoft.Json;

namespace Honorific;

public class TitleData {
    public string? Title = string.Empty;
    public bool IsPrefix;
    public ushort ColorKey;
    public ushort GlowKey;
    public Vector3? Color;
    public Vector3? Glow;

    public static implicit operator TitleData(CustomTitle title) => new() {
        Title = title.Title,
        IsPrefix = title.IsPrefix,
        ColorKey =  title.ColorKey,
        GlowKey = title.GlowKey,
        Color = title.Color,
        Glow = title.Glow,
    };
    public static implicit operator CustomTitle(TitleData data) => new() {
        Title = data.Title,
        IsPrefix = data.IsPrefix,
        ColorKey = data.ColorKey,
        GlowKey = data.GlowKey,
        Color = data.Color,
        Glow = data.Glow,
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

    public Vector3? Color;
    public Vector3? Glow;
    
    [JsonIgnore] public string DisplayTitle => $"《{Title}》";

    public bool IsValid() {
        if (Title == null) return false;
        if (Title.Length > 25) return false;
        if (Title.Any(char.IsControl)) return false;
        return true;
    }

    public static bool UseAdvancedColors = true;

    public void ConvertKeys() {
        Vector3 UiColorToVector3(uint col) {
            var fb = (col >> 8) & 255;
            var fg = (col >> 16) & 255;
            var fr = (col >> 24) & 255;
            return new Vector3(fr / 255f, fg / 255f, fb / 255f);
        }

        Vector3? UiKeyToVector3(ushort key, bool glow) {
            if (key == 0) return null;
            var uiColor = PluginService.Data.GetExcelSheet<UIColor>()?.GetRow(key);
            if (uiColor == null) return null;
            return UiColorToVector3(glow ? uiColor.UIGlow : uiColor.UIForeground);
        }

        void Convert(ref ushort oldValue, ref Vector3? newValue) {
            if (oldValue != 0) {
                if (newValue == null) {
                    newValue = UiKeyToVector3(oldValue, false);
                    oldValue = 0;
                } else {
                    oldValue = 0;
                }
            }
        }
        Convert(ref ColorKey, ref Color);
        Convert(ref GlowKey, ref Glow);
    }
    
    
    public SeString ToSeString(bool includeQuotes = true, bool includeColor = true) {
        ConvertKeys();
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

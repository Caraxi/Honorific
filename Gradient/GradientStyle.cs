using System;
using Lumina.Text;

namespace Honorific.Gradient;

public class GradientStyle {
    public GradientAnimationStyle AnimationStyle;
    public string Name;
    public byte[,] Colours;
    
    
    public int? ColourSet;

    public GradientStyle(string name, string b64, GradientAnimationStyle animStyle) {
        Name = name;
        AnimationStyle = animStyle;
        Colours = Decode(b64);
        ColourSet = null;
    }

    public GradientStyle(string name, byte[,] colours, GradientAnimationStyle animStyle) {
        Name = name;
        AnimationStyle = animStyle;
        Colours = colours;
        ColourSet = null;
    }

    private static byte[,] Decode(string b64) {
        var arr = Convert.FromBase64String(b64);
        var arr2 = new byte[arr.Length / 3, 3];
        for (var i = 0; i < arr.Length; i += 3) {
            arr2[i / 3, 0] =  arr[i];
            arr2[i / 3, 1] =  arr[i + 1];
            arr2[i / 3, 2] =  arr[i + 2];
        }

        return arr2;
    }

    public void Apply(SeStringBuilder builder, string title, bool animate) {
        if (!animate) {
            ApplyStatic(builder, title);
            return;
        }
        
        switch(AnimationStyle) {
            case GradientAnimationStyle.Wave: ApplyWave(builder, title); break;
            case GradientAnimationStyle.Pulse: ApplyPulse(builder, title); break;
            default: ApplyStatic(builder, title); break;
        };
    }

    private void ApplyPulse(SeStringBuilder builder, string title) {
        var glow = GradientSystem.GetColourRGB(this, 0, 5);
        builder.PushEdgeColorRgba(glow.R, glow.G, glow.B, 255);
        builder.Append(title);
        builder.PopEdgeColor();
    }

    private void ApplyWave(SeStringBuilder builder, string title) {
        if (title.Length > 25) {
            for (var i = 0; i < title.Length; i+=2) {
                var glow = GradientSystem.GetColourRGB(this, i, 5);
                builder.PushEdgeColorRgba(glow.R, glow.G, glow.B, 255);
                builder.Append(title.Substring(i, Math.Min(2, title.Length - i)));
                builder.PopEdgeColor();
            }
        } else {
            var i = 0;
            foreach (var c in title) {
                var glow = GradientSystem.GetColourRGB(this, i++, 5);
                builder.PushEdgeColorRgba(glow.R, glow.G, glow.B, 255);
                builder.AppendChar(c);
                builder.PopEdgeColor();
            }
        }
    }

    private void ApplyStatic(SeStringBuilder builder, string title) {

        var gradientSize = Colours.GetLength(0);
        
        if (title.Length > 25) {
            for (var i = 0; i < title.Length; i+=2) {
                var z = (int)MathF.Round(i / (float)title.Length * gradientSize);
                var glow = GradientSystem.GetColourRGB(this, z, 5, false);
                builder.PushEdgeColorRgba(glow.R, glow.G, glow.B, 255);
                builder.Append(title.Substring(i, Math.Min(2, title.Length - i)));
                builder.PopEdgeColor();
            }
        } else {
            var i = 0;
            foreach (var c in title) {
                var glow = GradientSystem.GetColourRGB(this, (int)MathF.Round(i++ / (float)title.Length * gradientSize), 5, false);
                builder.PushEdgeColorRgba(glow.R, glow.G, glow.B, 255);
                builder.AppendChar(c);
                builder.PopEdgeColor();
            }
        }
        
        
        
    }
}
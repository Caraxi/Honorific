using System;
using Honorific.Gradient;

namespace Honorific;

public class CustomGradient {
    public string Name { get; set; } = "Custom Gradient";
    public string Base64Data { get; set; } = string.Empty;
    public Guid Id { get; set; } = Guid.NewGuid();

    public GradientStyle? ToGradientStyle(GradientAnimationStyle animationStyle) {
        if (string.IsNullOrEmpty(Base64Data)) return null;
        try {
            var animationSuffix = animationStyle switch {
                GradientAnimationStyle.Wave => " (Wave)",
                GradientAnimationStyle.Pulse => " (Pulse)",
                GradientAnimationStyle.Static => " (Static)",
                _ => ""
            };
            return new GradientStyle(Name + animationSuffix, Base64Data, animationStyle);
        } catch {
            return null;
        }
    }
}
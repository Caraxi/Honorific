using System;
using System.Collections.Generic;
using System.Linq;

namespace Honorific.Gradient;

public class GradientBuilderArgs {

    public string Name { get; init; } = "Generated Style";
    public int Steps { get; init; } = 64;

    public GradientAnimationStyle AnimationStyle { get; init; } = GradientAnimationStyle.Wave;
        
    public required List<GradientBuilder.FixedColour> FixedColours { get; init; }
    public IReadOnlyList<GradientBuilder.Pair> Pairs { get; private set; } = new List<GradientBuilder.Pair>();

    public int GradientMode { get; init; } = 0;

    public void UpdatePairs() {
        var pairs = new List<GradientBuilder.Pair>();

        var start = FixedColours.Find(f => f.Position == 0);

        if (start == null) {
            throw new Exception("No Start Colour");
        }

        if (FixedColours.Find(f => f.Position == ushort.MaxValue) == null) {
            throw new Exception("No End Colour");
        }
        
        var colours = FixedColours.OrderBy(f => f.Position).ToList();
        for (var i = 0; i < colours.Count - 1; i++) {
            var a = colours[i];
            var b = colours[i + 1];
            pairs.Add(new GradientBuilder.Pair(a, b));
        }

        Pairs = pairs;
    }
        

}
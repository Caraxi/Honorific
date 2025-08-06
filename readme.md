# Working on :

Implementing a class "Palette" that functions as such
- A Palette class has a list of Paints, an ID, and a name.
- A Paint has a type (static, gradient (gradient types : rgb, hsv, etc)), four colors : Color1, Color2, GlowColor1, GlowColor2, and a length.
- If the type is static, Color2 and GlowColor2 are not used.
- The colors are represented as Vector3 of R, G, B values
- The length is an integer representing how many characters the paint occupies. If it is null, it occupies the remaining space.
- Palettes can be imported and exported as a string in a specific format, as follows:
- The format is "PaletteName:ID:Paint1Type:Color1:Color2:GlowColor1:GlowColor2:Length:Paint2Type:(...)"

The PaletteManager takes in a string and a palette, and returns an SeString using the adequate ColorPayloads according to the given Palette.

# Issues

Glow colors are not implemented at the moment because they cause crashes, only text colors are implemented

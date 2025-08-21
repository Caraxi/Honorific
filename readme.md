# New feature : Palettes

Palettes can be assigned to titles from the Titles tab.

Palettes can be modified in the Palettes tab - palettes are a list of *Paints*, with each paint having a type, colors and lengths that modify their behavior.
A paint of length 0 covers the remainder of the title.

# Issues

Glow colors are not implemented at the moment because they cause crashes, only text colors are implemented
- This is most likely impossible to implement because glows have to be inside color payloads, otherwise they will not work - and many glow payloads overload and crash the game. Small custom titles work, but far too unstable to be implemented.

Cannot put palettes on default custom titles

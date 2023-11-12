using System.Collections.Generic;

namespace Honorific; 

public class CharacterConfig {
    public CustomTitle DefaultTitle = new();
    public CustomTitle Override = new();
    public List<CustomTitle> CustomTitles = new();
}

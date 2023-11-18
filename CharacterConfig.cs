using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace Honorific; 

public class CharacterConfig {
    public CustomTitle DefaultTitle = new();
    public CustomTitle Override = new();
    public List<CustomTitle> CustomTitles = new();

    public bool UseRandom = false;

    [JsonIgnore] public CustomTitle? ActiveTitle;

    public CustomTitle? GetTitleByUniqueId(string uniqueId) {
        return CustomTitles.FirstOrDefault(t => t.GetUniqueId(this) == uniqueId) ?? (DefaultTitle.GetUniqueId(this) == uniqueId ? DefaultTitle : null);
    }
}

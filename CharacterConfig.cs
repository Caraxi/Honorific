using System.Collections.Generic;
using System.Linq;

namespace Honorific; 

public class CharacterConfig {
    public CustomTitle DefaultTitle = new();
    public CustomTitle Override = new();
    public List<CustomTitle> CustomTitles = new();

    public CustomTitle? GetTitleByUniqueId(string uniqueId) {
        return CustomTitles.FirstOrDefault(t => t.GetUniqueId(this) == uniqueId) ?? (DefaultTitle.GetUniqueId(this) == uniqueId ? DefaultTitle : null);
    }
}

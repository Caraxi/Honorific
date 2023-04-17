using System.Linq;

namespace Honorific; 

public class CustomTitle {
    public string? Title = string.Empty;
    public bool IsPrefix;
    
    public bool Enabled;
    public TitleConditionType TitleCondition = TitleConditionType.None;
    public int ConditionParam0;

    public bool IsValid() {
        if (Title == null) return false;
        if (Title.Length > 25) return false;
        if (Title.Any(char.IsControl)) return false;
        return true;
    }
}

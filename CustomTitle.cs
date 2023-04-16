namespace Honorific; 

public class CustomTitle {
    public string? Title = string.Empty;
    public bool IsPrefix;
    
    public bool Enabled;
    public TitleConditionType TitleCondition = TitleConditionType.None;
    public int ConditionParam0;
}

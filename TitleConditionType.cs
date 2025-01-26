using System.ComponentModel;

namespace Honorific; 

public enum TitleConditionType {
    None,
    
    [Description("Class / Job")]
    ClassJob,
    
    [Description("Role")]
    JobRole,
    
    [Description("Gear Set")]
    GearSet,
    
    [Description("Original Title")]
    Title,
    
    [Description("Location")]
    Location,
}

using System.ComponentModel;

namespace Honorific; 

public enum TitleConditionType {
    None,
    
    [Description("Class / Job")]
    ClassJob,
    
    [Description("Role")]
    JobRole,
}

using System.ComponentModel;
using Lumina.Excel.GeneratedSheets;

namespace Honorific; 

public enum ClassJobRole {
    None = 0,
    Tank = 1,
    Healer = 2,
    DPS = 3,
    [Description("Crafter / Gatherer")]
    NonCombat = 4,
    
    [Description("Melee DPS")]
    MeleeDPS = 5,
    
    [Description("Ranged Physical DPS")]
    RangedPhysicalDPS = 6,
    
    [Description("Ranged Magical DPS")]
    RangedMagicalDPS = 7,
    
    Crafter = 8,
    Gatherer = 9,
}

public static class ClassJobRoleExtenstion {
    public static bool IsRole(this ClassJob? job, ClassJobRole role) {
        if (job == null || job.RowId == 0) return false;
        return role switch {
            ClassJobRole.None => false,
            ClassJobRole.Tank => job.Role == 1,
            ClassJobRole.Healer => job.Role == 4,
            ClassJobRole.DPS => job.Role is 2 or 3 or 5,
            ClassJobRole.NonCombat => job.Role == 0,
            ClassJobRole.MeleeDPS => job.Role == 2,
            ClassJobRole.RangedPhysicalDPS => job.LimitBreak1.Row == 4238,
            ClassJobRole.RangedMagicalDPS => job.LimitBreak1.Row == 203 || job.RowId == 36,
            ClassJobRole.Crafter => job.RowId is >= 8 and <= 15,
            ClassJobRole.Gatherer => job.RowId is 16 or 17 or 18,
            _ => false
        };
    }
}
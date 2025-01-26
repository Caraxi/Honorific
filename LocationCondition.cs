using Lumina.Excel.Sheets;

namespace Honorific;

public class LocationCondition {
    public uint TerritoryType;
    
    public bool ShouldSerializeWard() => PluginService.Data.GetExcelSheet<TerritoryType>().GetRowOrDefault(TerritoryType)?.TerritoryIntendedUse.RowId is 13 or 14;
    public bool ShouldSerializePlot() => Ward != null && PluginService.Data.GetExcelSheet<TerritoryType>().GetRowOrDefault(TerritoryType)?.TerritoryIntendedUse.RowId == 14;
    public bool ShouldSerializeRoom() => Plot != null && ShouldSerializePlot() && TerritoryType is 384 or 385 or 376 or 652 or 983 or 608 or 609 or 610 or 655 or 999; // Private Chambers & Apartments
    public bool IsApartment() => TerritoryType is 537 or 574 or 575 or 608 or 609 or 610 or 654 or 655 or 985 or 999;
    
    
    public int? Ward;
    public int? Plot;
    public int? Room;
}

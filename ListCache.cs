using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Utility;
using Lumina.Excel.Sheets;

namespace Honorific;

public static class ListCache {
    public static Lazy<List<(uint territoryTypeId, string name)>> TerritoryTypeNames = new(() => {
        return PluginService.Data.GetExcelSheet<TerritoryType>()
            .OrderBy(t => t.PlaceName.Value.Name.ExtractText().IsNullOrWhitespace() ? 1 : 0)
            .ThenBy(t => t.PlaceName.Value.Name.ExtractText())
            .Select(t => (t.RowId, t.PlaceName.Value.Name.ExtractText().IsNullOrWhitespace() ? $"TerritoryType#{t.RowId}" : t.PlaceName.Value.Name.ExtractText()))
            .ToList();
    });
}

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Honorific; 

public class CharacterConfig {
    public CustomTitle DefaultTitle = new();
    public CustomTitle Override = new();
    public List<CustomTitle> CustomTitles = new();

    public bool UseRandom = false;
    public bool RandomOnZoneChange = false;

    [JsonIgnore] public CustomTitle? ActiveTitle;

    public CustomTitle? GetTitleByUniqueId(string uniqueId) {
        return CustomTitles.FirstOrDefault(t => t.GetUniqueId(this) == uniqueId) ?? (DefaultTitle.GetUniqueId(this) == uniqueId ? DefaultTitle : null);
    }

    public bool TryGetTitleByUniqueId(string uniqueId, [NotNullWhen(true)] out CustomTitle? title) {
        title =  GetTitleByUniqueId(uniqueId);
        return title != null;
    }

    [JsonIgnore] public IEnumerable<CustomTitle> AllTitles => [..CustomTitles, DefaultTitle];
    
    public IReadOnlyList<CustomTitle> GetTitlesBySearchString(string searchString) {
        if (string.IsNullOrEmpty(searchString)) return [];
        if (TryGetTitleByUniqueId(searchString, out var unique)) return [unique];
        if (searchString.Equals("meta:all", StringComparison.InvariantCultureIgnoreCase)) return AllTitles.ToList();
        if (searchString.Equals("meta:default", StringComparison.InvariantCultureIgnoreCase)) return [DefaultTitle];
        if (searchString.StartsWith("regex:")) {
            try {
                var regex = new Regex(searchString[6..]);
                return AllTitles.Where(t => t.Title != null && regex.IsMatch(t.Title)).ToList();
            } catch (Exception ex) {
                PluginService.Chat.PrintError($"Error processing regex search. {ex.Message}", "Honorific");
                return [];
            }
        }

        return AllTitles.Where(t => t.Title?.Equals(searchString, StringComparison.InvariantCultureIgnoreCase) == true).ToList();
    }
    
}

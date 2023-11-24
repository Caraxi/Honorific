using System.Linq;
using System.Numerics;
using Dalamud.Interface.Colors;
using ImGuiNET;

namespace Honorific; 

public static class ImGuiExt {
    public static unsafe bool TriStateCheckbox(string label, out bool? setAll, params bool[] values) {
        setAll = null;
        var active = values.Count(v => v);
        var all = active == values.Length;
        var any = active > 0;
        
        if (ImGui.Checkbox(label, ref all)) {
            setAll = all;
            return true;
        }

        if (!all && any) {
            var dl = ImGui.GetWindowDrawList();
            var spacing = ImGui.GetStyle().FramePadding;
            dl.AddRectFilled(ImGui.GetItemRectMin() + spacing, ImGui.GetItemRectMax() - spacing, ImGui.GetColorU32(ImGuiCol.CheckMark));
        }
        
        return false;
    }
}

using System;
using System.Diagnostics;
using Dalamud.Plugin.Services;
using ImGuiNET;

namespace Honorific; 

public static class PerformanceMonitors {

    public class Monitor {
        public string Name { get; }
        public bool MultiCapture { get; }
        private Stopwatch sw = new Stopwatch();

        public Monitor(string name, bool multiCapture) {
            Name = name;
            MultiCapture = multiCapture;
        }

        public TimeSpan Last { get; private set; } = TimeSpan.Zero;
        public TimeSpan Max { get; private set; } = TimeSpan.Zero;
        public TimeSpan Average { get; private set; } = TimeSpan.Zero;
        public TimeSpan Total { get; private set; } = TimeSpan.Zero;

        private long updateCount = 0;
        
        public void Update() {
            updateCount++;
            Last = sw.Elapsed;

            Total += Last;
            if (Last > Max) Max = Last;
            Average = Total / updateCount;
            
            if (sw.IsRunning) {
                sw.Restart();
            } else {
                sw.Reset();
            }
        }

        public void Draw() {
            ImGui.TableNextColumn();
            ImGui.Text($"{Name}");
            ImGui.TableNextColumn();
            ImGui.Text($"{Average.TotalMilliseconds:F3}ms");
            ImGui.TableNextColumn();
            ImGui.Text($"{Max.TotalMilliseconds:F3}ms");
            ImGui.TableNextColumn();
            ImGui.Text($"{Last.TotalMilliseconds:F3}ms");
        }

        public MonitorRun Start() {
            return new MonitorRun(this, () => sw.Start(), () => {
                sw.Stop();
                if (!MultiCapture) Update();
            });
        }
    }

    public class MonitorRun : IDisposable {
        private readonly Action endAction;
        
        public MonitorRun(Monitor monitor, Action begin, Action end) {
            endAction = end;
            begin.Invoke();
        }

        public void Dispose() {
            endAction.Invoke();
        }
    }
    
    public static Monitor FrameProcessing { get; } = new("Per Frame Processing", true);
    public static Monitor PlateProcessing { get; } = new("Per Plate Processing", false);
    public static Monitor CleanupProcessing { get; } = new("Cleanup Processing per Frame", true);
    
    public static void LogFramePerformance(IFramework framework) {
        FrameProcessing.Update();
        CleanupProcessing.Update();
    }

    public static void DrawTable() {
        if (ImGui.BeginTable("performance", 4)) {
            
            ImGui.TableSetupColumn("Monitor");
            ImGui.TableSetupColumn("Average");
            ImGui.TableSetupColumn("Max");
            ImGui.TableSetupColumn("Last");
            ImGui.TableHeadersRow();

            FrameProcessing.Draw();
            PlateProcessing.Draw();
            CleanupProcessing.Draw();
            
            ImGui.EndTable();
        }
    }
    
}

using System;
using System.Linq;
using System.Numerics;
using Dalamud.Interface.Colors;
using Dalamud.Interface.ImGuiSeStringRenderer;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;
using Honorific.Gradient;

namespace Honorific;

public class CustomGradientEditorWindow : Window {
    private readonly PluginConfig config;
    private readonly Plugin plugin;
    private CustomGradient? editingGradient;
    private string gradientName = "Custom Gradient";
    private bool isNewGradient = true;

    public CustomGradientEditorWindow(Plugin plugin, PluginConfig config)
        : base("Custom Gradient Editor##HonorificGradientEditor", ImGuiWindowFlags.None) {
        this.plugin = plugin;
        this.config = config;

        SizeConstraints = new WindowSizeConstraints {
            MinimumSize = new Vector2(800, 600),
            MaximumSize = new Vector2(4000, 4000)
        };
    }

    public void OpenForNew() {
        IsOpen = true;
        isNewGradient = true;
        editingGradient = null;
        gradientName = "Custom Gradient";
        GradientBuilder.FixedColours.Clear();
        GradientBuilder.FixedColours.Add(new GradientBuilder.FixedColour(0, 0xFF000000));
        GradientBuilder.Editing = Guid.Empty;
        GradientBuilder.Mode = 0;
        GradientBuilder.AnimationStyle = GradientAnimationStyle.Wave;
        GradientBuilder.PreviewText = "Preview Title";
        GradientBuilder.PreviewTextColour = Vector3.Zero;
        GradientBuilder.GenerateStyle();
    }

    public void OpenForEdit(CustomGradient gradient) {
        IsOpen = true;
        isNewGradient = false;
        editingGradient = gradient;
        gradientName = gradient.Name;

        // Load the gradient into the builder
        try {
            var style = new GradientStyle("Import", gradient.Base64Data, GradientAnimationStyle.Wave);
            GradientBuilder.FixedColours.Clear();
            GradientBuilder.Length = style.Colours.GetLength(0);
            for (var i = 0; i < GradientBuilder.Length; i++) {
                var abgr = ((uint)0xFF << 24) | ((uint)style.Colours[i, 2] << 16) | ((uint)style.Colours[i, 1] << 8) | style.Colours[i, 0];
                GradientBuilder.FixedColours.Add(new GradientBuilder.FixedColour((ushort)MathF.Round(i / (float)GradientBuilder.Length * ushort.MaxValue), abgr));
            }
            GradientBuilder.UpdatePairs();
            GradientBuilder.GenerateStyle();
        } catch {
            // If loading fails, start fresh
            OpenForNew();
        }
    }

    public override void Draw() {
        using var _ = ImRaii.PushId("CustomGradientEditor");

        // Header with name input and save button
        ImGui.SetNextItemWidth(300);
        ImGui.InputText("Gradient Name", ref gradientName, 64);

        ImGui.SameLine();

        if (ImGui.Button(isNewGradient ? "Save New Gradient" : "Update Gradient")) {
            SaveGradient();
        }

        if (!isNewGradient && editingGradient != null) {
            ImGui.SameLine();
            if (ImGui.Button("Delete Gradient")) {
                config.CustomGradients.Remove(editingGradient);
                PluginService.PluginInterface.SavePluginConfig(config);
                IsOpen = false;
            }
        }

        ImGui.SameLine();
        if (ImGui.Button("Cancel")) {
            IsOpen = false;
        }

        ImGui.Separator();

        // Draw the gradient builder
        GradientSystem.DrawGradientBuilder();
    }

    private void SaveGradient() {
        if (string.IsNullOrWhiteSpace(gradientName)) {
            gradientName = "Custom Gradient";
        }

        if (GradientBuilder.GeneratedStyle == null) {
            GradientBuilder.GenerateStyle();
        }

        if (GradientBuilder.GeneratedStyle == null) {
            return;
        }

        // Convert the generated style to Base64
        var bytes = GradientBuilder.GeneratedStyle.Colours.Cast<byte>().ToArray();
        var b64 = Convert.ToBase64String(bytes);

        if (isNewGradient) {
            var newGradient = new CustomGradient {
                Name = gradientName,
                Base64Data = b64,
                Id = Guid.NewGuid()
            };
            config.CustomGradients.Add(newGradient);
        } else if (editingGradient != null) {
            editingGradient.Name = gradientName;
            editingGradient.Base64Data = b64;
        }

        PluginService.PluginInterface.SavePluginConfig(config);
        IsOpen = false;
    }
}
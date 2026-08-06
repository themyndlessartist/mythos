using Godot;
using Mythos.Content;

namespace Mythos.GodotIntegration;

/// <summary>
/// Minimal engine entry point for foundation prototype validation.
/// </summary>
public partial class PrototypeRoot : Node
{
    public override void _Ready()
    {
        var bundlePath = ProjectSettings.GlobalizePath("res://Content/genesis-lakewood.bundle.json");
        var bundleBytes = File.ReadAllBytes(bundlePath);
        var import = new ContentBundleImporter().Import(bundleBytes);
        if (!import.IsSuccess)
        {
            GD.PushError($"Genesis content import failed: {import.Error!.Code} - {import.Error.Message}");
            GetTree().Quit(1);
            return;
        }

        var bundle = import.Value!;
        var khaige = new CharacterAuthoringRecordReader().Read(bundle, "mythos-genesis.khaige");
        if (!khaige.IsSuccess)
        {
            GD.PushError($"Genesis character import failed: {khaige.Error!.Code} - {khaige.Error.Message}");
            GetTree().Quit(1);
            return;
        }

        GD.Print($"{Framework.FrameworkAssembly.Name} ready with content package '{bundle.PackageId}' and character '{khaige.Value!.DisplayName}'.");
    }
}

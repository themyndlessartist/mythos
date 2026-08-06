using Godot;
using Mythos.Content;
using Mythos.Framework.Entities;
using Mythos.Genesis;

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

        var entities = new EntityRegistry();
        var runtimeCharacter = GenesisCharacterBootstrap.Create(khaige.Value!, entities, 0);
        if (!runtimeCharacter.IsSuccess)
        {
            GD.PushError($"Genesis character bootstrap failed: {runtimeCharacter.ErrorCode} - {runtimeCharacter.ErrorMessage}");
            GetTree().Quit(1);
            return;
        }

        var project = new LakewoodProjectDefinitionReader().Read(bundle, "mythos-genesis.storehouse");
        if (!project.IsSuccess)
        {
            GD.PushError($"Genesis project import failed: {project.Error!.Code} - {project.Error.Message}");
            GetTree().Quit(1);
            return;
        }

        var lakewood = new LakewoodPrototypeView();
        AddChild(lakewood);
        if (!lakewood.Initialize(bundle, project.Value!, out var viewError))
        {
            GD.PushError($"Lakewood view failed: {viewError}");
            GetTree().Quit(1);
            return;
        }

        GD.Print($"{Framework.FrameworkAssembly.Name} ready with content package '{bundle.PackageId}' and runtime character '{runtimeCharacter.Character!.Identity}'.");
    }
}

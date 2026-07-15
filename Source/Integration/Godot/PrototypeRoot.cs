using Godot;

namespace Mythos.GodotIntegration;

/// <summary>
/// Minimal engine entry point for foundation prototype validation.
/// </summary>
public partial class PrototypeRoot : Node
{
    public override void _Ready()
    {
        GD.Print($"{Framework.FrameworkAssembly.Name} foundation prototype ready.");
    }
}

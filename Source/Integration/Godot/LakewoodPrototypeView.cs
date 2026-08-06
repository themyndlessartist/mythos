using System.Text.Json;
using Godot;
using Mythos.Content;
using Mythos.Genesis;

namespace Mythos.GodotIntegration;

internal partial class LakewoodPrototypeView : Control
{
    private const string Timber = "mythos-genesis.timber";
    private const string Stone = "mythos-genesis.stone";
    private const string SavePath = "user://lakewood-prototype-save.json";
    private static readonly JsonSerializerOptions SaveOptions = new() { WriteIndented = true };

    private LakewoodSettlementState state = null!;
    private Label status = null!;
    private TextureRect completedStorehouse = null!;
    private Button buildButton = null!;
    private int npcStep;

    internal bool Initialize(ImportedContentBundle bundle, LakewoodProjectDefinition project, out string error)
    {
        ArgumentNullException.ThrowIfNull(bundle);
        state = new LakewoodSettlementState(project);
        error = string.Empty;

        if (!TryTexture(bundle, "assets/maps/lakewood-background-prototype-v1.png", out var background, out error) ||
            !TryTexture(bundle, "assets/characters/khaige-prototype-v1.png", out var khaige, out error) ||
            !TryTexture(bundle, "assets/characters/lakewood-worker-prototype-v1.png", out var worker, out error) ||
            !TryTexture(bundle, "assets/buildings/storehouse-complete-prototype-v1.png", out var storehouse, out error))
            return false;

        Name = "LakewoodPrototype";
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddBackground(background!);
        AddCharacter(khaige!);
        AddNpcMarkers(worker!);
        AddStorehouse(storehouse!);
        AddHud();
        RestoreIfAvailable();
        Refresh();
        StartNpcAssistance();
        return true;
    }

    public override void _ExitTree() => Save();

    private void AddBackground(Texture2D texture)
    {
        var background = new TextureRect
        {
            Name = "LakewoodBackground",
            Texture = texture,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(background);
    }

    private void AddCharacter(Texture2D texture)
    {
        var character = new TextureRect
        {
            Name = "Khaige",
            Texture = texture,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            Position = new Vector2(555, 280),
            Size = new Vector2(96, 128),
            MouseFilter = MouseFilterEnum.Ignore,
            TooltipText = "Khaige (non-canon prototype visual)",
        };
        AddChild(character);
    }

    private void AddNpcMarkers(Texture2D texture)
    {
        AddNpc(texture, "Alden - Woodcutter", new(1010, 370), new Color("d3e4bf"));
        AddNpc(texture, "Mara - Quarry worker", new(980, 100), new Color("d5d9df"));
        AddNpc(texture, "Tovin - Builder", new(420, 500), new Color("e1c18d"));
        AddNpc(texture, "Elia - Fisher", new(185, 265), new Color("b8d9e7"));
    }

    private void AddNpc(Texture2D texture, string role, Vector2 position, Color color)
    {
        var figure = new TextureRect
        {
            Texture = texture,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            Position = position,
            Size = new Vector2(68, 92),
            Modulate = color,
            TooltipText = $"Provisional {role}",
            MouseFilter = MouseFilterEnum.Stop,
        };
        AddChild(figure);
    }

    private void AddStorehouse(Texture2D texture)
    {
        completedStorehouse = new TextureRect
        {
            Name = "CompletedStorehouse",
            Texture = texture,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            Position = new Vector2(330, 430),
            Size = new Vector2(250, 220),
            MouseFilter = MouseFilterEnum.Ignore,
            Visible = false,
        };
        AddChild(completedStorehouse);
    }

    private void AddHud()
    {
        var panel = new PanelContainer { Name = "SettlementPanel" };
        panel.SetAnchor(Side.Left, 1);
        panel.SetAnchor(Side.Right, 1);
        panel.OffsetLeft = -340;
        panel.OffsetRight = -16;
        panel.OffsetTop = 16;
        panel.OffsetBottom = 370;
        panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0.075f, 0.09f, 0.08f, 0.94f),
            BorderColor = new Color("9a815c"),
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = 6,
            CornerRadiusTopRight = 6,
            CornerRadiusBottomLeft = 6,
            CornerRadiusBottomRight = 6,
            ContentMarginLeft = 18,
            ContentMarginTop = 16,
            ContentMarginRight = 18,
            ContentMarginBottom = 16,
        });
        var content = new VBoxContainer();
        content.AddThemeConstantOverride("separation", 10);
        var title = new Label { Text = "LAKEWOOD" };
        title.AddThemeFontSizeOverride("font_size", 22);
        content.AddChild(title);
        var projectTitle = new Label { Text = state.Project.DisplayName };
        projectTitle.AddThemeColorOverride("font_color", new Color("d8c18d"));
        content.AddChild(projectTitle);
        status = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(280, 72) };
        content.AddChild(status);

        var resources = new HBoxContainer();
        resources.AddThemeConstantOverride("separation", 8);
        resources.AddChild(Command("+ Timber", () => Contribute(Timber, 5)));
        resources.AddChild(Command("+ Stone", () => Contribute(Stone, 5)));
        resources.AddChild(Command("+ Labor", () => ContributeLabor(2)));
        content.AddChild(resources);

        buildButton = Command("Complete Storehouse", Complete);
        content.AddChild(buildButton);
        content.AddChild(Command("Save Progress", Save));
        content.AddChild(Command("Reset Prototype", Reset));
        var assistance = new Label { Text = "NPC crews contribute automatically." };
        assistance.AddThemeColorOverride("font_color", new Color("aeb8ad"));
        content.AddChild(assistance);
        panel.AddChild(content);
        AddChild(panel);
    }

    private static Button Command(string text, Action action)
    {
        var button = new Button { Text = text, CustomMinimumSize = new Vector2(0, 34) };
        button.Pressed += action;
        return button;
    }

    private void Contribute(string resourceId, int amount)
    {
        state.ContributeResource(resourceId, amount);
        Refresh();
    }

    private void ContributeLabor(int amount)
    {
        state.ContributeLabor(amount);
        Refresh();
    }

    private void Complete()
    {
        state.TryComplete();
        Save();
        Refresh();
    }

    private void StartNpcAssistance()
    {
        var timer = new Godot.Timer { WaitTime = 2.0, Autostart = true };
        timer.Timeout += () =>
        {
            if (state.ProjectComplete) return;
            switch (npcStep++ % 3)
            {
                case 0: state.ContributeResource(Timber, 2); break;
                case 1: state.ContributeResource(Stone, 1); break;
                default: state.ContributeLabor(1); break;
            }
            Refresh();
        };
        AddChild(timer);
    }

    private void Refresh()
    {
        var timber = state.Stockpile.GetValueOrDefault(Timber);
        var stone = state.Stockpile.GetValueOrDefault(Stone);
        status.Text = state.ProjectComplete
            ? "Storehouse complete\nStorage capacity expanded"
            : $"Timber  {timber} / 20\nStone    {stone} / 10\nLabor     {state.LaborContributed} / 8";
        completedStorehouse.Visible = state.ProjectComplete;
        buildButton.Disabled = state.ProjectComplete || timber < 20 || stone < 10 || state.LaborContributed < 8;
    }

    private void Save()
    {
        if (state is null) return;
        var path = ProjectSettings.GlobalizePath(SavePath);
        File.WriteAllText(path, JsonSerializer.Serialize(state.ExportSnapshot(), SaveOptions));
    }

    private void Reset()
    {
        state = new LakewoodSettlementState(state.Project);
        Save();
        Refresh();
    }

    private void RestoreIfAvailable()
    {
        var path = ProjectSettings.GlobalizePath(SavePath);
        if (!File.Exists(path)) return;
        try
        {
            state.Restore(JsonSerializer.Deserialize<LakewoodSettlementSnapshot>(File.ReadAllText(path), SaveOptions));
        }
        catch (JsonException exception)
        {
            GD.PushWarning($"Lakewood prototype save ignored: {exception.Message}");
        }
    }

    private static bool TryTexture(
        ImportedContentBundle bundle,
        string path,
        out Texture2D? texture,
        out string error)
    {
        texture = null;
        error = string.Empty;
        if (!bundle.Files.TryGetValue(path, out var file))
        {
            error = $"Missing image '{path}'.";
            return false;
        }
        var image = new Image();
        var load = image.LoadPngFromBuffer(file.Bytes.ToArray());
        if (load != Error.Ok)
        {
            error = $"Could not decode '{path}': {load}.";
            return false;
        }
        texture = ImageTexture.CreateFromImage(image);
        return true;
    }
}

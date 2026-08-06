using System.Text.Json;
using Mythos.Content;

namespace Mythos.Genesis;

public sealed record LakewoodResourceRequirement(string ResourceId, int Amount);

public sealed record LakewoodProjectDefinition(
    string ProjectId,
    string DisplayName,
    string SiteMarkerId,
    IReadOnlyList<LakewoodResourceRequirement> ResourceRequirements,
    int LaborRequired,
    string CompletionAssetId,
    string CompletionStateId);

public readonly record struct LakewoodProjectResult(LakewoodProjectDefinition? Value, ContentImportError? Error)
{
    public bool IsSuccess => Error is null;
    public static LakewoodProjectResult Success(LakewoodProjectDefinition value) => new(value, null);
    public static LakewoodProjectResult Failure(string code, string message, string? path = null) =>
        new(null, new ContentImportError(code, message, path));
}

public sealed class LakewoodProjectDefinitionReader
{
    public LakewoodProjectResult Read(ImportedContentBundle bundle, string projectId)
    {
        ArgumentNullException.ThrowIfNull(bundle);
        if (!TryFindEntryPath(bundle.PackageManifest.Bytes.Span, projectId, out var path) ||
            !bundle.Files.TryGetValue(path, out var file))
        {
            return LakewoodProjectResult.Failure(ContentImportErrorCodes.RecordNotFound, $"Project '{projectId}' was not found.");
        }

        try
        {
            using var document = JsonDocument.Parse(file.Bytes);
            var root = document.RootElement;
            if (!Exact(root, "document_kind", "mythos.settlement-project-authoring") ||
                !Exact(root, "schema_version", "1.0") ||
                !Required(root, "project_record_id", out var id) || id != projectId ||
                !Required(root, "display_name", out var displayName) ||
                !Required(root, "site_marker_id", out var siteMarkerId) ||
                !Required(root, "completion_state_id", out var completionStateId) ||
                !root.TryGetProperty("labor_required", out var labor) || !labor.TryGetInt32(out var laborRequired) || laborRequired <= 0 ||
                !root.TryGetProperty("completion_asset", out var completionAsset) ||
                !Required(completionAsset, "record_id", out var completionAssetId) ||
                !root.TryGetProperty("resource_requirements", out var requirementsElement) ||
                requirementsElement.ValueKind != JsonValueKind.Array)
            {
                return Invalid(path);
            }

            var requirements = new List<LakewoodResourceRequirement>();
            foreach (var item in requirementsElement.EnumerateArray())
            {
                if (!Required(item, "resource_id", out var resourceId) ||
                    !item.TryGetProperty("amount", out var amountElement) || !amountElement.TryGetInt32(out var amount) || amount <= 0 ||
                    requirements.Any(existing => existing.ResourceId == resourceId))
                {
                    return Invalid(path);
                }
                requirements.Add(new(resourceId, amount));
            }
            if (requirements.Count == 0) return Invalid(path);

            return LakewoodProjectResult.Success(new(
                id, displayName, siteMarkerId, requirements.AsReadOnly(), laborRequired, completionAssetId, completionStateId));
        }
        catch (JsonException exception)
        {
            return LakewoodProjectResult.Failure(ContentImportErrorCodes.InvalidRecord, exception.Message, path);
        }
    }

    private static bool TryFindEntryPath(ReadOnlySpan<byte> manifestBytes, string projectId, out string path)
    {
        path = string.Empty;
        using var document = JsonDocument.Parse(manifestBytes.ToArray());
        foreach (var entry in document.RootElement.GetProperty("entries").EnumerateArray())
        {
            if (Exact(entry, "kind", "settlement-project") && Exact(entry, "id", projectId) && Required(entry, "path", out path))
                return true;
        }
        return false;
    }

    private static LakewoodProjectResult Invalid(string path) =>
        LakewoodProjectResult.Failure(ContentImportErrorCodes.InvalidRecord, "Settlement project record is malformed.", path);
    private static bool Exact(JsonElement element, string name, string expected) =>
        Required(element, name, out var value) && value == expected;
    private static bool Required(JsonElement element, string name, out string value)
    {
        value = string.Empty;
        if (!element.TryGetProperty(name, out var property) || property.ValueKind != JsonValueKind.String) return false;
        value = property.GetString() ?? string.Empty;
        return value.Length > 0 && value == value.Trim();
    }
}

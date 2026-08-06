using System.Text.Json;
using System.Text.RegularExpressions;

namespace Mythos.Content;

public sealed record ImportedCharacterAuthoringRecord(
    string RecordId,
    string DisplayName,
    string? Notes,
    IReadOnlyList<string> Tags);

public readonly record struct CharacterContentResult(ImportedCharacterAuthoringRecord? Value, ContentImportError? Error)
{
    public bool IsSuccess => Error is null;

    public static CharacterContentResult Success(ImportedCharacterAuthoringRecord value) => new(value, null);

    public static CharacterContentResult Failure(string code, string message, string? path = null) =>
        new(null, new ContentImportError(code, message, path));
}

/// <summary>Reads validated DATA-005 Character records without creating runtime entities.</summary>
public sealed class CharacterAuthoringRecordReader
{
    private static readonly Regex AuthoringIdPattern = new(
        "^[a-z][a-z0-9-]*(\\.[a-z][a-z0-9-]*)+$",
        RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);

    public CharacterContentResult Read(ImportedContentBundle bundle, string recordId)
    {
        ArgumentNullException.ThrowIfNull(bundle);
        if (!AuthoringIdPattern.IsMatch(recordId))
        {
            return CharacterContentResult.Failure(
                ContentImportErrorCodes.InvalidRecord,
                "Character record ID is malformed.");
        }

        if (!TryFindEntryPath(bundle.PackageManifest.Bytes.Span, recordId, out var path) ||
            !bundle.Files.TryGetValue(path, out var file))
        {
            return CharacterContentResult.Failure(
                ContentImportErrorCodes.RecordNotFound,
                $"Character record '{recordId}' was not found.");
        }

        return ParseRecord(file.Bytes.Span, recordId, path);
    }

    private static CharacterContentResult ParseRecord(ReadOnlySpan<byte> bytes, string expectedId, string path)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(bytes.ToArray());
        }
        catch (JsonException exception)
        {
            return CharacterContentResult.Failure(ContentImportErrorCodes.InvalidRecord, exception.Message, path);
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                !HasExactString(root, "document_kind", "mythos.character-authoring") ||
                !HasExactString(root, "schema_version", "1.0") ||
                !TryRequiredString(root, "character_record_id", out var id) ||
                !string.Equals(id, expectedId, StringComparison.Ordinal) ||
                !TryRequiredString(root, "display_name", out var displayName) ||
                displayName != displayName.Trim() ||
                !IsSafePlainText(displayName))
            {
                return CharacterContentResult.Failure(
                    ContentImportErrorCodes.InvalidRecord,
                    "Character record identity or display name is invalid.",
                    path);
            }

            var tags = new List<string>();
            if (root.TryGetProperty("tags", out var tagsElement))
            {
                if (tagsElement.ValueKind != JsonValueKind.Array)
                {
                    return InvalidField(path, "Character tags must be an array.");
                }

                foreach (var tagElement in tagsElement.EnumerateArray())
                {
                    var tag = tagElement.ValueKind == JsonValueKind.String ? tagElement.GetString() : null;
                    if (tag is null || !AuthoringIdPattern.IsMatch(tag) ||
                        (tags.Count > 0 && string.CompareOrdinal(tags[^1], tag) >= 0))
                    {
                        return InvalidField(path, "Character tags must be sorted, unique namespaced IDs.");
                    }

                    tags.Add(tag);
                }
            }

            string? notes = null;
            if (root.TryGetProperty("notes", out var notesElement))
            {
                notes = notesElement.ValueKind == JsonValueKind.String ? notesElement.GetString() : null;
                if (notes is null || !IsSafePlainText(notes))
                {
                    return InvalidField(path, "Character notes must be safe plain text.");
                }
            }

            if (root.TryGetProperty("extensions", out var extensions) &&
                (extensions.ValueKind != JsonValueKind.Object ||
                 extensions.EnumerateObject().Any(property => !AuthoringIdPattern.IsMatch(property.Name))))
            {
                return InvalidField(path, "Character extension keys must be namespaced IDs.");
            }

            if (root.TryGetProperty("visual", out var visual) && !ValidateVisual(visual))
            {
                return InvalidField(path, "Character visual reference is malformed.");
            }

            return CharacterContentResult.Success(
                new ImportedCharacterAuthoringRecord(id, displayName, notes, tags.AsReadOnly()));
        }
    }

    private static bool TryFindEntryPath(ReadOnlySpan<byte> manifestBytes, string recordId, out string path)
    {
        path = string.Empty;
        using var document = JsonDocument.Parse(manifestBytes.ToArray());
        foreach (var entry in document.RootElement.GetProperty("entries").EnumerateArray())
        {
            if (HasExactString(entry, "kind", "character") && HasExactString(entry, "id", recordId) &&
                TryRequiredString(entry, "path", out path))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ValidateVisual(JsonElement visual)
    {
        if (visual.ValueKind != JsonValueKind.Object ||
            !visual.TryGetProperty("sprite_manifest", out var reference) || reference.ValueKind != JsonValueKind.Object ||
            !TryRequiredString(reference, "package_id", out var packageId) || !AuthoringIdPattern.IsMatch(packageId) ||
            !TryRequiredString(reference, "record_id", out var recordId) || !AuthoringIdPattern.IsMatch(recordId) ||
            !visual.TryGetProperty("options", out var options) || options.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        return options.EnumerateObject().All(option =>
            AuthoringIdPattern.IsMatch(option.Name) &&
            option.Value.ValueKind == JsonValueKind.String &&
            AuthoringIdPattern.IsMatch(option.Value.GetString() ?? string.Empty));
    }

    private static bool IsSafePlainText(string value) =>
        !value.Contains('<') && !value.Contains('>') &&
        value.All(character => character is '\t' or '\n' or '\r' || !char.IsControl(character));

    private static CharacterContentResult InvalidField(string path, string message) =>
        CharacterContentResult.Failure(ContentImportErrorCodes.InvalidRecord, message, path);

    private static bool HasExactString(JsonElement element, string name, string expected) =>
        TryRequiredString(element, name, out var value) && string.Equals(value, expected, StringComparison.Ordinal);

    private static bool TryRequiredString(JsonElement element, string name, out string value)
    {
        value = string.Empty;
        if (!element.TryGetProperty(name, out var property) || property.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        value = property.GetString() ?? string.Empty;
        return value.Length > 0;
    }
}

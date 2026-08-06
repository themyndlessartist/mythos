using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Mythos.Content;

public sealed record ContentImportLimits(
    int MaximumFiles = 4096,
    int MaximumFileBytes = 64 * 1024 * 1024,
    long MaximumBundleBytes = 512L * 1024 * 1024);

public sealed class ContentBundleImporter
{
    private static readonly HashSet<string> Schema10EntryKinds =
        ["npc", "sprite-animation", "layered-map", "asset"];

    private static readonly HashSet<string> Schema11EntryKinds =
        [.. Schema10EntryKinds, "character"];

    private static readonly HashSet<string> Schema12EntryKinds =
        [.. Schema11EntryKinds, "settlement-project"];

    private static readonly Regex PackageIdPattern = new(
        "^[a-z][a-z0-9-]*(\\.[a-z][a-z0-9-]*)+$",
        RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);

    private readonly ContentImportLimits limits;

    public ContentBundleImporter(ContentImportLimits? limits = null)
    {
        this.limits = limits ?? new ContentImportLimits();
    }

    public ContentImportResult Import(ReadOnlySpan<byte> bundleBytes)
    {
        if (bundleBytes.Length > limits.MaximumBundleBytes)
        {
            return ContentImportResult.Failure(ContentImportErrorCodes.LimitExceeded, "Bundle exceeds the configured byte limit.");
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(bundleBytes.ToArray());
        }
        catch (JsonException exception)
        {
            return ContentImportResult.Failure(ContentImportErrorCodes.MalformedBundle, exception.Message);
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                !HasExactString(root, "bundle_kind", "mythos.content-bundle") ||
                !HasExactString(root, "bundle_version", "1.0"))
            {
                return ContentImportResult.Failure(ContentImportErrorCodes.UnsupportedBundle, "Bundle kind or version is unsupported.");
            }

            if (!TryRequiredString(root, "package_id", out var packageId) || !PackageIdPattern.IsMatch(packageId))
            {
                return ContentImportResult.Failure(ContentImportErrorCodes.InvalidPackageId, "Package ID is missing or malformed.");
            }

            if (!root.TryGetProperty("files", out var filesElement) || filesElement.ValueKind != JsonValueKind.Array)
            {
                return ContentImportResult.Failure(ContentImportErrorCodes.MalformedBundle, "Bundle files must be an array.");
            }

            if (filesElement.GetArrayLength() > limits.MaximumFiles)
            {
                return ContentImportResult.Failure(ContentImportErrorCodes.LimitExceeded, "Bundle contains too many files.");
            }

            var files = new Dictionary<string, ImportedContentFile>(StringComparer.Ordinal);
            var foldedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var fileElement in filesElement.EnumerateArray())
            {
                var parsed = ParseFile(fileElement);
                if (!parsed.IsSuccess)
                {
                    return parsed.ErrorResult;
                }

                var file = parsed.File!;
                if (!files.TryAdd(file.Path, file) || !foldedPaths.Add(file.Path))
                {
                    return ContentImportResult.Failure(
                        ContentImportErrorCodes.DuplicatePath,
                        "Bundle paths must be unique without case collisions.",
                        file.Path);
                }
            }

            if (!files.TryGetValue("package.json", out var manifest))
            {
                return ContentImportResult.Failure(ContentImportErrorCodes.MissingManifest, "Bundle does not contain package.json.");
            }

            var inventoryValidation = ValidateManifest(packageId, manifest.Bytes.Span, files);
            return inventoryValidation ?? ContentImportResult.Success(new ImportedContentBundle(packageId, files));
        }
    }

    private ParsedFile ParseFile(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object ||
            !TryRequiredString(element, "path", out var path) ||
            !TryRequiredString(element, "media_type", out var mediaType) ||
            !TryRequiredString(element, "content_base64", out var encoded) ||
            !element.TryGetProperty("size", out var sizeElement) || !sizeElement.TryGetInt32(out var declaredSize) || declaredSize < 0 ||
            !element.TryGetProperty("integrity", out var integrity) || integrity.ValueKind != JsonValueKind.Object ||
            !HasExactString(integrity, "algorithm", "sha256") ||
            !TryRequiredString(integrity, "digest", out var digest))
        {
            return ParsedFile.Failure(ContentImportErrorCodes.MalformedBundle, "Bundle file metadata is malformed.");
        }

        if (!IsSafePath(path))
        {
            return ParsedFile.Failure(ContentImportErrorCodes.InvalidPath, "Bundle file path is unsafe.", path);
        }

        if (declaredSize > limits.MaximumFileBytes)
        {
            return ParsedFile.Failure(ContentImportErrorCodes.LimitExceeded, "Bundle file exceeds the configured byte limit.", path);
        }

        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(encoded);
        }
        catch (FormatException)
        {
            return ParsedFile.Failure(ContentImportErrorCodes.InvalidEncoding, "Bundle file is not valid base64.", path);
        }

        if (bytes.Length != declaredSize)
        {
            return ParsedFile.Failure(ContentImportErrorCodes.SizeMismatch, "Decoded file size does not match its declaration.", path);
        }

        var actualDigest = Convert.ToHexStringLower(SHA256.HashData(bytes));
        if (!string.Equals(actualDigest, digest, StringComparison.Ordinal))
        {
            return ParsedFile.Failure(ContentImportErrorCodes.IntegrityMismatch, "File SHA-256 digest does not match.", path);
        }

        return ParsedFile.Success(new ImportedContentFile(path, mediaType, bytes, actualDigest));
    }

    private static ContentImportResult? ValidateManifest(
        string packageId,
        ReadOnlySpan<byte> manifestBytes,
        IReadOnlyDictionary<string, ImportedContentFile> files)
    {
        JsonDocument manifestDocument;
        try
        {
            manifestDocument = JsonDocument.Parse(manifestBytes.ToArray());
        }
        catch (JsonException exception)
        {
            return ContentImportResult.Failure(ContentImportErrorCodes.MalformedBundle, exception.Message, "package.json");
        }

        using (manifestDocument)
        {
            var root = manifestDocument.RootElement;
            if (!HasExactString(root, "document_kind", "mythos.content-package") ||
                !TryRequiredString(root, "schema_version", out var schemaVersion) ||
                (schemaVersion != "1.0" && schemaVersion != "1.1" && schemaVersion != "1.2") ||
                !TryRequiredString(root, "package_id", out var manifestPackageId) ||
                !string.Equals(packageId, manifestPackageId, StringComparison.Ordinal) ||
                !root.TryGetProperty("entries", out var entries) || entries.ValueKind != JsonValueKind.Array)
            {
                return ContentImportResult.Failure(ContentImportErrorCodes.InventoryMismatch, "Package manifest identity or inventory is invalid.", "package.json");
            }

            var declaredPaths = new HashSet<string>(StringComparer.Ordinal);
            var declaredIds = new HashSet<string>(StringComparer.Ordinal);
            var supportedEntryKinds = schemaVersion switch
            {
                "1.2" => Schema12EntryKinds,
                "1.1" => Schema11EntryKinds,
                _ => Schema10EntryKinds,
            };
            foreach (var entry in entries.EnumerateArray())
            {
                if (!TryRequiredString(entry, "kind", out var kind) ||
                    !supportedEntryKinds.Contains(kind) ||
                    !TryRequiredString(entry, "id", out var id) ||
                    !PackageIdPattern.IsMatch(id) ||
                    !declaredIds.Add(id) ||
                    !TryRequiredString(entry, "path", out var path) ||
                    !IsSafePath(path) ||
                    !TryRequiredString(entry, "media_type", out var mediaType) ||
                    !entry.TryGetProperty("size", out var sizeElement) || !sizeElement.TryGetInt32(out var size) ||
                    !entry.TryGetProperty("integrity", out var integrity) || integrity.ValueKind != JsonValueKind.Object ||
                    !HasExactString(integrity, "algorithm", "sha256") ||
                    !TryRequiredString(integrity, "digest", out var digest) ||
                    !files.TryGetValue(path, out var file) ||
                    file.MediaType != mediaType || file.Bytes.Length != size || file.Sha256Digest != digest ||
                    !declaredPaths.Add(path))
                {
                    return ContentImportResult.Failure(ContentImportErrorCodes.InventoryMismatch, "Package inventory does not match bundle files.", "package.json");
                }
            }

            var bundledContentPaths = files.Keys.Where(path => path != "package.json").ToHashSet(StringComparer.Ordinal);
            if (!declaredPaths.SetEquals(bundledContentPaths))
            {
                return ContentImportResult.Failure(ContentImportErrorCodes.InventoryMismatch, "Bundle contains undeclared or missing content files.", "package.json");
            }
        }

        return null;
    }

    private static bool IsSafePath(string path) =>
        !string.IsNullOrWhiteSpace(path) &&
        path == path.Trim() &&
        !path.StartsWith('/') &&
        !path.Contains('\\') &&
        !path.Contains("//", StringComparison.Ordinal) &&
        path.Split('/').All(segment =>
            segment.Length > 0 && segment != "." && segment != ".." &&
            segment.All(character => !char.IsControl(character)));

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

    private readonly record struct ParsedFile(ImportedContentFile? File, ContentImportResult ErrorResult)
    {
        public bool IsSuccess => File is not null;
        public static ParsedFile Success(ImportedContentFile file) => new(file, default);
        public static ParsedFile Failure(string code, string message, string? path = null) =>
            new(null, ContentImportResult.Failure(code, message, path));
    }
}

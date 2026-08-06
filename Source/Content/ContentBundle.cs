using System.Collections.ObjectModel;

namespace Mythos.Content;

public sealed record ImportedContentFile(
    string Path,
    string MediaType,
    ReadOnlyMemory<byte> Bytes,
    string Sha256Digest);

public sealed class ImportedContentBundle
{
    internal ImportedContentBundle(string packageId, IReadOnlyDictionary<string, ImportedContentFile> files)
    {
        PackageId = packageId;
        Files = new ReadOnlyDictionary<string, ImportedContentFile>(
            new Dictionary<string, ImportedContentFile>(files, StringComparer.Ordinal));
    }

    public string PackageId { get; }
    public IReadOnlyDictionary<string, ImportedContentFile> Files { get; }
    public ImportedContentFile PackageManifest => Files["package.json"];
}

public sealed record ContentImportError(string Code, string Message, string? Path = null);

public readonly record struct ContentImportResult(ImportedContentBundle? Value, ContentImportError? Error)
{
    public bool IsSuccess => Error is null;

    public static ContentImportResult Success(ImportedContentBundle value) => new(value, null);

    public static ContentImportResult Failure(string code, string message, string? path = null) =>
        new(null, new ContentImportError(code, message, path));
}

public static class ContentImportErrorCodes
{
    public const string MalformedBundle = "content.malformed-bundle";
    public const string UnsupportedBundle = "content.unsupported-bundle";
    public const string InvalidPackageId = "content.invalid-package-id";
    public const string InvalidPath = "content.invalid-path";
    public const string DuplicatePath = "content.duplicate-path";
    public const string InvalidEncoding = "content.invalid-encoding";
    public const string SizeMismatch = "content.size-mismatch";
    public const string IntegrityMismatch = "content.integrity-mismatch";
    public const string MissingManifest = "content.missing-manifest";
    public const string InventoryMismatch = "content.inventory-mismatch";
    public const string LimitExceeded = "content.limit-exceeded";
}

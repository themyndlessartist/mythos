using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Mythos.Content;

namespace Mythos.Framework.UnitTests.Content;

public sealed class ContentBundleImporterTests
{
    [Fact]
    public void ImportsValidatedPackageAndPreservesBytes()
    {
        var package = Package("genesis.lakewood", []);
        var bundle = Bundle("genesis.lakewood", [("package.json", "application/json", package)]);

        var result = new ContentBundleImporter().Import(bundle);

        Assert.True(result.IsSuccess);
        Assert.Equal("genesis.lakewood", result.Value!.PackageId);
        Assert.Equal(package, result.Value.PackageManifest.Bytes.ToArray());
    }

    [Fact]
    public void RejectsTraversalCaseCollisionsAndTamperedBytes()
    {
        var package = Package("genesis.lakewood", []);
        Assert.Equal(
            ContentImportErrorCodes.InvalidPath,
            new ContentBundleImporter().Import(Bundle("genesis.lakewood", [("../package.json", "application/json", package)])).Error!.Code);

        var collision = RawBundle("genesis.lakewood",
            File("package.json", "application/json", package),
            File("PACKAGE.JSON", "application/json", package));
        Assert.Equal(ContentImportErrorCodes.DuplicatePath, new ContentBundleImporter().Import(collision).Error!.Code);

        var tampered = JsonSerializer.SerializeToUtf8Bytes(new
        {
            bundle_kind = "mythos.content-bundle",
            bundle_version = "1.0",
            package_id = "genesis.lakewood",
            files = new[]
            {
                new
                {
                    path = "package.json",
                    media_type = "application/json",
                    size = package.Length,
                    integrity = new { algorithm = "sha256", digest = new string('0', 64) },
                    content_base64 = Convert.ToBase64String(package),
                },
            },
        });
        Assert.Equal(ContentImportErrorCodes.IntegrityMismatch, new ContentBundleImporter().Import(tampered).Error!.Code);
    }

    [Fact]
    public void RejectsManifestAndBundleInventoryDisagreement()
    {
        var content = Encoding.UTF8.GetBytes("{}");
        var package = Package("genesis.lakewood",
        [
            new Entry("genesis.test", "records/test.json", "application/json", content.Length, Digest(content)),
        ]);

        var missing = Bundle("genesis.lakewood", [("package.json", "application/json", package)]);
        Assert.Equal(ContentImportErrorCodes.InventoryMismatch, new ContentBundleImporter().Import(missing).Error!.Code);

        var complete = Bundle("genesis.lakewood",
        [
            ("package.json", "application/json", package),
            ("records/test.json", "application/json", content),
        ]);
        Assert.True(new ContentBundleImporter().Import(complete).IsSuccess);
    }

    [Fact]
    public void EnforcesConfiguredFileAndBundleLimits()
    {
        var package = Package("genesis.lakewood", []);
        var bundle = Bundle("genesis.lakewood", [("package.json", "application/json", package)]);

        var result = new ContentBundleImporter(new ContentImportLimits(0, 1, 1)).Import(bundle);

        Assert.Equal(ContentImportErrorCodes.LimitExceeded, result.Error!.Code);
    }

    private static byte[] Package(string packageId, IReadOnlyList<Entry> entries) => JsonSerializer.SerializeToUtf8Bytes(new
    {
        document_kind = "mythos.content-package",
        schema_version = "1.0",
        package_id = packageId,
        package_version = "0.1.0",
        display_name = "Test",
        entries,
        dependencies = Array.Empty<object>(),
    });

    private static byte[] Bundle(string packageId, IReadOnlyList<(string Path, string MediaType, byte[] Bytes)> files) =>
        RawBundle(packageId, files.Select(file => File(file.Path, file.MediaType, file.Bytes)).ToArray());

    private static byte[] RawBundle(string packageId, params object[] files) => JsonSerializer.SerializeToUtf8Bytes(new
    {
        bundle_kind = "mythos.content-bundle",
        bundle_version = "1.0",
        package_id = packageId,
        files,
    });

    private static object File(string path, string mediaType, byte[] bytes) => new
    {
        path,
        media_type = mediaType,
        size = bytes.Length,
        integrity = new { algorithm = "sha256", digest = Digest(bytes) },
        content_base64 = Convert.ToBase64String(bytes),
    };

    private static string Digest(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));

    private sealed record Entry(string id, string path, string media_type, int size, string digest)
    {
        public string kind => "asset";
        public object integrity => new { algorithm = "sha256", digest };
    }
}

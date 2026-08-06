using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Mythos.Content;

namespace Mythos.Framework.UnitTests.Content;

public sealed class CharacterAuthoringRecordReaderTests
{
    [Fact]
    public void ReadsControlNeutralCharacterRecord()
    {
        var record = Record();
        var bundle = ImportBundle(record);

        var result = new CharacterAuthoringRecordReader().Read(bundle, "mythos-genesis.khaige");

        Assert.True(result.IsSuccess);
        Assert.Equal("Khaige", result.Value!.DisplayName);
        Assert.Equal(["mythos-genesis.playable-test-character"], result.Value.Tags);
    }

    [Fact]
    public void RejectsWrongIdentityAndUnsafeAuthoringText()
    {
        var wrongIdentity = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(Record()).Replace(
            "mythos-genesis.khaige",
            "mythos-genesis.someone-else",
            StringComparison.Ordinal));
        Assert.Equal(
            ContentImportErrorCodes.InvalidRecord,
            new CharacterAuthoringRecordReader().Read(ImportBundle(wrongIdentity), "mythos-genesis.khaige").Error!.Code);

        var unsafeText = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(Record()).Replace(
            "Khaige",
            "<b>Khaige</b>",
            StringComparison.Ordinal));
        Assert.Equal(
            ContentImportErrorCodes.InvalidRecord,
            new CharacterAuthoringRecordReader().Read(ImportBundle(unsafeText), "mythos-genesis.khaige").Error!.Code);
    }

    [Fact]
    public void DoesNotReadNpcOrMissingEntriesAsCharacters()
    {
        var record = Record();
        var bundle = ImportBundle(record, "npc");

        var result = new CharacterAuthoringRecordReader().Read(bundle, "mythos-genesis.khaige");

        Assert.Equal(ContentImportErrorCodes.RecordNotFound, result.Error!.Code);
    }

    private static ImportedContentBundle ImportBundle(byte[] record, string kind = "character")
    {
        const string path = "records/characters/mythos-genesis.khaige.json";
        var manifest = JsonSerializer.SerializeToUtf8Bytes(new
        {
            document_kind = "mythos.content-package",
            schema_version = "1.1",
            package_id = "mythos-genesis.lakewood",
            package_version = "0.1.0",
            display_name = "Test",
            entries = new[]
            {
                new
                {
                    kind,
                    id = "mythos-genesis.khaige",
                    path,
                    media_type = "application/json",
                    size = record.Length,
                    integrity = new { algorithm = "sha256", digest = Digest(record) },
                },
            },
            dependencies = Array.Empty<object>(),
        });
        var bundle = JsonSerializer.SerializeToUtf8Bytes(new
        {
            bundle_kind = "mythos.content-bundle",
            bundle_version = "1.0",
            package_id = "mythos-genesis.lakewood",
            files = new[]
            {
                File("package.json", manifest),
                File(path, record),
            },
        });
        var import = new ContentBundleImporter().Import(bundle);
        Assert.True(import.IsSuccess);
        return import.Value!;
    }

    private static byte[] Record() => JsonSerializer.SerializeToUtf8Bytes(new
    {
        document_kind = "mythos.character-authoring",
        schema_version = "1.0",
        character_record_id = "mythos-genesis.khaige",
        display_name = "Khaige",
        tags = new[] { "mythos-genesis.playable-test-character" },
        notes = "Test character.",
        extensions = new Dictionary<string, object> { ["mythos.genesis"] = new { starting_area = "lakewood" } },
    });

    private static object File(string path, byte[] bytes) => new
    {
        path,
        media_type = "application/json",
        size = bytes.Length,
        integrity = new { algorithm = "sha256", digest = Digest(bytes) },
        content_base64 = Convert.ToBase64String(bytes),
    };

    private static string Digest(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
}

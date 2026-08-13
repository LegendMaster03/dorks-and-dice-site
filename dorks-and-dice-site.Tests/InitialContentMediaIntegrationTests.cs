using System.Net;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;

namespace dorks_and_dice_site.Tests;

public sealed class InitialContentMediaIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly IReadOnlyDictionary<string, string> ExpectedAssetHashes =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["agarmirs-house.jpg"] = "5c39703de46c28ee0a24e3e1fef3b8bb2099383a88b7a08a2fc44390c5c70129",
            ["bruma-player-home.jpg"] = "ab91386f2406ebb69262be5337260acc26a81f1ad891a43518d23f68bb47c11b",
            ["discord-announcement.png"] = "e22c8e59f5b976fb563132a27dcf19c307a1616c3c3e20dde612583b252b0135",
            ["dorians-house.jpg"] = "0881e134e50ee8169a1e6497b5bc799a62fd527a2216e0fbf1e6d141d1671cde",
            ["ending.png"] = "191058462e578f286f153ec371b2c12f7c6d9332dc1a529bd2e3f8b9cacfab74",
            ["gamescom-solved.png"] = "d79b7e1858a13cc535e3e82e4e161b6a25df751edcfe8740444d79517ff9686f",
            ["prize-art-original.png"] = "a44fccef46044d6ded19c9c402fa4d0408582ff43a922f5e0e197b48aa262b3b",
            ["seniorproject-item-metadata-edit.png"] = "ceb9d7d614733d375282bce425b79095cb1fda3d12310a497f2569d3fdfb6bac",
            ["seniorproject-operations-dashboard.png"] = "89860d9ed875a7201095baaa2a62faacbc11aa555323e954f629577ab3d7bc71",
            ["seniorproject-qr-localhost.png"] = "e2d630bbd0d8ea569fc76fd125ae1e96dc0f6a42715b816936fdef515a9d194b",
            ["seridurs-house-basement.jpg"] = "4a3c0283fb4bc0d710ca77a40b0bf57c012a933acc5996811460297a2ad8ff3a",
            ["simlab-weed-wacker.png"] = "ae3f9341067b367dc32dd26f199fc2e89559e16a6779d218f2169390e29b5cab",
            ["timberscar-hollow.jpg"] = "6b81bef5ff31345a7c2b8410fe7ebbbafb395aa488d3d80700acf774b1b1dcdc",
            ["unf-cptc-regionals-2025.jpg"] = "0da46622c5f74071f7b2f1fc0c8270e0cfafaef6d762744a491cb51a7486642e"
        };

    private readonly WebApplicationFactory<Program> _factory;

    public InitialContentMediaIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public void CommittedDatabaseContainsCompleteManagedMediaRecords()
    {
        using var connection = OpenCommittedDatabase();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT asset_key, asset_file_name, asset_media_type, asset_length, asset_sha256, asset_data
            FROM content_asset
            ORDER BY asset_key
            """;
        using var reader = command.ExecuteReader();

        var assetCount = 0;
        while (reader.Read())
        {
            assetCount++;
            Assert.True(Guid.TryParseExact(reader.GetString(0), "N", out _));
            Assert.Matches(@"^[A-Za-z0-9_-]+\.(?:jpg|png)$", reader.GetString(1));
            Assert.Contains(reader.GetString(2), new[] { "image/jpeg", "image/png" });

            var data = (byte[])reader[5];
            Assert.Equal(reader.GetInt64(3), data.LongLength);
            var storedHash = reader.GetString(4);
            Assert.Equal(ExpectedAssetHashes[reader.GetString(1)], storedHash);
            Assert.Equal(storedHash, Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant());
        }

        Assert.Equal(ExpectedAssetHashes.Count, assetCount);
    }

    [Fact]
    public void EveryManagedMarkdownImageResolvesToItsOwningDatabaseAsset()
    {
        using var connection = OpenCommittedDatabase();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT p.page_id, r.revision_body, r.revision_metadata_json
            FROM content_page p
            JOIN content_revision r ON r.revision_id = p.page_current_revision_id
            """;
        using var reader = command.ExecuteReader();

        var references = new List<(long PageId, string Key, string FileName)>();
        while (reader.Read())
        {
            var body = reader.GetString(1);
            var metadata = reader.GetString(2);
            Assert.DoesNotContain("/site-modes/professional/images/articles/", body, StringComparison.Ordinal);
            Assert.DoesNotContain("/site-modes/professional/images/projects/SeniorProject/", body, StringComparison.Ordinal);
            Assert.DoesNotContain("/site-modes/professional/images/skyblivion/", body, StringComparison.Ordinal);
            Assert.DoesNotContain("]()", body, StringComparison.Ordinal);

            foreach (Match match in Regex.Matches(
                body,
                @"!\[[^\]]*\]\(/content/media/(?<key>[0-9a-f]{32})/(?<file>[^\s)]+)"))
            {
                references.Add((reader.GetInt64(0), match.Groups["key"].Value, match.Groups["file"].Value));
            }

            if (metadata.Contains("ending.png", StringComparison.Ordinal))
            {
                Assert.Contains("/content/media/", metadata, StringComparison.Ordinal);
                Assert.DoesNotContain("/site-modes/professional/images/articles/", metadata, StringComparison.Ordinal);
            }
        }
        reader.Close();

        Assert.Equal(13, references.Count);
        foreach (var reference in references)
        {
            using var assetCommand = connection.CreateCommand();
            assetCommand.CommandText = """
                SELECT COUNT(*)
                FROM content_asset
                WHERE asset_page_id = $pageId
                  AND asset_key = $assetKey
                  AND asset_file_name = $fileName
                """;
            assetCommand.Parameters.AddWithValue("$pageId", reference.PageId);
            assetCommand.Parameters.AddWithValue("$assetKey", reference.Key);
            assetCommand.Parameters.AddWithValue("$fileName", reference.FileName);
            Assert.Equal(1L, (long)assetCommand.ExecuteScalar()!);
        }
    }

    [Fact]
    public async Task ManagedMediaEndpointServesImmutableContentWithAnEtag()
    {
        string url;
        string mediaType;
        long length;
        using (var connection = OpenCommittedDatabase())
        using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                SELECT asset_key, asset_file_name, asset_media_type, asset_length
                FROM content_asset
                WHERE asset_file_name = 'unf-cptc-regionals-2025.jpg'
                """;
            using var reader = command.ExecuteReader();
            Assert.True(reader.Read());
            url = $"/content/media/{reader.GetString(0)}/{reader.GetString(1)}";
            mediaType = reader.GetString(2);
            length = reader.GetInt64(3);
        }

        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using var request = new HttpRequestMessage(HttpMethod.Get, $"http://localhost{url}");
        request.Headers.Host = "localhost";
        request.Headers.Add("Cookie", "DevelopmentPreviewSiteMode=professional; DevelopmentEnabledContentSources=Local");
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(mediaType, response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(length, response.Content.Headers.ContentLength);
        var cacheControl = response.Headers.CacheControl?.ToString() ?? string.Empty;
        Assert.Contains("immutable", cacheControl, StringComparison.OrdinalIgnoreCase);
        Assert.NotNull(response.Headers.ETag);
    }

    private static SqliteConnection OpenCommittedDatabase()
    {
        var databasePath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "dorks-and-dice-site", "Content", "content.db"));
        var connection = new SqliteConnection($"Data Source={databasePath};Mode=ReadOnly");
        connection.Open();
        return connection;
    }
}

using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using RaidOps.ExternalApplication.Implementations.Services;
using RaidOps.UnitTests.Helpers;

namespace RaidOps.UnitTests.ExternalApplication.Services;

/// <summary>Unit tests for <see cref="WagoToolsService"/> — a fake HTTP handler, never the real network.</summary>
public class WagoToolsServiceTests
{
    private const string ListfileUrl = "https://github.com/wowdev/wow-listfile/releases/latest/download/community-listfile.csv";

    private static (WagoToolsService Sut, FakeHttpMessageHandler Handler) MakeSut(HttpStatusCode status, string? content, string? listfileUrl = ListfileUrl)
    {
        var handler = new FakeHttpMessageHandler(status, content);
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Wago:ListfileUrl"] = listfileUrl })
            .Build();
        return (new WagoToolsService(new HttpClient(handler) { BaseAddress = new Uri("https://wago.tools") }, configuration), handler);
    }

    // ── GetLatestBuildsAsync ─────────────────────────────────────────────────

    [Fact]
    public async Task GetLatestBuildsAsync_Success_ReturnsBuildsKeyedByProduct()
    {
        const string json = """
            {
              "wow": {"product":"wow","version":"12.0.1.66102","created_at":"2026-09-22 14:18:01","build_config":"x"},
              "wow_classic_beta": {"product":"wow_classic_beta","version":"1.60.1.69977","created_at":"2026-09-20 08:00:00"}
            }
            """;
        var (sut, handler) = MakeSut(HttpStatusCode.OK, json);

        var result = await sut.GetLatestBuildsAsync();

        result.Keys.Should().BeEquivalentTo("wow", "wow_classic_beta");
        result["wow_classic_beta"].Version.Should().Be("1.60.1.69977");
        result["wow"].CreatedAt.Should().Be(new DateTime(2026, 9, 22, 14, 18, 1, DateTimeKind.Utc));
        handler.LastRequest!.Method.Should().Be(HttpMethod.Get);
        handler.LastRequest.RequestUri!.ToString().Should().Be("https://wago.tools/api/builds/latest");
    }

    [Fact]
    public async Task GetLatestBuildsAsync_NonSuccessStatus_ThrowsHttpRequestException()
    {
        var (sut, _) = MakeSut(HttpStatusCode.InternalServerError, "boom");

        var act = () => sut.GetLatestBuildsAsync();

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task GetLatestBuildsAsync_NullJson_ThrowsInvalidOperationException()
    {
        var (sut, _) = MakeSut(HttpStatusCode.OK, "null");

        var act = () => sut.GetLatestBuildsAsync();

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task GetLatestBuildsAsync_MalformedJson_ThrowsJsonException()
    {
        var (sut, _) = MakeSut(HttpStatusCode.OK, "<html>not json</html>");

        var act = () => sut.GetLatestBuildsAsync();

        await act.Should().ThrowAsync<JsonException>();
    }

    // ── GetSpellNamesAsync ───────────────────────────────────────────────────

    [Fact]
    public async Task GetSpellNamesAsync_RequestUrl_ContainsBuildAndLocale()
    {
        var (sut, handler) = MakeSut(HttpStatusCode.OK, "ID,Name_lang\n1,Fireball\n");

        await sut.GetSpellNamesAsync("1.60.1.69977", "frFR");

        handler.LastRequest!.RequestUri!.ToString().Should().Be("https://wago.tools/db2/SpellName/csv?build=1.60.1.69977&locale=frFR");
    }

    [Fact]
    public async Task GetSpellNamesAsync_Lf_SkipsHeaderAndParsesRows()
    {
        var (sut, _) = MakeSut(HttpStatusCode.OK, "ID,Name_lang\n133,Fireball\n116,Frostbolt\n");

        var result = await sut.GetSpellNamesAsync("b", "enUS");

        result.Select(s => (s.Id, s.Name)).Should().Equal((133, "Fireball"), (116, "Frostbolt"));
    }

    [Fact]
    public async Task GetSpellNamesAsync_Crlf_TrimsCarriageReturns()
    {
        var (sut, _) = MakeSut(HttpStatusCode.OK, "ID,Name_lang\r\n133,Fireball\r\n116,Frostbolt\r\n");

        var result = await sut.GetSpellNamesAsync("b", "enUS");

        result.Select(s => s.Name).Should().Equal("Fireball", "Frostbolt");
    }

    [Fact]
    public async Task GetSpellNamesAsync_QuotedNamesWithCommasAndDoubledQuotes_AreUnescaped()
    {
        const string csv = "ID,Name_lang\n1,\"Fireball, Rank 1\"\n2,\"The \"\"Chosen\"\" One\"\n3,\"Trailing \"\"\"\n4,\"\"\n";
        var (sut, _) = MakeSut(HttpStatusCode.OK, csv);

        var result = await sut.GetSpellNamesAsync("b", "enUS");

        result.Select(s => s.Name).Should().Equal("Fireball, Rank 1", "The \"Chosen\" One", "Trailing \"", string.Empty);
    }

    [Fact]
    public async Task GetSpellNamesAsync_BlankLinesAndShortRows_AreSkipped()
    {
        var (sut, _) = MakeSut(HttpStatusCode.OK, "ID,Name_lang\n\n133,Fireball\n999\n\n116,Frostbolt\n\n\n");

        var result = await sut.GetSpellNamesAsync("b", "enUS");

        result.Select(s => s.Id).Should().Equal(133, 116);
    }

    [Fact]
    public async Task GetSpellNamesAsync_ManyRowsLargerThanTheReaderBuffer_AreAllParsed()
    {
        // ~200 KB of CSV, far beyond StreamReader's internal buffer, so rows straddle buffer refills.
        var csv = "ID,Name_lang\n" + string.Concat(Enumerable.Range(1, 10_000).Select(i => $"{i},\"Spell, number {i}\"\n"));
        var (sut, _) = MakeSut(HttpStatusCode.OK, csv);

        var result = await sut.GetSpellNamesAsync("b", "enUS");

        result.Should().HaveCount(10_000);
        result[0].Should().BeEquivalentTo(new { Id = 1, Name = "Spell, number 1" });
        result[^1].Should().BeEquivalentTo(new { Id = 10_000, Name = "Spell, number 10000" });
    }

    [Fact]
    public async Task GetSpellNamesAsync_EmptyBody_ReturnsEmptyList()
    {
        var (sut, _) = MakeSut(HttpStatusCode.OK, string.Empty);

        var result = await sut.GetSpellNamesAsync("b", "enUS");

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetSpellNamesAsync_NonSuccessStatus_ThrowsHttpRequestException()
    {
        var (sut, _) = MakeSut(HttpStatusCode.NotFound, "nope");

        var act = () => sut.GetSpellNamesAsync("b", "enUS");

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    // ── GetSpellIconFileDataIdsAsync ─────────────────────────────────────────

    [Fact]
    public async Task GetSpellIconFileDataIdsAsync_ResolvesColumnsByHeaderNameNotPosition()
    {
        // Deliberately shuffled: SpellIconFileDataID comes BEFORE SpellID, with unrelated columns in between.
        const string csv = "ID,SpellIconFileDataID,Attributes_0,SpellID\n1,136243,0,133\n2,135810,4,116\n";
        var (sut, handler) = MakeSut(HttpStatusCode.OK, csv);

        var result = await sut.GetSpellIconFileDataIdsAsync("1.60.1.69977");

        result.Should().BeEquivalentTo(new Dictionary<int, int> { [133] = 136243, [116] = 135810 });
        handler.LastRequest!.RequestUri!.ToString().Should().Be("https://wago.tools/db2/SpellMisc/csv?build=1.60.1.69977");
    }

    [Fact]
    public async Task GetSpellIconFileDataIdsAsync_Crlf_ParsesRows()
    {
        var (sut, _) = MakeSut(HttpStatusCode.OK, "ID,SpellID,SpellIconFileDataID\r\n1,133,111\r\n2,116,222\r\n");

        var result = await sut.GetSpellIconFileDataIdsAsync("b");

        result.Should().BeEquivalentTo(new Dictionary<int, int> { [133] = 111, [116] = 222 });
    }

    [Fact]
    public async Task GetSpellIconFileDataIdsAsync_HeaderOnly_ReturnsEmpty()
    {
        var (sut, _) = MakeSut(HttpStatusCode.OK, "ID,SpellID,SpellIconFileDataID\n");

        var result = await sut.GetSpellIconFileDataIdsAsync("b");

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetSpellIconFileDataIdsAsync_ZeroFileDataId_IsOmitted()
    {
        const string csv = "ID,SpellID,SpellIconFileDataID\n1,133,0\n2,116,135810\n";
        var (sut, _) = MakeSut(HttpStatusCode.OK, csv);

        var result = await sut.GetSpellIconFileDataIdsAsync("b");

        result.Should().ContainSingle().Which.Should().Be(new KeyValuePair<int, int>(116, 135810));
    }

    [Fact]
    public async Task GetSpellIconFileDataIdsAsync_DuplicateSpellId_LastRowWins()
    {
        const string csv = "ID,SpellID,SpellIconFileDataID\n1,133,111\n2,133,222\n";
        var (sut, _) = MakeSut(HttpStatusCode.OK, csv);

        var result = await sut.GetSpellIconFileDataIdsAsync("b");

        result[133].Should().Be(222);
    }

    [Theory]
    [InlineData("ID,SpellIconFileDataID\n1,5\n")]
    [InlineData("ID,SpellID\n1,5\n")]
    [InlineData("ID,Other\n1,5\n")]
    public async Task GetSpellIconFileDataIdsAsync_MissingColumn_ThrowsInvalidOperationException(string csv)
    {
        var (sut, _) = MakeSut(HttpStatusCode.OK, csv);

        var act = () => sut.GetSpellIconFileDataIdsAsync("b");

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*SpellID/SpellIconFileDataID*");
    }

    [Fact]
    public async Task GetSpellIconFileDataIdsAsync_NonSuccessStatus_ThrowsHttpRequestException()
    {
        var (sut, _) = MakeSut(HttpStatusCode.BadGateway, "x");

        var act = () => sut.GetSpellIconFileDataIdsAsync("b");

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    // ── GetFileNameAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task GetFileNameAsync_Success_ReturnsFilenameAndSendsBuildVersion()
    {
        var (sut, handler) = MakeSut(HttpStatusCode.OK, """{"filename":"interface/icons/inv_misc_food_59.blp","id":136243}""");

        var result = await sut.GetFileNameAsync(136243, "1.60.1.69977");

        result.Should().Be("interface/icons/inv_misc_food_59.blp");
        handler.LastRequest!.RequestUri!.ToString().Should().Be("https://wago.tools/api/info/136243?version=1.60.1.69977");
    }

    [Fact]
    public async Task GetFileNameAsync_NonSuccessStatus_ThrowsHttpRequestException()
    {
        var (sut, _) = MakeSut(HttpStatusCode.BadRequest, "version required");

        var act = () => sut.GetFileNameAsync(1, "b");

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task GetFileNameAsync_NullJson_ThrowsInvalidOperationException()
    {
        var (sut, _) = MakeSut(HttpStatusCode.OK, "null");

        var act = () => sut.GetFileNameAsync(42, "b");

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*42*");
    }

    // ── GetIconFileNamesAsync ────────────────────────────────────────────────

    [Fact]
    public async Task GetIconFileNamesAsync_IconLine_YieldsIdToNameWithoutPathOrExtension()
    {
        var (sut, handler) = MakeSut(HttpStatusCode.OK, "134015;interface/icons/inv_misc_food_59.blp\n");

        var result = await sut.GetIconFileNamesAsync();

        result.Should().BeEquivalentTo(new Dictionary<int, string> { [134015] = "inv_misc_food_59" });
        handler.LastRequest!.Method.Should().Be(HttpMethod.Get);
        handler.LastRequest.RequestUri!.ToString().Should().Be(ListfileUrl);
    }

    [Fact]
    public async Task GetIconFileNamesAsync_MixedListfile_KeepsOnlyIconEntries()
    {
        const string listfile = "134015;interface/icons/inv_misc_food_59.blp\n"
            + "53183;sound/music/citymusic/orgrimmar/orgrimmar01.mp3\n"
            + "\n"
            + "no-separator-on-this-line\n"
            + ";interface/icons/leading_separator.blp\n"
            + "abc;interface/icons/non_numeric_id.blp\n"
            + "136243;interface/icons/spell_fire_flamebolt.blp\n"
            + "999;interface/iconsx/not_the_icons_folder.blp\n";
        var (sut, _) = MakeSut(HttpStatusCode.OK, listfile);

        var result = await sut.GetIconFileNamesAsync();

        result.Should().BeEquivalentTo(new Dictionary<int, string>
        {
            [134015] = "inv_misc_food_59",
            [136243] = "spell_fire_flamebolt",
        });
    }

    [Fact]
    public async Task GetIconFileNamesAsync_PathPrefixIsMatchedCaseInsensitively()
    {
        var (sut, _) = MakeSut(HttpStatusCode.OK, "1;Interface/Icons/INV_Misc_Food_59.blp\r\n2;INTERFACE/ICONS/Spell_Fire.BLP\r\n");

        var result = await sut.GetIconFileNamesAsync();

        result.Should().BeEquivalentTo(new Dictionary<int, string> { [1] = "INV_Misc_Food_59", [2] = "Spell_Fire" });
    }

    [Fact]
    public async Task GetIconFileNamesAsync_IconInASubfolder_KeepsTheBareFileName()
    {
        var (sut, _) = MakeSut(HttpStatusCode.OK, "7;interface/icons/achievement/ach_boss_x.blp\n");

        var result = await sut.GetIconFileNamesAsync();

        result.Should().ContainSingle().Which.Should().Be(new KeyValuePair<int, string>(7, "ach_boss_x"));
    }

    [Fact]
    public async Task GetIconFileNamesAsync_DuplicateId_LastLineWins()
    {
        var (sut, _) = MakeSut(HttpStatusCode.OK, "5;interface/icons/first.blp\n5;interface/icons/second.blp\n");

        var result = await sut.GetIconFileNamesAsync();

        result[5].Should().Be("second");
    }

    [Fact]
    public async Task GetIconFileNamesAsync_EmptyBody_ReturnsEmptyMap()
    {
        var (sut, _) = MakeSut(HttpStatusCode.OK, string.Empty);

        var result = await sut.GetIconFileNamesAsync();

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetIconFileNamesAsync_ListfileUrlNotConfigured_ThrowsInvalidOperationExceptionWithoutAnyRequest()
    {
        var (sut, handler) = MakeSut(HttpStatusCode.OK, "1;interface/icons/x.blp\n", listfileUrl: null);

        var act = () => sut.GetIconFileNamesAsync();

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Wago:ListfileUrl*");
        handler.LastRequest.Should().BeNull();
    }

    [Fact]
    public async Task GetIconFileNamesAsync_NonSuccessStatus_ThrowsHttpRequestException()
    {
        var (sut, _) = MakeSut(HttpStatusCode.ServiceUnavailable, "down");

        var act = () => sut.GetIconFileNamesAsync();

        await act.Should().ThrowAsync<HttpRequestException>();
    }
}

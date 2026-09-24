using System.Net;
using System.Text.Json;
using FluentAssertions;
using RaidOps.ExternalApplication.Implementations.Services;
using RaidOps.UnitTests.Helpers;

namespace RaidOps.UnitTests.ExternalApplication.Services;

/// <summary>Unit tests for <see cref="WagoToolsService"/> — a fake HTTP handler, never the real network.</summary>
public class WagoToolsServiceTests
{
    private static (WagoToolsService Sut, FakeHttpMessageHandler Handler) MakeSut(HttpStatusCode status, string? content)
    {
        var handler = new FakeHttpMessageHandler(status, content);
        return (new WagoToolsService(new HttpClient(handler)), handler);
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
}

using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using RaidOps.Application.Contracts.Raids.Spells.Commands;
using RaidOps.Application.Contracts.Raids.Spells.Responses;
using RaidOps.Application.Implementations.Raids.Spells.CommandHandlers;
using RaidOps.Domain.Models.Reference;
using RaidOps.ExternalApplication.Contracts.Services.DiscordBot;
using RaidOps.ExternalApplication.Contracts.Services.WagoTools;
using RaidOps.ExternalApplication.Contracts.Services.WagoTools.Responses;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;
using RaidOps.UnitTests.Helpers;

namespace RaidOps.UnitTests.Application.Raids.Spells.CommandHandlers;

/// <summary>Unit tests for <see cref="SyncSpellsCommandHandler"/>.</summary>
public class SyncSpellsCommandHandlerTests
{
    private const string ForeverProduct = "wow_classic_beta";
    private const string RetailProduct = "wow";
    private const int ForeverExpansionId = 12;
    private const int RetailExpansionId = 11;
    private const string ChannelId = "1234567890";

    private readonly Mock<IBranchRepository> _branches = new();
    private readonly Mock<IWagoToolsService> _wago = new();
    private readonly Mock<ISpellRepository> _spells = new();
    private readonly Mock<IDiscordBotService> _discord = new();
    private readonly Mock<IMessageService> _messages = new();
    private readonly CapturingLogger<SyncSpellsCommandHandler> _logger = new();
    private readonly List<string> _callOrder = [];

    private const string IconBaseUrl = "https://render.worldofwarcraft.com/us/icons/56/";

    private Dictionary<string, string?> _config = new() { ["Discord:SpellSyncChannelId"] = ChannelId, ["Blizzard:SpellIconBaseUrl"] = IconBaseUrl };
    private List<SpellAvailability> _upsertedRows = [];
    private readonly List<List<SpellAvailability>> _upsertBatches = [];

    public SyncSpellsCommandHandlerTests()
    {
        _discord.SetupGet(d => d.Messages).Returns(_messages.Object);

        // No icon is in the community listfile unless a test says so — icons then fall back to individual lookups.
        SetupListfile();

        // The handler hands the repository a lazy sequence, so it must be enumerated here.
        _spells.Setup(s => s.UpsertAsync(It.IsAny<IEnumerable<SpellAvailability>>(), It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<SpellAvailability>, CancellationToken>((rows, _) =>
            {
                _callOrder.Add("upsert");
                _upsertedRows = rows.ToList();
                _upsertBatches.Add(_upsertedRows);
            })
            .ReturnsAsync(new SpellSyncDiff());

        _branches.Setup(b => b.UpdateSyncStateAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .Callback(() => _callOrder.Add("syncState"))
            .Returns(Task.CompletedTask);
    }

    private SyncSpellsCommandHandler MakeSut() => new(
        _branches.Object,
        _wago.Object,
        _spells.Object,
        _discord.Object,
        new ConfigurationBuilder().AddInMemoryCollection(_config).Build(),
        _logger);

    private static Branch MakeBranch(int id, string name, string product, int expansionId, string? lastBuild = null) => new()
    {
        Id = id,
        Name = name,
        WagoProductCode = product,
        CurrentExpansionId = expansionId,
        LastSyncedBuildVersion = lastBuild,
    };

    private static readonly DateTime BuildDate = new(2026, 9, 22, 14, 18, 1, DateTimeKind.Utc);

    private void SetupBranches(params Branch[] branches)
        => _branches.Setup(b => b.GetActiveWagoTrackedAsync(It.IsAny<CancellationToken>())).ReturnsAsync(branches.ToList());

    private void SetupLatestBuilds(params (string Product, string Version)[] builds)
        => _wago.Setup(w => w.GetLatestBuildsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(builds.ToDictionary(b => b.Product, b => new WagoBuildInfo { Product = b.Product, Version = b.Version, CreatedAt = BuildDate }));

    private void SetupNames(string build, string locale, params (int Id, string Name)[] names)
        => _wago.Setup(w => w.GetSpellNamesAsync(build, locale, It.IsAny<CancellationToken>()))
            .ReturnsAsync(names.Select(n => new WagoSpellName { Id = n.Id, Name = n.Name }).ToList());

    private void SetupIcons(string build, Dictionary<int, int> iconFileDataIds)
        => _wago.Setup(w => w.GetSpellIconFileDataIdsAsync(build, It.IsAny<CancellationToken>())).ReturnsAsync(iconFileDataIds);

    private void SetupFileName(int fileDataId, string build, string fileName)
        => _wago.Setup(w => w.GetFileNameAsync(fileDataId, build, It.IsAny<CancellationToken>())).ReturnsAsync(fileName);

    private void SetupListfile(params (int FileDataId, string IconName)[] icons)
        => _wago.Setup(w => w.GetIconFileNamesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(icons.ToDictionary(i => i.FileDataId, i => i.IconName));

    private void SetupEmptyBuildContent(string build)
    {
        SetupNames(build, "enUS");
        SetupNames(build, "frFR");
        SetupNames(build, "deDE");
        SetupIcons(build, []);
    }

    private void SetupDiff(SpellSyncDiff diff)
        => _spells.Setup(s => s.UpsertAsync(It.IsAny<IEnumerable<SpellAvailability>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(diff);

    private static List<BranchSyncResult> Results(RaidOps.Application.Contracts.Common.CommandResponse response)
        => (List<BranchSyncResult>)response.Body!;

    // ── Nothing to do ────────────────────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_NoTrackedBranches_ReturnsOkWithEmptyListAndNeverCallsWago()
    {
        SetupBranches();

        var result = await MakeSut().HandleAsync(new SyncSpellsCommand());

        result.IsSuccess.Should().BeTrue();
        Results(result.Value!).Should().BeEmpty();
        result.Value!.Message.Should().Contain("No wago.tools-tracked branch");
        _wago.Verify(w => w.GetLatestBuildsAsync(It.IsAny<CancellationToken>()), Times.Never);
        _wago.Verify(w => w.GetIconFileNamesAsync(It.IsAny<CancellationToken>()), Times.Never);
        _spells.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task HandleAsync_ProductMissingFromLatestBuilds_SkipsBranchWithWarning()
    {
        SetupBranches(MakeBranch(5, "Forever", ForeverProduct, ForeverExpansionId));
        SetupLatestBuilds((RetailProduct, "12.0.0.1"));

        var result = await MakeSut().HandleAsync(new SyncSpellsCommand());

        result.IsSuccess.Should().BeTrue();
        Results(result.Value!).Should().BeEmpty();
        _wago.Verify(w => w.GetSpellNamesAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _wago.Verify(w => w.GetIconFileNamesAsync(It.IsAny<CancellationToken>()), Times.Never);
        _spells.Verify(s => s.UpsertAsync(It.IsAny<IEnumerable<SpellAvailability>>(), It.IsAny<CancellationToken>()), Times.Never);
        _logger.Entries.Should().Contain(e => e.Level == LogLevel.Warning && e.Message.Contains(ForeverProduct) && e.Message.Contains("Forever"));
    }

    [Fact]
    public async Task HandleAsync_BuildUnchangedAndNotForced_IsSkippedWithNothingFetchedOrUpserted()
    {
        SetupBranches(MakeBranch(5, "Forever", ForeverProduct, ForeverExpansionId, lastBuild: "1.60.1.69977"));
        SetupLatestBuilds((ForeverProduct, "1.60.1.69977"));

        var result = await MakeSut().HandleAsync(new SyncSpellsCommand { Force = false });

        var branchResult = Results(result.Value!).Should().ContainSingle().Subject;
        branchResult.Skipped.Should().BeTrue();
        branchResult.BranchId.Should().Be(5);
        branchResult.BranchName.Should().Be("Forever");
        branchResult.PreviousBuild.Should().Be("1.60.1.69977");
        branchResult.LatestBuild.Should().Be("1.60.1.69977");
        _wago.Verify(w => w.GetSpellNamesAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _wago.Verify(w => w.GetSpellIconFileDataIdsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _wago.Verify(w => w.GetIconFileNamesAsync(It.IsAny<CancellationToken>()), Times.Never);
        _spells.Verify(s => s.UpsertAsync(It.IsAny<IEnumerable<SpellAvailability>>(), It.IsAny<CancellationToken>()), Times.Never);
        _branches.Verify(b => b.UpdateSyncStateAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
        result.Value!.Message.Should().Be("1 branch(es) checked.");
    }

    [Fact]
    public async Task HandleAsync_BuildUnchangedButForced_ReSyncsTheSameBuild()
    {
        SetupBranches(MakeBranch(5, "Forever", ForeverProduct, ForeverExpansionId, lastBuild: "1.60.1.69977"));
        SetupLatestBuilds((ForeverProduct, "1.60.1.69977"));
        SetupEmptyBuildContent("1.60.1.69977");

        var result = await MakeSut().HandleAsync(new SyncSpellsCommand { Force = true });

        Results(result.Value!).Single().Skipped.Should().BeFalse();
        _spells.Verify(s => s.UpsertAsync(It.IsAny<IEnumerable<SpellAvailability>>(), It.IsAny<CancellationToken>()), Times.Once);
        _branches.Verify(b => b.UpdateSyncStateAsync(5, "1.60.1.69977", BuildDate, It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── Sync of a changed build ──────────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_IconBaseUrlNotConfigured_ThrowsBeforeTouchingTheDatabase()
    {
        _config = new Dictionary<string, string?> { ["Discord:SpellSyncChannelId"] = ChannelId };
        SetupBranches(MakeBranch(5, "Forever", ForeverProduct, ForeverExpansionId));
        SetupLatestBuilds((ForeverProduct, "1.60.1.69977"));
        SetupEmptyBuildContent("1.60.1.69977");

        var act = () => MakeSut().HandleAsync(new SyncSpellsCommand());

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Blizzard:SpellIconBaseUrl*");
        _spells.Verify(s => s.UpsertAsync(It.IsAny<IEnumerable<SpellAvailability>>(), It.IsAny<CancellationToken>()), Times.Never);
        _branches.Verify(b => b.UpdateSyncStateAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_ChangedBuild_FetchesThreeLocalesAndSpellMiscAndUpsertsThenUpdatesSyncState()
    {
        SetupBranches(MakeBranch(5, "Forever", ForeverProduct, ForeverExpansionId, lastBuild: "1.60.0.1"));
        SetupLatestBuilds((ForeverProduct, "1.60.1.69977"));
        SetupNames("1.60.1.69977", "enUS", (133, "Fireball"), (116, "Frostbolt"));
        SetupNames("1.60.1.69977", "frFR", (133, "Boule de feu"), (116, "Eclair de givre"));
        SetupNames("1.60.1.69977", "deDE", (133, "Feuerball"), (116, "Frostblitz"));
        SetupIcons("1.60.1.69977", new Dictionary<int, int> { [133] = 1001, [116] = 1002 });
        SetupFileName(1001, "1.60.1.69977", "interface/icons/spell_fire_flamebolt.blp");
        SetupFileName(1002, "1.60.1.69977", "interface/icons/spell_frost_frostbolt02.blp");

        var result = await MakeSut().HandleAsync(new SyncSpellsCommand());

        result.IsSuccess.Should().BeTrue();
        _upsertedRows.Should().HaveCount(2);
        var fireball = _upsertedRows.Single(r => r.SpellId == 133);
        fireball.ExpansionId.Should().Be(ForeverExpansionId);
        fireball.NameEn.Should().Be("Fireball");
        fireball.NameFr.Should().Be("Boule de feu");
        fireball.NameDe.Should().Be("Feuerball");
        fireball.IconUrl.Should().Be("https://render.worldofwarcraft.com/us/icons/56/spell_fire_flamebolt.jpg");
        _upsertedRows.Single(r => r.SpellId == 116).IconUrl.Should().EndWith("spell_frost_frostbolt02.jpg");
        _callOrder.Should().Equal("upsert", "syncState");
        _branches.Verify(b => b.UpdateSyncStateAsync(5, "1.60.1.69977", BuildDate, It.IsAny<CancellationToken>()), Times.Once);
        _wago.Verify(w => w.GetSpellNamesAsync("1.60.1.69977", "enUS", It.IsAny<CancellationToken>()), Times.Once);
        _wago.Verify(w => w.GetSpellNamesAsync("1.60.1.69977", "frFR", It.IsAny<CancellationToken>()), Times.Once);
        _wago.Verify(w => w.GetSpellNamesAsync("1.60.1.69977", "deDE", It.IsAny<CancellationToken>()), Times.Once);
        _wago.Verify(w => w.GetSpellIconFileDataIdsAsync("1.60.1.69977", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_ChangedBuild_ResultCarriesPreviousLatestAndDiffCounts()
    {
        SetupBranches(MakeBranch(5, "Forever", ForeverProduct, ForeverExpansionId, lastBuild: "1.60.0.1"));
        SetupLatestBuilds((ForeverProduct, "1.60.1.69977"));
        SetupEmptyBuildContent("1.60.1.69977");
        SetupDiff(new SpellSyncDiff
        {
            Added = [new SpellSyncEntry { SpellId = 1, NameEn = "A" }, new SpellSyncEntry { SpellId = 2, NameEn = "B" }],
            Renamed = [new SpellSyncEntry { SpellId = 3, NameEn = "C2", PreviousNameEn = "C1" }],
        });

        var result = await MakeSut().HandleAsync(new SyncSpellsCommand());

        var branchResult = Results(result.Value!).Single();
        branchResult.Skipped.Should().BeFalse();
        branchResult.PreviousBuild.Should().Be("1.60.0.1");
        branchResult.LatestBuild.Should().Be("1.60.1.69977");
        branchResult.AddedCount.Should().Be(2);
        branchResult.RenamedCount.Should().Be(1);
    }

    [Fact]
    public async Task HandleAsync_LocaleMissingASpell_FallsBackToEnglishName()
    {
        SetupBranches(MakeBranch(5, "Forever", ForeverProduct, ForeverExpansionId));
        SetupLatestBuilds((ForeverProduct, "b1"));
        SetupNames("b1", "enUS", (1, "Only English"), (2, "Has French"), (3, "Has German"));
        SetupNames("b1", "frFR", (2, "Francais"));
        SetupNames("b1", "deDE", (3, "Deutsch"));
        SetupIcons("b1", []);

        await MakeSut().HandleAsync(new SyncSpellsCommand());

        var onlyEnglish = _upsertedRows.Single(r => r.SpellId == 1);
        onlyEnglish.NameFr.Should().Be("Only English");
        onlyEnglish.NameDe.Should().Be("Only English");
        var hasFrench = _upsertedRows.Single(r => r.SpellId == 2);
        hasFrench.NameFr.Should().Be("Francais");
        hasFrench.NameDe.Should().Be("Has French");
        var hasGerman = _upsertedRows.Single(r => r.SpellId == 3);
        hasGerman.NameFr.Should().Be("Has German");
        hasGerman.NameDe.Should().Be("Deutsch");
    }

    [Fact]
    public async Task HandleAsync_SpellsSharingAnIconFileDataId_ResolveEachDistinctFileDataIdOnce()
    {
        SetupBranches(MakeBranch(5, "Forever", ForeverProduct, ForeverExpansionId));
        SetupLatestBuilds((ForeverProduct, "b1"));
        SetupNames("b1", "enUS", (1, "Rank 1"), (2, "Rank 2"), (3, "Rank 3"), (4, "No icon"));
        SetupNames("b1", "frFR");
        SetupNames("b1", "deDE");
        SetupIcons("b1", new Dictionary<int, int> { [1] = 500, [2] = 500, [3] = 600 });
        SetupFileName(500, "b1", "interface/icons/shared.blp");
        SetupFileName(600, "b1", "interface/icons/other.blp");

        await MakeSut().HandleAsync(new SyncSpellsCommand());

        _wago.Verify(w => w.GetFileNameAsync(500, "b1", It.IsAny<CancellationToken>()), Times.Once);
        _wago.Verify(w => w.GetFileNameAsync(600, "b1", It.IsAny<CancellationToken>()), Times.Once);
        _wago.Verify(w => w.GetFileNameAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        _upsertedRows.Single(r => r.SpellId == 1).IconUrl.Should().EndWith("/shared.jpg");
        _upsertedRows.Single(r => r.SpellId == 2).IconUrl.Should().EndWith("/shared.jpg");
        _upsertedRows.Single(r => r.SpellId == 3).IconUrl.Should().EndWith("/other.jpg");
        _upsertedRows.Single(r => r.SpellId == 4).IconUrl.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsync_WindowsStyleFileName_StillYieldsIconNameWithoutExtension()
    {
        SetupBranches(MakeBranch(5, "Forever", ForeverProduct, ForeverExpansionId));
        SetupLatestBuilds((ForeverProduct, "b1"));
        SetupNames("b1", "enUS", (1, "Spell"));
        SetupNames("b1", "frFR");
        SetupNames("b1", "deDE");
        SetupIcons("b1", new Dictionary<int, int> { [1] = 500 });
        SetupFileName(500, "b1", "Interface\\Icons\\INV_Misc_Food_59.blp");

        await MakeSut().HandleAsync(new SyncSpellsCommand());

        _upsertedRows.Single().IconUrl.Should().Be("https://render.worldofwarcraft.com/us/icons/56/INV_Misc_Food_59.jpg");
    }

    [Fact]
    public async Task HandleAsync_HttpFailureOnOneFileDataId_LeavesOnlyThatIconEmptyAndContinues()
    {
        SetupBranches(MakeBranch(5, "Forever", ForeverProduct, ForeverExpansionId));
        SetupLatestBuilds((ForeverProduct, "b1"));
        SetupNames("b1", "enUS", (1, "Good"), (2, "Dangling"));
        SetupNames("b1", "frFR");
        SetupNames("b1", "deDE");
        SetupIcons("b1", new Dictionary<int, int> { [1] = 500, [2] = 600 });
        SetupFileName(500, "b1", "interface/icons/good.blp");
        _wago.Setup(w => w.GetFileNameAsync(600, "b1", It.IsAny<CancellationToken>())).ThrowsAsync(new HttpRequestException("404"));

        var result = await MakeSut().HandleAsync(new SyncSpellsCommand());

        result.IsSuccess.Should().BeTrue();
        _upsertedRows.Single(r => r.SpellId == 1).IconUrl.Should().EndWith("/good.jpg");
        _upsertedRows.Single(r => r.SpellId == 2).IconUrl.Should().BeEmpty();
        _branches.Verify(b => b.UpdateSyncStateAsync(5, "b1", BuildDate, It.IsAny<CancellationToken>()), Times.Once);
        _logger.Entries.Should().Contain(e => e.Level == LogLevel.Warning && e.Exception is HttpRequestException && e.Message.Contains("600"));
    }

    [Fact]
    public async Task HandleAsync_NonHttpFailureResolvingAnIcon_Propagates()
    {
        SetupBranches(MakeBranch(5, "Forever", ForeverProduct, ForeverExpansionId));
        SetupLatestBuilds((ForeverProduct, "b1"));
        SetupNames("b1", "enUS", (1, "Spell"));
        SetupNames("b1", "frFR");
        SetupNames("b1", "deDE");
        SetupIcons("b1", new Dictionary<int, int> { [1] = 500 });
        _wago.Setup(w => w.GetFileNameAsync(500, "b1", It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("bad json"));

        var act = () => MakeSut().HandleAsync(new SyncSpellsCommand());

        await act.Should().ThrowAsync<InvalidOperationException>();
        _branches.Verify(b => b.UpdateSyncStateAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_MultipleBranches_SyncsOnlyTheOnesWhoseBuildChanged()
    {
        SetupBranches(
            MakeBranch(1, "Retail", RetailProduct, RetailExpansionId, lastBuild: "12.0.0.1"),
            MakeBranch(5, "Forever", ForeverProduct, ForeverExpansionId, lastBuild: "1.60.0.1"));
        SetupLatestBuilds((RetailProduct, "12.0.0.1"), (ForeverProduct, "1.60.1.69977"));
        SetupEmptyBuildContent("1.60.1.69977");

        var result = await MakeSut().HandleAsync(new SyncSpellsCommand());

        var results = Results(result.Value!);
        results.Select(r => r.BranchName).Should().Equal("Retail", "Forever");
        results.Select(r => r.Skipped).Should().Equal(true, false);
        _branches.Verify(b => b.UpdateSyncStateAsync(1, It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
        _branches.Verify(b => b.UpdateSyncStateAsync(5, "1.60.1.69977", BuildDate, It.IsAny<CancellationToken>()), Times.Once);
        result.Value!.Message.Should().Be("2 branch(es) checked.");
    }

    // ── Icon resolution: community listfile first, individual lookups for the rest ──

    /// <summary>Spells 1..<paramref name="count"/> each carry their own icon FileDataID (1000 + spell ID), none in the listfile.</summary>
    private void SetupManySpellsWithUnlistedIcons(string build, int count)
    {
        SetupBranches(MakeBranch(5, "Forever", ForeverProduct, ForeverExpansionId));
        SetupLatestBuilds((ForeverProduct, build));
        SetupNames(build, "enUS", Enumerable.Range(1, count).Select(i => (i, $"Spell {i}")).ToArray());
        SetupNames(build, "frFR");
        SetupNames(build, "deDE");
        SetupIcons(build, Enumerable.Range(1, count).ToDictionary(i => i, i => 1000 + i));
        _wago.Setup(w => w.GetFileNameAsync(It.IsAny<int>(), build, It.IsAny<CancellationToken>()))
            .ReturnsAsync((int fileDataId, string _, CancellationToken _) => $"interface/icons/icon_{fileDataId}.blp");
    }

    private List<int> LookedUpFileDataIds() => _wago.Invocations
        .Where(i => i.Method.Name == nameof(IWagoToolsService.GetFileNameAsync))
        .Select(i => (int)i.Arguments[0])
        .ToList();

    [Fact]
    public async Task HandleAsync_IconInTheListfile_IsUsedWithoutAnyIndividualLookup()
    {
        SetupBranches(MakeBranch(5, "Forever", ForeverProduct, ForeverExpansionId));
        SetupLatestBuilds((ForeverProduct, "b1"));
        SetupNames("b1", "enUS", (1, "Rank 1"), (2, "Rank 2"), (3, "Other"));
        SetupNames("b1", "frFR");
        SetupNames("b1", "deDE");
        SetupIcons("b1", new Dictionary<int, int> { [1] = 500, [2] = 500, [3] = 600 });
        SetupListfile((500, "shared_icon"), (600, "other_icon"), (700, "unused_icon"));

        var result = await MakeSut().HandleAsync(new SyncSpellsCommand());

        result.IsSuccess.Should().BeTrue();
        _wago.Verify(w => w.GetFileNameAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _upsertedRows.Single(r => r.SpellId == 1).IconUrl.Should().Be(IconBaseUrl + "shared_icon.jpg");
        _upsertedRows.Single(r => r.SpellId == 2).IconUrl.Should().Be(IconBaseUrl + "shared_icon.jpg");
        _upsertedRows.Single(r => r.SpellId == 3).IconUrl.Should().Be(IconBaseUrl + "other_icon.jpg");
        _logger.Entries.Should().NotContain(e => e.Level == LogLevel.Warning);
    }

    [Fact]
    public async Task HandleAsync_IconMissingFromTheListfile_FallsBackToAnIndividualLookupOnlyForThatOne()
    {
        SetupBranches(MakeBranch(5, "Forever", ForeverProduct, ForeverExpansionId));
        SetupLatestBuilds((ForeverProduct, "b1"));
        SetupNames("b1", "enUS", (1, "Listed"), (2, "Too new"), (3, "No icon"));
        SetupNames("b1", "frFR");
        SetupNames("b1", "deDE");
        SetupIcons("b1", new Dictionary<int, int> { [1] = 500, [2] = 600 });
        SetupListfile((500, "listed_icon"));
        SetupFileName(600, "b1", "interface/icons/brand_new_icon.blp");

        await MakeSut().HandleAsync(new SyncSpellsCommand());

        LookedUpFileDataIds().Should().Equal(600);
        _wago.Verify(w => w.GetFileNameAsync(600, "b1", It.IsAny<CancellationToken>()), Times.Once);
        _upsertedRows.Single(r => r.SpellId == 1).IconUrl.Should().Be(IconBaseUrl + "listed_icon.jpg");
        _upsertedRows.Single(r => r.SpellId == 2).IconUrl.Should().Be(IconBaseUrl + "brand_new_icon.jpg");
        _upsertedRows.Single(r => r.SpellId == 3).IconUrl.Should().BeEmpty();
        _logger.Entries.Should().NotContain(e => e.Level == LogLevel.Information && e.Message.Contains("missing from the listfile"));
    }

    [Fact]
    public async Task HandleAsync_MoreThan500IconsMissingFromTheListfile_OnlyTheHighestFileDataIdsAreLookedUp()
    {
        SetupManySpellsWithUnlistedIcons("b1", 520);

        var result = await MakeSut().HandleAsync(new SyncSpellsCommand());

        result.IsSuccess.Should().BeTrue();
        // FileDataIDs are 1001..1520: the 500 newest are 1021..1520, the 20 oldest (1001..1020) are skipped.
        var lookedUp = LookedUpFileDataIds();
        lookedUp.Should().HaveCount(500);
        lookedUp.Should().BeEquivalentTo(Enumerable.Range(1021, 500));
        _upsertedRows.Should().HaveCount(520);
        _upsertedRows.Where(r => r.SpellId <= 20).Should().OnlyContain(r => r.IconUrl == string.Empty);
        _upsertedRows.Where(r => r.SpellId > 20).Should().OnlyContain(r => r.IconUrl.StartsWith(IconBaseUrl) && r.IconUrl.EndsWith(".jpg"));
        _upsertedRows.Single(r => r.SpellId == 21).IconUrl.Should().Be(IconBaseUrl + "icon_1021.jpg");
        _upsertedRows.Single(r => r.SpellId == 520).IconUrl.Should().Be(IconBaseUrl + "icon_1520.jpg");
        _logger.Entries.Should().ContainSingle(e => e.Level == LogLevel.Information && e.Message.Contains("20 icon(s) missing from the listfile") && e.Message.Contains("500"));
        _branches.Verify(b => b.UpdateSyncStateAsync(5, "b1", BuildDate, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_ExactlyTheCapMissingFromTheListfile_AreAllLookedUpWithoutASkippedLog()
    {
        SetupManySpellsWithUnlistedIcons("b1", 500);

        await MakeSut().HandleAsync(new SyncSpellsCommand());

        LookedUpFileDataIds().Should().BeEquivalentTo(Enumerable.Range(1001, 500));
        _upsertedRows.Should().OnlyContain(r => r.IconUrl.Length > 0);
        _logger.Entries.Should().NotContain(e => e.Level == LogLevel.Information && e.Message.Contains("missing from the listfile"));
    }

    [Fact]
    public async Task HandleAsync_ListfileHitsDoNotCountTowardsTheIndividualLookupCap()
    {
        // 600 icons: 200 resolved by the listfile, 400 not — all 400 fit under the cap and none are skipped.
        SetupManySpellsWithUnlistedIcons("b1", 600);
        SetupListfile(Enumerable.Range(1, 200).Select(i => (1000 + i, $"listed_{i}")).ToArray());

        await MakeSut().HandleAsync(new SyncSpellsCommand());

        LookedUpFileDataIds().Should().BeEquivalentTo(Enumerable.Range(1201, 400));
        _upsertedRows.Single(r => r.SpellId == 1).IconUrl.Should().Be(IconBaseUrl + "listed_1.jpg");
        _upsertedRows.Single(r => r.SpellId == 600).IconUrl.Should().Be(IconBaseUrl + "icon_1600.jpg");
        _upsertedRows.Should().OnlyContain(r => r.IconUrl.Length > 0);
        _logger.Entries.Should().NotContain(e => e.Message.Contains("missing from the listfile"));
    }

    [Fact]
    public async Task HandleAsync_IndividualLookupFailsWithHttpError_LeavesThatIconEmptyLogsAWarningAndKeepsTheListfileIcons()
    {
        SetupBranches(MakeBranch(5, "Forever", ForeverProduct, ForeverExpansionId));
        SetupLatestBuilds((ForeverProduct, "b1"));
        SetupNames("b1", "enUS", (1, "Listed"), (2, "Lookup ok"), (3, "Lookup fails"));
        SetupNames("b1", "frFR");
        SetupNames("b1", "deDE");
        SetupIcons("b1", new Dictionary<int, int> { [1] = 500, [2] = 600, [3] = 700 });
        SetupListfile((500, "listed_icon"));
        SetupFileName(600, "b1", "interface/icons/ok_icon.blp");
        _wago.Setup(w => w.GetFileNameAsync(700, "b1", It.IsAny<CancellationToken>())).ThrowsAsync(new HttpRequestException("400"));

        var result = await MakeSut().HandleAsync(new SyncSpellsCommand());

        result.IsSuccess.Should().BeTrue();
        _upsertedRows.Single(r => r.SpellId == 1).IconUrl.Should().EndWith("/listed_icon.jpg");
        _upsertedRows.Single(r => r.SpellId == 2).IconUrl.Should().EndWith("/ok_icon.jpg");
        _upsertedRows.Single(r => r.SpellId == 3).IconUrl.Should().BeEmpty();
        _logger.Entries.Should().ContainSingle(e => e.Level == LogLevel.Warning && e.Exception is HttpRequestException && e.Message.Contains("700") && e.Message.Contains("b1"));
        _branches.Verify(b => b.UpdateSyncStateAsync(5, "b1", BuildDate, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_ManyIndividualLookupsFail_SyncStillCompletesWithEmptyIcons()
    {
        SetupManySpellsWithUnlistedIcons("b1", 40);
        _wago.Setup(w => w.GetFileNameAsync(It.IsAny<int>(), "b1", It.IsAny<CancellationToken>())).ThrowsAsync(new HttpRequestException("429"));

        var result = await MakeSut().HandleAsync(new SyncSpellsCommand());

        result.IsSuccess.Should().BeTrue();
        _upsertedRows.Should().HaveCount(40).And.OnlyContain(r => r.IconUrl == string.Empty);
        _logger.Entries.Count(e => e.Level == LogLevel.Warning).Should().Be(40);
    }

    [Fact]
    public async Task HandleAsync_TwoBranchesToSync_FetchesTheListfileOnceAndSharesItAcrossBranches()
    {
        SetupBranches(
            MakeBranch(1, "Retail", RetailProduct, RetailExpansionId),
            MakeBranch(5, "Forever", ForeverProduct, ForeverExpansionId));
        SetupLatestBuilds((RetailProduct, "r2"), (ForeverProduct, "f2"));
        SetupNames("r2", "enUS", (1, "Retail Spell"));
        SetupNames("r2", "frFR");
        SetupNames("r2", "deDE");
        SetupIcons("r2", new Dictionary<int, int> { [1] = 500 });
        SetupNames("f2", "enUS", (2, "Forever Spell"), (3, "Forever New Icon"));
        SetupNames("f2", "frFR");
        SetupNames("f2", "deDE");
        SetupIcons("f2", new Dictionary<int, int> { [2] = 500, [3] = 900 });
        SetupListfile((500, "shared_icon"));
        SetupFileName(900, "f2", "interface/icons/forever_only.blp");

        var result = await MakeSut().HandleAsync(new SyncSpellsCommand());

        result.IsSuccess.Should().BeTrue();
        _wago.Verify(w => w.GetIconFileNamesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _upsertBatches.Should().HaveCount(2);
        _upsertBatches[0].Single().IconUrl.Should().Be(IconBaseUrl + "shared_icon.jpg");
        _upsertBatches[1].Single(r => r.SpellId == 2).IconUrl.Should().Be(IconBaseUrl + "shared_icon.jpg");
        _upsertBatches[1].Single(r => r.SpellId == 3).IconUrl.Should().Be(IconBaseUrl + "forever_only.jpg");
        // Individual lookups use the build of the branch being synced.
        _wago.Verify(w => w.GetFileNameAsync(900, "f2", It.IsAny<CancellationToken>()), Times.Once);
        _wago.Verify(w => w.GetFileNameAsync(It.IsAny<int>(), "r2", It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_OnlyOneOfTwoBranchesNeedsSyncing_FetchesTheListfileOnce()
    {
        SetupBranches(
            MakeBranch(1, "Retail", RetailProduct, RetailExpansionId, lastBuild: "r1"),
            MakeBranch(5, "Forever", ForeverProduct, ForeverExpansionId, lastBuild: "f1"));
        SetupLatestBuilds((RetailProduct, "r1"), (ForeverProduct, "f2"));
        SetupEmptyBuildContent("f2");

        await MakeSut().HandleAsync(new SyncSpellsCommand());

        _wago.Verify(w => w.GetIconFileNamesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_EveryBranchSkippedAsUnchanged_NeverFetchesTheListfile()
    {
        SetupBranches(
            MakeBranch(1, "Retail", RetailProduct, RetailExpansionId, lastBuild: "r1"),
            MakeBranch(5, "Forever", ForeverProduct, ForeverExpansionId, lastBuild: "f1"));
        SetupLatestBuilds((RetailProduct, "r1"), (ForeverProduct, "f1"));

        var result = await MakeSut().HandleAsync(new SyncSpellsCommand { Force = false });

        Results(result.Value!).Should().OnlyContain(r => r.Skipped);
        _wago.Verify(w => w.GetIconFileNamesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_ForcedSyncOfAnUnchangedBuild_FetchesTheListfile()
    {
        SetupBranches(MakeBranch(5, "Forever", ForeverProduct, ForeverExpansionId, lastBuild: "f1"));
        SetupLatestBuilds((ForeverProduct, "f1"));
        SetupEmptyBuildContent("f1");

        await MakeSut().HandleAsync(new SyncSpellsCommand { Force = true });

        _wago.Verify(w => w.GetIconFileNamesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_ListfileFetchFails_PropagatesAndNeitherUpsertsNorUpdatesTheSyncState()
    {
        SetupBranches(
            MakeBranch(1, "Retail", RetailProduct, RetailExpansionId),
            MakeBranch(5, "Forever", ForeverProduct, ForeverExpansionId));
        SetupLatestBuilds((RetailProduct, "r2"), (ForeverProduct, "f2"));
        SetupEmptyBuildContent("r2");
        SetupEmptyBuildContent("f2");
        _wago.Setup(w => w.GetIconFileNamesAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new HttpRequestException("listfile host is down"));

        var act = () => MakeSut().HandleAsync(new SyncSpellsCommand());

        await act.Should().ThrowAsync<HttpRequestException>().WithMessage("*listfile host is down*");
        _spells.Verify(s => s.UpsertAsync(It.IsAny<IEnumerable<SpellAvailability>>(), It.IsAny<CancellationToken>()), Times.Never);
        _branches.Verify(b => b.UpdateSyncStateAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
        _discord.VerifyGet(d => d.Messages, Times.Never);
    }

    [Fact]
    public async Task HandleAsync_ListfileTaskFaultsAsynchronously_PropagatesToo()
    {
        SetupBranches(MakeBranch(5, "Forever", ForeverProduct, ForeverExpansionId));
        SetupLatestBuilds((ForeverProduct, "f2"));
        SetupEmptyBuildContent("f2");
        _wago.Setup(w => w.GetIconFileNamesAsync(It.IsAny<CancellationToken>())).Returns(async () =>
        {
            await Task.Yield();
            throw new InvalidOperationException("Wago:ListfileUrl is not configured.");
        });

        var act = () => MakeSut().HandleAsync(new SyncSpellsCommand());

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Wago:ListfileUrl*");
        _spells.Verify(s => s.UpsertAsync(It.IsAny<IEnumerable<SpellAvailability>>(), It.IsAny<CancellationToken>()), Times.Never);
        _branches.Verify(b => b.UpdateSyncStateAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_UpsertedRows_AreALazySequenceOnlyEnumeratedByTheRepository()
    {
        SetupBranches(MakeBranch(5, "Forever", ForeverProduct, ForeverExpansionId));
        SetupLatestBuilds((ForeverProduct, "b1"));
        SetupNames("b1", "enUS", (1, "A"), (2, "B"));
        SetupNames("b1", "frFR");
        SetupNames("b1", "deDE");
        SetupIcons("b1", []);
        IEnumerable<SpellAvailability>? received = null;
        _spells.Setup(s => s.UpsertAsync(It.IsAny<IEnumerable<SpellAvailability>>(), It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<SpellAvailability>, CancellationToken>((rows, _) => received = rows)
            .ReturnsAsync(new SpellSyncDiff());

        await MakeSut().HandleAsync(new SyncSpellsCommand());

        received.Should().NotBeNull();
        received.Should().NotBeAssignableTo<ICollection<SpellAvailability>>();
        received!.Select(r => r.SpellId).Should().Equal(1, 2);
    }

    // ── Discord notification ─────────────────────────────────────────────────

    private DiscordEmbedContent? _sentEmbed;
    private ulong? _sentChannelId;

    private void CaptureEmbed()
        => _messages.Setup(m => m.SendEmbedAsync(It.IsAny<ulong>(), It.IsAny<DiscordEmbedContent>(), It.IsAny<CancellationToken>()))
            .Callback<ulong, DiscordEmbedContent, CancellationToken>((channel, embed, _) =>
            {
                _sentChannelId = channel;
                _sentEmbed = embed;
            })
            .Returns(Task.CompletedTask);

    private static SpellSyncEntry Entry(int id, string name, string? previous = null)
        => new() { SpellId = id, NameEn = name, PreviousNameEn = previous };

    [Fact]
    public async Task HandleAsync_AddedSpells_SendsOneEmbedToTheConfiguredChannel()
    {
        SetupBranches(MakeBranch(5, "Forever", ForeverProduct, ForeverExpansionId));
        SetupLatestBuilds((ForeverProduct, "b2"));
        SetupEmptyBuildContent("b2");
        SetupDiff(new SpellSyncDiff { Added = [Entry(1, "Fireball"), Entry(2, "Frostbolt")] });
        CaptureEmbed();

        await MakeSut().HandleAsync(new SyncSpellsCommand());

        _messages.Verify(m => m.SendEmbedAsync(It.IsAny<ulong>(), It.IsAny<DiscordEmbedContent>(), It.IsAny<CancellationToken>()), Times.Once);
        _sentChannelId.Should().Be(1234567890UL);
        _sentEmbed!.Title.Should().Be("Spell data synced");
        _sentEmbed.Description.Should().Be("1 branch(es) had spell changes this run.");
        var field = _sentEmbed.Fields.Should().ContainSingle().Subject;
        field.Name.Should().Be("Forever · b2");
        field.Value.Should().Be("**2** added, **0** renamed\nAdded: Fireball, Frostbolt");
    }

    [Fact]
    public async Task HandleAsync_RenamedOnly_SendsEmbedWithRenameSample()
    {
        SetupBranches(MakeBranch(5, "Forever", ForeverProduct, ForeverExpansionId, lastBuild: "b1"));
        SetupLatestBuilds((ForeverProduct, "b2"));
        SetupEmptyBuildContent("b2");
        SetupDiff(new SpellSyncDiff { Renamed = [Entry(1, "New Name", "Old Name")] });
        CaptureEmbed();

        await MakeSut().HandleAsync(new SyncSpellsCommand());

        var field = _sentEmbed!.Fields!.Single();
        field.Name.Should().Be("Forever · b1 → b2");
        field.Value.Should().Be("**0** added, **1** renamed\nRenamed: Old Name → New Name");
    }

    [Fact]
    public async Task HandleAsync_ForcedResyncOfSameBuildWithChanges_FieldNameShowsOnlyTheBuild()
    {
        SetupBranches(MakeBranch(5, "Forever", ForeverProduct, ForeverExpansionId, lastBuild: "b2"));
        SetupLatestBuilds((ForeverProduct, "b2"));
        SetupEmptyBuildContent("b2");
        SetupDiff(new SpellSyncDiff { Renamed = [Entry(1, "New", "Old")] });
        CaptureEmbed();

        await MakeSut().HandleAsync(new SyncSpellsCommand { Force = true });

        _sentEmbed!.Fields!.Single().Name.Should().Be("Forever · b2");
    }

    [Fact]
    public async Task HandleAsync_MoreThanTenChanges_SamplesTenAndReportsTheRest()
    {
        SetupBranches(MakeBranch(5, "Forever", ForeverProduct, ForeverExpansionId));
        SetupLatestBuilds((ForeverProduct, "b2"));
        SetupEmptyBuildContent("b2");
        SetupDiff(new SpellSyncDiff
        {
            Added = Enumerable.Range(1, 12).Select(i => Entry(i, $"Spell{i}")).ToList(),
            Renamed = Enumerable.Range(1, 10).Select(i => Entry(100 + i, $"New{i}", $"Old{i}")).ToList(),
        });
        CaptureEmbed();

        await MakeSut().HandleAsync(new SyncSpellsCommand());

        var lines = _sentEmbed!.Fields!.Single().Value.Split('\n');
        lines[0].Should().Be("**12** added, **10** renamed");
        lines[1].Should().StartWith("Added: Spell1, Spell2").And.Contain("Spell10").And.NotContain("Spell11").And.EndWith(" (+2 more)");
        // Exactly ten entries is not "more than ten" — no suffix.
        lines[2].Should().Contain("Old10 → New10").And.NotContain("more");
    }

    [Fact]
    public async Task HandleAsync_TwoBranchesChanged_OneFieldEachInOneEmbed()
    {
        SetupBranches(
            MakeBranch(1, "Retail", RetailProduct, RetailExpansionId),
            MakeBranch(5, "Forever", ForeverProduct, ForeverExpansionId));
        SetupLatestBuilds((RetailProduct, "r2"), (ForeverProduct, "f2"));
        SetupEmptyBuildContent("r2");
        SetupEmptyBuildContent("f2");
        SetupDiff(new SpellSyncDiff { Added = [Entry(1, "X")] });
        CaptureEmbed();

        await MakeSut().HandleAsync(new SyncSpellsCommand());

        _messages.Verify(m => m.SendEmbedAsync(It.IsAny<ulong>(), It.IsAny<DiscordEmbedContent>(), It.IsAny<CancellationToken>()), Times.Once);
        _sentEmbed!.Fields!.Select(f => f.Name).Should().Equal("Retail · r2", "Forever · f2");
        _sentEmbed.Description.Should().Be("2 branch(es) had spell changes this run.");
    }

    [Fact]
    public async Task HandleAsync_NoChanges_DoesNotSendAnything()
    {
        SetupBranches(MakeBranch(5, "Forever", ForeverProduct, ForeverExpansionId));
        SetupLatestBuilds((ForeverProduct, "b2"));
        SetupEmptyBuildContent("b2");

        var result = await MakeSut().HandleAsync(new SyncSpellsCommand());

        result.IsSuccess.Should().BeTrue();
        _discord.VerifyGet(d => d.Messages, Times.Never);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task HandleAsync_ChannelIdNotConfigured_SkipsSendWithWarningAndStillSucceeds(string? channelId)
    {
        _config = new Dictionary<string, string?> { ["Discord:SpellSyncChannelId"] = channelId, ["Blizzard:SpellIconBaseUrl"] = IconBaseUrl };
        SetupBranches(MakeBranch(5, "Forever", ForeverProduct, ForeverExpansionId));
        SetupLatestBuilds((ForeverProduct, "b2"));
        SetupEmptyBuildContent("b2");
        SetupDiff(new SpellSyncDiff { Added = [Entry(1, "Fireball")] });

        var result = await MakeSut().HandleAsync(new SyncSpellsCommand());

        result.IsSuccess.Should().BeTrue();
        _discord.VerifyGet(d => d.Messages, Times.Never);
        _logger.Entries.Should().Contain(e => e.Level == LogLevel.Warning && e.Message.Contains("SpellSyncChannelId"));
    }

    [Fact]
    public async Task HandleAsync_SendingTheEmbedThrows_IsSwallowedWithWarningAndSyncStillSucceeds()
    {
        SetupBranches(MakeBranch(5, "Forever", ForeverProduct, ForeverExpansionId));
        SetupLatestBuilds((ForeverProduct, "b2"));
        SetupEmptyBuildContent("b2");
        SetupDiff(new SpellSyncDiff { Added = [Entry(1, "Fireball")] });
        _messages.Setup(m => m.SendEmbedAsync(It.IsAny<ulong>(), It.IsAny<DiscordEmbedContent>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Missing Access"));

        var result = await MakeSut().HandleAsync(new SyncSpellsCommand());

        result.IsSuccess.Should().BeTrue();
        _branches.Verify(b => b.UpdateSyncStateAsync(5, "b2", BuildDate, It.IsAny<CancellationToken>()), Times.Once);
        _logger.Entries.Should().Contain(e => e.Level == LogLevel.Warning && e.Exception is InvalidOperationException);
    }

    [Fact]
    public async Task HandleAsync_UnparseableChannelId_IsSwallowedLikeAnyOtherNotificationFailure()
    {
        _config = new Dictionary<string, string?> { ["Discord:SpellSyncChannelId"] = "not-a-number", ["Blizzard:SpellIconBaseUrl"] = IconBaseUrl };
        SetupBranches(MakeBranch(5, "Forever", ForeverProduct, ForeverExpansionId));
        SetupLatestBuilds((ForeverProduct, "b2"));
        SetupEmptyBuildContent("b2");
        SetupDiff(new SpellSyncDiff { Added = [Entry(1, "Fireball")] });

        var result = await MakeSut().HandleAsync(new SyncSpellsCommand());

        result.IsSuccess.Should().BeTrue();
        _logger.Entries.Should().Contain(e => e.Level == LogLevel.Warning && e.Exception is FormatException);
    }

    [Fact]
    public async Task HandleAsync_SendingTheEmbedIsCancelled_DoesNotSwallowTheCancellation()
    {
        SetupBranches(MakeBranch(5, "Forever", ForeverProduct, ForeverExpansionId));
        SetupLatestBuilds((ForeverProduct, "b2"));
        SetupEmptyBuildContent("b2");
        SetupDiff(new SpellSyncDiff { Added = [Entry(1, "Fireball")] });
        _messages.Setup(m => m.SendEmbedAsync(It.IsAny<ulong>(), It.IsAny<DiscordEmbedContent>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        var act = () => MakeSut().HandleAsync(new SyncSpellsCommand());

        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}

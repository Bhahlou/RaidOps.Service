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

    public SyncSpellsCommandHandlerTests()
    {
        _discord.SetupGet(d => d.Messages).Returns(_messages.Object);

        _spells.Setup(s => s.UpsertAsync(It.IsAny<IEnumerable<SpellAvailability>>(), It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<SpellAvailability>, CancellationToken>((rows, _) =>
            {
                _callOrder.Add("upsert");
                _upsertedRows = rows.ToList();
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

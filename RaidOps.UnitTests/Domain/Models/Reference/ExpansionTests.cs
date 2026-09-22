using FluentAssertions;
using RaidOps.Domain.Models.Reference;

namespace RaidOps.UnitTests.Domain.Models.Reference;

/// <summary>
/// Unit tests for <see cref="Expansion.IsContentAvailableFrom"/> — the rule that decides whether
/// content first introduced on one expansion (e.g. a class's <c>FirstExpansionId</c>) should be
/// considered available on another, correctly handling a forked branch like "Forever" that started
/// from Classic content rather than continuing the mainline chronology.
/// </summary>
public class ExpansionTests
{
    private static Expansion Mainline(int id, int releaseOrder) => new() { Id = id, Name = $"Exp{id}", ShortCode = $"E{id}", ReleaseOrder = releaseOrder };

    private static Expansion Forked(int id, int releaseOrder, int forkedFromExpansionId) =>
        new() { Id = id, Name = $"Exp{id}", ShortCode = $"E{id}", ReleaseOrder = releaseOrder, ForkedFromExpansionId = forkedFromExpansionId };

    [Fact]
    public void IsContentAvailableFrom_SameMainlineChronology_EarlierOriginIsAvailable()
    {
        var classic = Mainline(1, 1);
        var tbc = Mainline(2, 2);

        tbc.IsContentAvailableFrom(classic).Should().BeTrue();
    }

    [Fact]
    public void IsContentAvailableFrom_SameMainlineChronology_LaterOriginIsNotAvailable()
    {
        var classic = Mainline(1, 1);
        var tbc = Mainline(2, 2);

        classic.IsContentAvailableFrom(tbc).Should().BeFalse();
    }

    [Fact]
    public void IsContentAvailableFrom_ForkedBranch_ContentFromItsOwnForkPoint_IsAvailable()
    {
        var classic = Mainline(1, 1);
        var forever = Forked(12, 12, forkedFromExpansionId: 1);

        forever.IsContentAvailableFrom(classic).Should().BeTrue();
    }

    [Fact]
    public void IsContentAvailableFrom_ForkedBranch_MainlineContentAddedAfterTheForkPoint_IsNotAvailable()
    {
        // Regression: a plain "origin.ReleaseOrder <= target.ReleaseOrder" cutoff would wrongly
        // include this (2 <= 12) — WotLK is on the mainline chronology, not on Forever's fork.
        var wotlk = Mainline(3, 3);
        var forever = Forked(12, 12, forkedFromExpansionId: 1);

        forever.IsContentAvailableFrom(wotlk).Should().BeFalse();
    }

    [Fact]
    public void IsContentAvailableFrom_ForkedBranch_OwnSubsequentContent_FollowsChronologyWithinTheFork()
    {
        // A hypothetical future Forever-exclusive expansion, sharing the same fork point.
        var forever = Forked(12, 12, forkedFromExpansionId: 1);
        var foreverSequel = Forked(13, 13, forkedFromExpansionId: 1);

        foreverSequel.IsContentAvailableFrom(forever).Should().BeTrue();
        forever.IsContentAvailableFrom(foreverSequel).Should().BeFalse();
    }

    [Fact]
    public void IsContentAvailableFrom_TwoDifferentForks_NeitherIsTheOthersForkPoint_IsNotAvailable()
    {
        var forever = Forked(12, 12, forkedFromExpansionId: 1);
        var otherBranch = Forked(20, 1, forkedFromExpansionId: 5);

        forever.IsContentAvailableFrom(otherBranch).Should().BeFalse();
    }
}

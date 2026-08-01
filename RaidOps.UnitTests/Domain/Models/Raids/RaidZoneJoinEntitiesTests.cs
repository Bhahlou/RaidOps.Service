using FluentAssertions;
using RaidOps.Domain.Models.Discord;
using RaidOps.Domain.Models.Raids;
using RaidOps.Domain.Models.Reference;

namespace RaidOps.UnitTests.Domain.Models.Raids;

/// <summary>
/// Covers plain EF Core relationship-plumbing navigation properties on the raid builder's Domain
/// models that have no application-layer reader of their own (every repository query navigates
/// the other way — event/series → zone, zone → expansion is read once at seed time, series/event
/// → guild is never dereferenced by a handler).
/// </summary>
public class RaidZoneJoinEntitiesTests
{
    [Fact]
    public void RaidEventZone_RaidEventNavigation_CanBeSetAndRead()
    {
        var raidEvent = new RaidEvent { Id = 1, Name = "Split 1" };

        var eventZone = new RaidEventZone { RaidEventId = raidEvent.Id, RaidZoneId = 7, RaidEvent = raidEvent };

        eventZone.RaidEvent.Should().BeSameAs(raidEvent);
        eventZone.RaidEvent.Name.Should().Be("Split 1");
    }

    [Fact]
    public void RaidSeriesZone_RaidSeriesNavigation_CanBeSetAndRead()
    {
        var series = new RaidSeries { Id = 1, Name = "Split 1" };

        var seriesZone = new RaidSeriesZone { RaidSeriesId = series.Id, RaidZoneId = 7, RaidSeries = series };

        seriesZone.RaidSeries.Should().BeSameAs(series);
        seriesZone.RaidSeries.Name.Should().Be("Split 1");
    }

    [Fact]
    public void RaidEvent_RaidSeriesNavigation_CanBeSetAndRead()
    {
        var series = new RaidSeries { Id = 1, Name = "Split 1" };

        var raidEvent = new RaidEvent { Id = 1, Name = "Split 1 — Feb 4", RaidSeriesId = series.Id, RaidSeries = series };

        raidEvent.RaidSeries.Should().BeSameAs(series);
        raidEvent.RaidSeries!.Name.Should().Be("Split 1");
    }

    [Fact]
    public void RaidZone_ExpansionNavigation_CanBeSetAndRead()
    {
        var expansion = new Expansion { Id = 2, Name = "The Burning Crusade", ShortCode = "TBC" };

        var zone = new RaidZone { Id = 4, Name = "Serpentshrine Cavern", ExpansionId = expansion.Id, Expansion = expansion };

        zone.Expansion.Should().BeSameAs(expansion);
        zone.Expansion.ShortCode.Should().Be("TBC");
    }

    [Fact]
    public void RaidSeries_GuildNavigation_CanBeSetAndRead()
    {
        var guild = new Guild { Id = "guild-1", Name = "Test Guild" };

        var series = new RaidSeries { Id = 1, Name = "Split 1", GuildId = guild.Id, Guild = guild };

        series.Guild.Should().BeSameAs(guild);
        series.Guild.Name.Should().Be("Test Guild");
    }

    [Fact]
    public void RaidEvent_GuildNavigation_CanBeSetAndRead()
    {
        var guild = new Guild { Id = "guild-1", Name = "Test Guild" };

        var raidEvent = new RaidEvent { Id = 1, Name = "Split 1", GuildId = guild.Id, Guild = guild };

        raidEvent.Guild.Should().BeSameAs(guild);
        raidEvent.Guild.Name.Should().Be("Test Guild");
    }
}

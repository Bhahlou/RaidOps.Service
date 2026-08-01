using FluentAssertions;
using RaidOps.Application.Implementations.Raids.Helpers;

namespace RaidOps.UnitTests.Application.Raids.Helpers;

public class GuildTimeHelperTests
{
    [Fact]
    public void ToGuildLocalDateTime_NullTimezone_ReturnsUtcUnchanged()
    {
        var utc = new DateTime(2026, 1, 15, 20, 0, 0, DateTimeKind.Utc);

        var result = GuildTimeHelper.ToGuildLocalDateTime(utc, null);

        result.Should().Be(utc);
    }

    [Fact]
    public void ToGuildLocalDateTime_WhitespaceTimezone_ReturnsUtcUnchanged()
    {
        var utc = new DateTime(2026, 1, 15, 20, 0, 0, DateTimeKind.Utc);

        var result = GuildTimeHelper.ToGuildLocalDateTime(utc, "   ");

        result.Should().Be(utc);
    }

    [Fact]
    public void ToGuildLocalDateTime_UnknownTimezone_FallsBackToUtc()
    {
        var utc = new DateTime(2026, 1, 15, 20, 0, 0, DateTimeKind.Utc);

        var result = GuildTimeHelper.ToGuildLocalDateTime(utc, "Not/A_Real_Zone");

        result.Should().Be(utc);
    }

    [Fact]
    public void ToGuildLocalDateTime_ValidTimezone_ConvertsToLocal()
    {
        // Winter in Paris is UTC+1.
        var utc = new DateTime(2026, 1, 15, 20, 0, 0, DateTimeKind.Utc);

        var result = GuildTimeHelper.ToGuildLocalDateTime(utc, "Europe/Paris");

        result.Should().Be(new DateTime(2026, 1, 15, 21, 0, 0));
    }

    [Fact]
    public void ToGuildLocalDate_ValidTimezone_ReturnsLocalCalendarDate()
    {
        // 23:30 UTC on the 15th becomes 00:30 on the 16th in Paris (winter, UTC+1).
        var utc = new DateTime(2026, 1, 15, 23, 30, 0, DateTimeKind.Utc);

        var result = GuildTimeHelper.ToGuildLocalDate(utc, "Europe/Paris");

        result.Should().Be(new DateOnly(2026, 1, 16));
    }

    [Fact]
    public void FromGuildLocal_NullTimezone_TreatsLocalAsUtc()
    {
        var local = new DateTime(2026, 1, 15, 20, 0, 0);

        var result = GuildTimeHelper.FromGuildLocal(local, null);

        result.Should().Be(DateTime.SpecifyKind(local, DateTimeKind.Utc));
        result.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact]
    public void FromGuildLocal_WhitespaceTimezone_TreatsLocalAsUtc()
    {
        var local = new DateTime(2026, 1, 15, 20, 0, 0);

        var result = GuildTimeHelper.FromGuildLocal(local, "  ");

        result.Should().Be(DateTime.SpecifyKind(local, DateTimeKind.Utc));
    }

    [Fact]
    public void FromGuildLocal_UnknownTimezone_FallsBackToTreatingLocalAsUtc()
    {
        var local = new DateTime(2026, 1, 15, 20, 0, 0);

        var result = GuildTimeHelper.FromGuildLocal(local, "Not/A_Real_Zone");

        result.Should().Be(DateTime.SpecifyKind(local, DateTimeKind.Utc));
    }

    [Fact]
    public void FromGuildLocal_ValidTimezone_ConvertsToUtc()
    {
        // 21:00 local Paris time in winter (UTC+1) is 20:00 UTC.
        var local = new DateTime(2026, 1, 15, 21, 0, 0);

        var result = GuildTimeHelper.FromGuildLocal(local, "Europe/Paris");

        result.Should().Be(new DateTime(2026, 1, 15, 20, 0, 0, DateTimeKind.Utc));
        result.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact]
    public void RoundTrip_FromGuildLocalThenToGuildLocalDateTime_ReturnsOriginalLocal()
    {
        var local = new DateTime(2026, 6, 10, 21, 0, 0);

        var utc = GuildTimeHelper.FromGuildLocal(local, "Europe/Paris");
        var backToLocal = GuildTimeHelper.ToGuildLocalDateTime(utc, "Europe/Paris");

        backToLocal.Should().Be(local);
    }
}

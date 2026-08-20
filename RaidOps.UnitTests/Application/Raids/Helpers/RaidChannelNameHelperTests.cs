using FluentAssertions;
using RaidOps.Application.Implementations.Raids.Helpers;

namespace RaidOps.UnitTests.Application.Raids.Helpers;

public class RaidChannelNameHelperTests
{
    private static readonly DateOnly Tuesday = new(2026, 8, 18);

    [Fact]
    public void BuildChannelName_English_UsesShortWeekdayDayMonth()
    {
        var result = RaidChannelNameHelper.BuildChannelName("Kara", Tuesday, "en");

        result.Should().Be("kara-tue-18-aug");
    }

    [Fact]
    public void BuildChannelName_French_UsesFrenchAbbreviations()
    {
        var result = RaidChannelNameHelper.BuildChannelName("Kara", Tuesday, "fr");

        result.Should().Be("kara-mar-18-août");
    }

    [Fact]
    public void BuildChannelName_German_UsesGermanAbbreviations()
    {
        var result = RaidChannelNameHelper.BuildChannelName("Kara", Tuesday, "de");

        result.Should().Be("kara-di-18-aug");
    }

    [Fact]
    public void BuildChannelName_UnknownLanguage_FallsBackToEnglish()
    {
        var result = RaidChannelNameHelper.BuildChannelName("Kara", Tuesday, "es");

        result.Should().Be("kara-tue-18-aug");
    }

    [Fact]
    public void BuildChannelName_NullLanguage_FallsBackToEnglish()
    {
        var result = RaidChannelNameHelper.BuildChannelName("Kara", Tuesday, null);

        result.Should().Be("kara-tue-18-aug");
    }

    [Fact]
    public void BuildChannelName_NameWithSpacesAndMixedCase_IsSlugified()
    {
        var result = RaidChannelNameHelper.BuildChannelName("Split 1 - SSC/TK", Tuesday, "en");

        result.Should().Be("split-1-ssctk-tue-18-aug");
    }

    [Fact]
    public void BuildChannelName_VeryLongName_TruncatedToOneHundredChars()
    {
        var longName = new string('a', 200);

        var result = RaidChannelNameHelper.BuildChannelName(longName, Tuesday, "en");

        result.Length.Should().Be(100);
    }

    [Fact]
    public void BuildChannelName_EmptyName_StillProducesTheDatePart()
    {
        var result = RaidChannelNameHelper.BuildChannelName("", Tuesday, "en");

        result.Should().Be("tue-18-aug");
    }
}

using FluentAssertions;
using RaidOps.Application.Implementations.Guilds.Registration.Helpers;

namespace RaidOps.UnitTests.Application.Guilds.Registration.Helpers;

public class DiscordLocaleMapperTests
{
    [Theory]
    [InlineData(null, "en")]
    [InlineData("", "en")]
    [InlineData("en", "en")]
    [InlineData("fr", "fr")]
    [InlineData("de", "de")]
    [InlineData("en-US", "en")]
    [InlineData("fr-FR", "fr")]
    [InlineData("de-DE", "de")]
    [InlineData("pt-BR", "en")]
    [InlineData("es", "en")]
    [InlineData("FR", "fr")]
    public void ToAppLanguage_MapsToSupportedLanguageOrFallsBackToEnglish(string? discordLocale, string expected)
    {
        DiscordLocaleMapper.ToAppLanguage(discordLocale).Should().Be(expected);
    }
}

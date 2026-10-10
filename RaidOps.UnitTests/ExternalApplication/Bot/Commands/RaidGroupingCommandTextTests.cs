using FluentAssertions;
using RaidOps.Application.Contracts.Common;
using RaidOps.ExternalApplication.Implementations.Bot.Commands;

namespace RaidOps.UnitTests.ExternalApplication.Bot.Commands;

public class RaidGroupingCommandTextTests
{
    // ── InvalidRaidSelection ──────────────────────────────────────────────────

    [Theory]
    [InlineData("fr", "❌ Sélection de raid invalide — réessaie en choisissant une suggestion de l'autocomplétion.")]
    [InlineData("de", "❌ Ungültige Raid-Auswahl — versuche es erneut, indem du einen Autovervollständigungsvorschlag auswählst.")]
    [InlineData("en", "❌ Invalid raid selection — try again by picking an autocomplete suggestion.")]
    [InlineData("es", "❌ Invalid raid selection — try again by picking an autocomplete suggestion.")]
    public void InvalidRaidSelection_ReturnsLocalizedMessage(string language, string expected)
    {
        RaidGroupingCommandText.InvalidRaidSelection(language).Should().Be(expected);
    }

    // ── Result — success ──────────────────────────────────────────────────────

    [Theory]
    [InlineData("fr", "✅ Message de groupage envoyé !")]
    [InlineData("de", "✅ Gruppierungsnachricht gesendet!")]
    [InlineData("en", "✅ Grouping message sent!")]
    public void Result_Success_ReturnsLocalizedSuccessMessage(string language, string expected)
    {
        RaidGroupingCommandText.Result(success: true, errorCode: null, language).Should().Be(expected);
    }

    [Fact]
    public void Result_Success_IgnoresErrorCode()
    {
        // A success result should never surface a failure reason even if one was somehow passed.
        RaidGroupingCommandText.Result(success: true, errorCode: ResponseDetail.Forbidden, "en")
            .Should().Be("✅ Grouping message sent!");
    }

    // ── Result — failure, known codes ─────────────────────────────────────────

    [Theory]
    [InlineData(ResponseDetail.Forbidden, "fr", "❌ tu dois être officier de cette guilde pour utiliser cette commande.")]
    [InlineData(ResponseDetail.Forbidden, "de", "❌ du musst Offizier dieser Gilde sein, um diesen Befehl zu verwenden.")]
    [InlineData(ResponseDetail.Forbidden, "en", "❌ you must be an officer of this guild to use this command.")]
    [InlineData(ResponseDetail.RaidEventNotFound, "en", "❌ this raid no longer exists.")]
    [InlineData(ResponseDetail.RaidEventNotPublished, "en", "❌ this raid must be published before grouping.")]
    [InlineData(ResponseDetail.NoAnnouncementChannelConfigured, "en", "❌ no announcement channel is configured for this branch.")]
    [InlineData(ResponseDetail.NoAssignmentsToNotify, "en", "❌ this raid has no assigned players.")]
    [InlineData(ResponseDetail.RaidGroupingCharacterNotFound, "en", "❌ no assigned character with that name was found in this raid.")]
    public void Result_Failure_KnownErrorCode_ReturnsLocalizedReason(string errorCode, string language, string expected)
    {
        RaidGroupingCommandText.Result(success: false, errorCode, language).Should().Be(expected);
    }

    [Fact]
    public void Result_Failure_RequesterHasNoCharacter_QuotesLocalizedParameterName()
    {
        // The quoted parameter name must match what each locale's fr.json/de.json Discord
        // localization file renames the "character" slash-command option to.
        RaidGroupingCommandText.Result(false, ResponseDetail.RaidGroupingRequesterHasNoCharacter, "fr")
            .Should().Contain("\"personnage\"");
        RaidGroupingCommandText.Result(false, ResponseDetail.RaidGroupingRequesterHasNoCharacter, "de")
            .Should().Contain("\"charakter\"");
        RaidGroupingCommandText.Result(false, ResponseDetail.RaidGroupingRequesterHasNoCharacter, "en")
            .Should().Contain("\"character\"");
    }

    // ── Result — failure, unknown/null codes ──────────────────────────────────

    [Theory]
    [InlineData("fr", "❌ une erreur inattendue est survenue.")]
    [InlineData("de", "❌ ein unerwarteter Fehler ist aufgetreten.")]
    [InlineData("en", "❌ an unexpected error occurred.")]
    public void Result_Failure_UnrecognizedErrorCode_FallsBackToGenericReason(string language, string expected)
    {
        RaidGroupingCommandText.Result(false, "SomeUnmappedCode", language).Should().Be(expected);
    }

    [Fact]
    public void Result_Failure_NullErrorCode_FallsBackToGenericReason()
    {
        RaidGroupingCommandText.Result(false, null, "en").Should().Be("❌ an unexpected error occurred.");
    }

    [Fact]
    public void Result_Failure_UnsupportedLanguage_FallsBackToEnglish()
    {
        RaidGroupingCommandText.Result(false, ResponseDetail.RaidEventNotFound, "es").Should().Be("❌ this raid no longer exists.");
    }
}

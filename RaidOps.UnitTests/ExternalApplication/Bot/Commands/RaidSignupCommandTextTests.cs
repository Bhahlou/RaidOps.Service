using FluentAssertions;
using RaidOps.Application.Contracts.Common;
using RaidOps.ExternalApplication.Implementations.Bot.Commands;

namespace RaidOps.UnitTests.ExternalApplication.Bot.Commands;

public class RaidSignupCommandTextTests
{
    // ── Result — success ──────────────────────────────────────────────────────

    [Theory]
    [InlineData("fr", "✅ Réponse enregistrée !")]
    [InlineData("de", "✅ Antwort gespeichert!")]
    [InlineData("en", "✅ Response saved!")]
    public void Result_Success_ReturnsLocalizedSuccessMessage(string language, string expected)
    {
        RaidSignupCommandText.Result(success: true, errorCode: null, language, frontendUrl: null).Should().Be(expected);
    }

    [Fact]
    public void Result_Success_IgnoresErrorCode()
    {
        RaidSignupCommandText.Result(success: true, errorCode: ResponseDetail.Forbidden, "en", frontendUrl: null)
            .Should().Be("✅ Response saved!");
    }

    // ── Result — failure, known codes ─────────────────────────────────────────

    [Theory]
    [InlineData(ResponseDetail.RaidEventNotFound, "en", "❌ this raid no longer exists.")]
    [InlineData(ResponseDetail.RaidEventNotFound, "fr", "❌ ce raid n'existe plus.")]
    [InlineData(ResponseDetail.RaidEventNotFound, "de", "❌ dieser Raid existiert nicht mehr.")]
    [InlineData(ResponseDetail.RaidEventNotInSignupMode, "en", "❌ this raid is not in signup mode.")]
    [InlineData(ResponseDetail.RaidEventNotInSignupMode, "fr", "❌ ce raid n'est pas en mode inscription.")]
    [InlineData(ResponseDetail.RaidEventNotInSignupMode, "de", "❌ dieser Raid ist nicht im Anmeldemodus.")]
    [InlineData(ResponseDetail.CharacterNotFound, "en", "❌ this character no longer exists on RaidOps. Check your imported characters: https://app/characters")]
    [InlineData(ResponseDetail.CharacterNotFound, "fr", "❌ ce personnage n'existe plus sur RaidOps. Vérifie tes personnages importés : https://app/characters")]
    [InlineData(ResponseDetail.CharacterNotFound, "de", "❌ dieser Charakter existiert nicht mehr auf RaidOps. Überprüfe deine importierten Charaktere: https://app/characters")]
    [InlineData(ResponseDetail.CharacterNotOnRoster, "en", "❌ this character is no longer on this branch's roster.")]
    [InlineData(ResponseDetail.CharacterNotOnRoster, "fr", "❌ ce personnage n'est plus membre du roster de cette branche.")]
    [InlineData(ResponseDetail.CharacterNotOnRoster, "de", "❌ dieser Charakter ist nicht mehr Mitglied des Rosters dieses Branches.")]
    public void Result_Failure_KnownErrorCode_ReturnsLocalizedReason(string errorCode, string language, string expected)
    {
        RaidSignupCommandText.Result(success: false, errorCode, language, frontendUrl: "https://app").Should().Be(expected);
    }

    [Theory]
    [InlineData("en", "❌ you must be on this guild branch's roster to respond. Not registered on RaidOps yet? https://app/get-started")]
    [InlineData("fr", "❌ tu dois faire partie du roster de cette branche pour répondre. Pas encore inscrit sur RaidOps ? https://app/get-started")]
    [InlineData("de", "❌ du musst Mitglied des Rosters dieses Branches sein, um zu antworten. Noch nicht auf RaidOps registriert? https://app/get-started")]
    public void Result_Failure_Forbidden_ReturnsLocalizedReasonWithGetStartedLink(string language, string expected)
    {
        RaidSignupCommandText.Result(false, ResponseDetail.Forbidden, language, "https://app").Should().Be(expected);
    }

    [Theory]
    [InlineData("en", "❌ you have no character on this branch's roster. Import it on RaidOps: https://app/characters")]
    [InlineData("fr", "❌ tu n'as aucun personnage sur le roster de cette branche. Importe-le sur RaidOps : https://app/characters")]
    [InlineData("de", "❌ du hast keinen Charakter im Roster dieses Branches. Importiere ihn auf RaidOps: https://app/characters")]
    public void Result_Failure_CharacterRequiredForSignup_ReturnsLocalizedReasonWithCharactersImportLink(string language, string expected)
    {
        RaidSignupCommandText.Result(false, ResponseDetail.CharacterRequiredForSignup, language, "https://app").Should().Be(expected);
    }

    // ── Result — failure, SpecRequiredForSignup / SpecNotAvailableForCharacter ─

    [Theory]
    [InlineData("en", "❌ this character has no raid spec configured — set one up on RaidOps: https://app/characters")]
    [InlineData("fr", "❌ ce personnage n'a aucune spécialisation de raid déclarée — configure-la sur RaidOps : https://app/characters")]
    [InlineData("de", "❌ dieser Charakter hat keine Raid-Skillung konfiguriert — richte sie auf RaidOps ein: https://app/characters")]
    public void Result_Failure_SpecRequiredForSignup_NoCharacterProfileUrl_FallsBackToLocalizedCharactersList(string language, string expected)
    {
        RaidSignupCommandText.Result(false, ResponseDetail.SpecRequiredForSignup, language, "https://app", characterProfileUrl: null).Should().Be(expected);
    }

    [Theory]
    [InlineData("en", "❌ this spec isn't declared for this character anymore — update it on RaidOps: https://app/characters")]
    [InlineData("fr", "❌ cette spécialisation n'est plus déclarée pour ce personnage — mets-le à jour sur RaidOps : https://app/characters")]
    [InlineData("de", "❌ diese Skillung ist für diesen Charakter nicht mehr konfiguriert — aktualisiere ihn auf RaidOps: https://app/characters")]
    public void Result_Failure_SpecNotAvailableForCharacter_NoCharacterProfileUrl_FallsBackToLocalizedCharactersList(string language, string expected)
    {
        RaidSignupCommandText.Result(false, ResponseDetail.SpecNotAvailableForCharacter, language, "https://app", characterProfileUrl: null).Should().Be(expected);
    }

    [Fact]
    public void Result_Failure_SpecRequiredForSignup_CharacterProfileUrlGiven_UsesItInsteadOfCharactersList()
    {
        var message = RaidSignupCommandText.Result(false, ResponseDetail.SpecRequiredForSignup, "en", "https://app", characterProfileUrl: "https://app/characters/retail/silvermoon/arthas");

        message.Should().Contain("https://app/characters/retail/silvermoon/arthas");
        message.Should().NotContain("https://app/characters:");
    }

    [Fact]
    public void Result_Failure_SpecNotAvailableForCharacter_CharacterProfileUrlGiven_UsesIt()
    {
        RaidSignupCommandText.Result(false, ResponseDetail.SpecNotAvailableForCharacter, "en", "https://app", characterProfileUrl: "https://app/characters/retail/silvermoon/arthas")
            .Should().Contain("https://app/characters/retail/silvermoon/arthas");
    }

    // ── Result — failure, unknown/null codes ──────────────────────────────────

    [Theory]
    [InlineData("fr", "❌ une erreur inattendue est survenue.")]
    [InlineData("de", "❌ ein unerwarteter Fehler ist aufgetreten.")]
    [InlineData("en", "❌ an unexpected error occurred.")]
    public void Result_Failure_UnrecognizedErrorCode_FallsBackToGenericReason(string language, string expected)
    {
        RaidSignupCommandText.Result(false, "SomeUnmappedCode", language, null).Should().Be(expected);
    }

    [Fact]
    public void Result_Failure_NullErrorCode_FallsBackToGenericReason()
    {
        RaidSignupCommandText.Result(false, null, "en", null).Should().Be("❌ an unexpected error occurred.");
    }

    [Fact]
    public void Result_Failure_UnsupportedLanguage_FallsBackToEnglish()
    {
        RaidSignupCommandText.Result(false, ResponseDetail.RaidEventNotFound, "es", null).Should().Be("❌ this raid no longer exists.");
    }

    // ── InvalidAction ─────────────────────────────────────────────────────────

    [Theory]
    [InlineData("fr", "❌ Action d'inscription invalide.")]
    [InlineData("de", "❌ Ungültige Anmeldeaktion.")]
    [InlineData("en", "❌ Invalid signup action.")]
    [InlineData("es", "❌ Invalid signup action.")]
    public void InvalidAction_ReturnsLocalizedMessage(string language, string expected)
    {
        RaidSignupCommandText.InvalidAction(language).Should().Be(expected);
    }

    // ── NoCharacterSelected / NoSpecSelected ────────────────────────────────────

    [Theory]
    [InlineData("fr", "❌ Aucun personnage sélectionné.")]
    [InlineData("de", "❌ Kein Charakter ausgewählt.")]
    [InlineData("en", "❌ No character selected.")]
    public void NoCharacterSelected_ReturnsLocalizedMessage(string language, string expected)
    {
        RaidSignupCommandText.NoCharacterSelected(language).Should().Be(expected);
    }

    [Theory]
    [InlineData("fr", "❌ Aucune spécialisation sélectionnée.")]
    [InlineData("de", "❌ Keine Skillung ausgewählt.")]
    [InlineData("en", "❌ No spec selected.")]
    public void NoSpecSelected_ReturnsLocalizedMessage(string language, string expected)
    {
        RaidSignupCommandText.NoSpecSelected(language).Should().Be(expected);
    }

    // ── Placeholders ──────────────────────────────────────────────────────────

    [Theory]
    [InlineData("fr", "Choisis ton personnage")]
    [InlineData("de", "Wähle deinen Charakter")]
    [InlineData("en", "Choose your character")]
    public void CharacterSelectPlaceholder_ReturnsLocalizedMessage(string language, string expected)
    {
        RaidSignupCommandText.CharacterSelectPlaceholder(language).Should().Be(expected);
    }

    [Theory]
    [InlineData("fr", "Choisis ta spécialisation")]
    [InlineData("de", "Wähle deine Skillung")]
    [InlineData("en", "Choose your spec")]
    public void SpecSelectPlaceholder_ReturnsLocalizedMessage(string language, string expected)
    {
        RaidSignupCommandText.SpecSelectPlaceholder(language).Should().Be(expected);
    }

    // ── CharacterImportHint / SpecImportHint ────────────────────────────────────

    [Theory]
    [InlineData("fr", "Ton personnage n'est pas dans la liste ? Importe-le sur RaidOps : https://app/characters")]
    [InlineData("de", "Dein Charakter ist nicht in der Liste? Importiere ihn auf RaidOps: https://app/characters")]
    [InlineData("en", "Character not in the list? Import it on RaidOps: https://app/characters")]
    public void CharacterImportHint_ReturnsLocalizedMessage(string language, string expected)
    {
        RaidSignupCommandText.CharacterImportHint("https://app", language).Should().Be(expected);
    }

    [Theory]
    [InlineData("fr", "La spécialisation que tu cherches n'est pas dans la liste ? Déclare-la sur RaidOps : https://app/characters")]
    [InlineData("de", "Die gesuchte Skillung ist nicht in der Liste? Trage sie auf RaidOps ein: https://app/characters")]
    [InlineData("en", "Spec you're looking for isn't listed? Declare it on RaidOps: https://app/characters")]
    public void SpecImportHint_ReturnsLocalizedMessage(string language, string expected)
    {
        RaidSignupCommandText.SpecImportHint("https://app", language).Should().Be(expected);
    }

    // ── CharacterProfileUrl ──────────────────────────────────────────────────

    [Fact]
    public void CharacterProfileUrl_LowercasesNameAndSlugifiesBranch()
    {
        var url = RaidSignupCommandText.CharacterProfileUrl("https://app", "Classic Era", "silvermoon", "Arthas");

        url.Should().Be("https://app/characters/classic-era/silvermoon/arthas");
    }

    [Fact]
    public void CharacterProfileUrl_BranchNameWithUnderscores_CollapsedToSingleHyphen()
    {
        var url = RaidSignupCommandText.CharacterProfileUrl("https://app", "MoP_Classic  Era", "silvermoon", "Arthas");

        url.Should().Be("https://app/characters/mop-classic-era/silvermoon/arthas");
    }
}

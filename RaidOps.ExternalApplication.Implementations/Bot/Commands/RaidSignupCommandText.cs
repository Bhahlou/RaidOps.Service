using System.Text.RegularExpressions;
using RaidOps.Application.Contracts.Common;

namespace RaidOps.ExternalApplication.Implementations.Bot.Commands;

/// <summary>
/// Localized text for the raid signup-call embed's Accept/Tentative/Decline button interactions'
/// ephemeral responses, keyed by the guild's configured <c>Guild.Language</c>. Same shape as
/// <see cref="RaidGroupingCommandText"/>. Falls back to English for an unsupported/missing language.
/// </summary>
internal static partial class RaidSignupCommandText
{
    /// <param name="characterProfileUrl">
    /// Deep link to the character in question's RaidOps profile, when one is known and relevant to
    /// the failure (<see cref="ResponseDetail.SpecRequiredForSignup"/>/
    /// <see cref="ResponseDetail.SpecNotAvailableForCharacter"/>) — falls back to the plain
    /// <c>/characters</c> list when <c>null</c>, same as every other error that just needs "go manage
    /// your characters on RaidOps" rather than one specific character.
    /// </param>
    public static string Result(bool success, string? errorCode, string language, string? frontendUrl, string? characterProfileUrl = null) =>
        success ? SuccessMessage(language) : $"❌ {FailureReason(errorCode, language, frontendUrl, characterProfileUrl)}";

    /// <summary>Shown when a customId's status segment doesn't parse — should be unreachable in practice since we build every customId ourselves, but localized anyway rather than left as a dead English string.</summary>
    public static string InvalidAction(string language) => language switch
    {
        "fr" => "❌ Action d'inscription invalide.",
        "de" => "❌ Ungültige Anmeldeaktion.",
        _ => "❌ Invalid signup action.",
    };

    /// <summary>Shown when a select menu's submission somehow carries no value — should be unreachable given a required (min 1) StringMenu, kept as a defensive guard.</summary>
    public static string NoCharacterSelected(string language) => language switch
    {
        "fr" => "❌ Aucun personnage sélectionné.",
        "de" => "❌ Kein Charakter ausgewählt.",
        _ => "❌ No character selected.",
    };

    /// <inheritdoc cref="NoCharacterSelected"/>
    public static string NoSpecSelected(string language) => language switch
    {
        "fr" => "❌ Aucune spécialisation sélectionnée.",
        "de" => "❌ Keine Skillung ausgewählt.",
        _ => "❌ No spec selected.",
    };

    /// <summary>Placeholder of the character-select menu.</summary>
    public static string CharacterSelectPlaceholder(string language) => language switch
    {
        "fr" => "Choisis ton personnage",
        "de" => "Wähle deinen Charakter",
        _ => "Choose your character",
    };

    /// <summary>Placeholder of the spec-select menu.</summary>
    public static string SpecSelectPlaceholder(string language) => language switch
    {
        "fr" => "Choisis ta spécialisation",
        "de" => "Wähle deine Skillung",
        _ => "Choose your spec",
    };

    /// <summary>
    /// Ephemeral message content shown above the character-select menu — plain message content
    /// doesn't render <c>[label](url)</c> markdown links (only embeds do), so this uses a bare URL,
    /// which Discord auto-hyperlinks in both plain content and embeds alike.
    /// </summary>
    public static string CharacterImportHint(string? frontendUrl, string language) => language switch
    {
        "fr" => $"Ton personnage n'est pas dans la liste ? Importe-le sur RaidOps : {frontendUrl}/characters",
        "de" => $"Dein Charakter ist nicht in der Liste? Importiere ihn auf RaidOps: {frontendUrl}/characters",
        _ => $"Character not in the list? Import it on RaidOps: {frontendUrl}/characters",
    };

    /// <summary>Ephemeral message content shown above the spec-select menu — same bare-URL rationale as <see cref="CharacterImportHint"/>.</summary>
    public static string SpecImportHint(string? frontendUrl, string language) => language switch
    {
        "fr" => $"La spécialisation que tu cherches n'est pas dans la liste ? Déclare-la sur RaidOps : {frontendUrl}/characters",
        "de" => $"Die gesuchte Skillung ist nicht in der Liste? Trage sie auf RaidOps ein: {frontendUrl}/characters",
        _ => $"Spec you're looking for isn't listed? Declare it on RaidOps: {frontendUrl}/characters",
    };

    /// <summary>
    /// Deep link to a specific character's RaidOps profile — mirrors the front end's own slug logic
    /// exactly (<c>character-card.component.ts</c>: <c>branchName.toLowerCase().replace(/[\s_]+/g, '-')</c>
    /// for the branch segment, realm slug and lowercased character name as-is), so this always matches
    /// the front's own generated routerLink.
    /// </summary>
    public static string CharacterProfileUrl(string? frontendUrl, string branchName, string realmSlug, string characterName)
    {
        var branchSlug = BranchSlugSeparatorRegex().Replace(branchName.ToLowerInvariant(), "-");
        return $"{frontendUrl}/characters/{branchSlug}/{realmSlug}/{characterName.ToLowerInvariant()}";
    }

    /// <summary>Matches the whitespace/underscore runs collapsed to a single hyphen in a branch slug.</summary>
    [GeneratedRegex(@"[\s_]+")]
    private static partial Regex BranchSlugSeparatorRegex();

    private static string SuccessMessage(string language) => language switch
    {
        "fr" => "✅ Réponse enregistrée !",
        "de" => "✅ Antwort gespeichert!",
        _ => "✅ Response saved!",
    };

    /// <summary>
    /// One localized-message builder per error code, each taking <c>(frontendUrl, characterProfileUrl, language)</c>
    /// — most ignore one or both of the URL parameters (only the codes that genuinely benefit from a
    /// link use them), but a single delegate shape keeps every entry uniform.
    /// </summary>
    private static readonly Dictionary<string, Func<string?, string?, string, string>> FailureReasonsByErrorCode = new()
    {
        [ResponseDetail.Forbidden] = ForbiddenReason,
        [ResponseDetail.RaidEventNotFound] = RaidEventNotFoundReason,
        [ResponseDetail.RaidEventNotInSignupMode] = RaidEventNotInSignupModeReason,
        [ResponseDetail.CharacterRequiredForSignup] = CharacterRequiredForSignupReason,
        [ResponseDetail.SpecRequiredForSignup] = SpecRequiredForSignupReason,
        [ResponseDetail.SpecNotAvailableForCharacter] = SpecNotAvailableForCharacterReason,
        [ResponseDetail.CharacterNotFound] = CharacterNotFoundReason,
        [ResponseDetail.CharacterNotOnRoster] = CharacterNotOnRosterReason,
    };

    private static string ForbiddenReason(string? frontendUrl, string? characterProfileUrl, string language) => language switch
    {
        "fr" => $"tu dois faire partie du roster de cette branche pour répondre. Pas encore inscrit sur RaidOps ? {frontendUrl}/get-started",
        "de" => $"du musst Mitglied des Rosters dieses Branches sein, um zu antworten. Noch nicht auf RaidOps registriert? {frontendUrl}/get-started",
        _ => $"you must be on this guild branch's roster to respond. Not registered on RaidOps yet? {frontendUrl}/get-started",
    };

    private static string RaidEventNotFoundReason(string? frontendUrl, string? characterProfileUrl, string language) => language switch
    {
        "fr" => "ce raid n'existe plus.",
        "de" => "dieser Raid existiert nicht mehr.",
        _ => "this raid no longer exists.",
    };

    private static string RaidEventNotInSignupModeReason(string? frontendUrl, string? characterProfileUrl, string language) => language switch
    {
        "fr" => "ce raid n'est pas en mode inscription.",
        "de" => "dieser Raid ist nicht im Anmeldemodus.",
        _ => "this raid is not in signup mode.",
    };

    private static string CharacterRequiredForSignupReason(string? frontendUrl, string? characterProfileUrl, string language) => language switch
    {
        "fr" => $"tu n'as aucun personnage sur le roster de cette branche. Importe-le sur RaidOps : {frontendUrl}/characters",
        "de" => $"du hast keinen Charakter im Roster dieses Branches. Importiere ihn auf RaidOps: {frontendUrl}/characters",
        _ => $"you have no character on this branch's roster. Import it on RaidOps: {frontendUrl}/characters",
    };

    private static string SpecRequiredForSignupReason(string? frontendUrl, string? characterProfileUrl, string language)
    {
        var url = characterProfileUrl ?? $"{frontendUrl}/characters";
        return language switch
        {
            "fr" => $"ce personnage n'a aucune spécialisation de raid déclarée — configure-la sur RaidOps : {url}",
            "de" => $"dieser Charakter hat keine Raid-Skillung konfiguriert — richte sie auf RaidOps ein: {url}",
            _ => $"this character has no raid spec configured — set one up on RaidOps: {url}",
        };
    }

    private static string SpecNotAvailableForCharacterReason(string? frontendUrl, string? characterProfileUrl, string language)
    {
        var url = characterProfileUrl ?? $"{frontendUrl}/characters";
        return language switch
        {
            "fr" => $"cette spécialisation n'est plus déclarée pour ce personnage — mets-le à jour sur RaidOps : {url}",
            "de" => $"diese Skillung ist für diesen Charakter nicht mehr konfiguriert — aktualisiere ihn auf RaidOps: {url}",
            _ => $"this spec isn't declared for this character anymore — update it on RaidOps: {url}",
        };
    }

    private static string CharacterNotFoundReason(string? frontendUrl, string? characterProfileUrl, string language) => language switch
    {
        "fr" => $"ce personnage n'existe plus sur RaidOps. Vérifie tes personnages importés : {frontendUrl}/characters",
        "de" => $"dieser Charakter existiert nicht mehr auf RaidOps. Überprüfe deine importierten Charaktere: {frontendUrl}/characters",
        _ => $"this character no longer exists on RaidOps. Check your imported characters: {frontendUrl}/characters",
    };

    private static string CharacterNotOnRosterReason(string? frontendUrl, string? characterProfileUrl, string language) => language switch
    {
        "fr" => "ce personnage n'est plus membre du roster de cette branche.",
        "de" => "dieser Charakter ist nicht mehr Mitglied des Rosters dieses Branches.",
        _ => "this character is no longer on this branch's roster.",
    };

    private static readonly (string Fr, string De, string En) UnexpectedFailureReason = (
        "une erreur inattendue est survenue.",
        "ein unerwarteter Fehler ist aufgetreten.",
        "an unexpected error occurred.");

    private static string FailureReason(string? errorCode, string language, string? frontendUrl, string? characterProfileUrl)
    {
        if (errorCode is not null && FailureReasonsByErrorCode.TryGetValue(errorCode, out var build))
            return build(frontendUrl, characterProfileUrl, language);

        return language switch
        {
            "fr" => UnexpectedFailureReason.Fr,
            "de" => UnexpectedFailureReason.De,
            _ => UnexpectedFailureReason.En,
        };
    }
}

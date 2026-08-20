using RaidOps.Application.Contracts.Common;

namespace RaidOps.ExternalApplication.Implementations.Bot.Commands;

/// <summary>
/// Localized text for the <c>/raid invite</c> subcommand's ephemeral responses, keyed by the
/// guild's configured <c>Guild.Language</c>. Kept in the bot layer rather than reusing
/// <c>RaidNotificationText</c> — that helper is internal to <c>RaidOps.Application.Implementations</c>
/// and scoped to notification content, not interaction-response text. Falls back to English for an
/// unsupported/missing language.
/// </summary>
internal static class RaidGroupingCommandText
{
    public static string InvalidRaidSelection(string language) => language switch
    {
        "fr" => "❌ Sélection de raid invalide — réessaie en choisissant une suggestion de l'autocomplétion.",
        "de" => "❌ Ungültige Raid-Auswahl — versuche es erneut, indem du einen Autovervollständigungsvorschlag auswählst.",
        _ => "❌ Invalid raid selection — try again by picking an autocomplete suggestion.",
    };

    public static string Result(bool success, string? errorCode, string language) =>
        success ? SuccessMessage(language) : $"❌ {FailureReason(errorCode, language)}";

    private static string SuccessMessage(string language) => language switch
    {
        "fr" => "✅ Message de groupage envoyé !",
        "de" => "✅ Gruppierungsnachricht gesendet!",
        _ => "✅ Grouping message sent!",
    };

    /// <summary>
    /// One (fr, de, en) tuple per <c>ResponseDetail</c> code — a flat lookup instead of a
    /// switch-inside-a-switch, which is both more readable at this width and keeps
    /// <see cref="FailureReason"/>'s cognitive complexity low.
    /// </summary>
    private static readonly Dictionary<string, (string Fr, string De, string En)> FailureReasonsByErrorCode = new()
    {
        [ResponseDetail.Forbidden] = (
            "tu dois être officier de cette guilde pour utiliser cette commande.",
            "du musst Offizier dieser Gilde sein, um diesen Befehl zu verwenden.",
            "you must be an officer of this guild to use this command."),
        [ResponseDetail.RaidEventNotFound] = (
            "ce raid n'existe plus.",
            "dieser Raid existiert nicht mehr.",
            "this raid no longer exists."),
        [ResponseDetail.RaidEventNotPublished] = (
            "ce raid doit être publié avant de pouvoir grouper.",
            "dieser Raid muss veröffentlicht sein, bevor gruppiert werden kann.",
            "this raid must be published before grouping."),
        [ResponseDetail.NoAnnouncementChannelConfigured] = (
            "aucun salon d'annonce n'est configuré pour cette branche.",
            "für diesen Branch ist kein Ankündigungskanal konfiguriert.",
            "no announcement channel is configured for this branch."),
        [ResponseDetail.NoAssignmentsToNotify] = (
            "ce raid n'a aucun joueur assigné.",
            "diesem Raid sind keine Spieler zugewiesen.",
            "this raid has no assigned players."),
        // The parameter name quoted here must match what that locale's Discord client shows for
        // the "character" option — see the fr/de localization files under Bot/Commands/Localizations.
        [ResponseDetail.RaidGroupingRequesterHasNoCharacter] = (
            "tu n'as pas de personnage assigné à ce raid — précise le paramètre \"personnage\".",
            "du hast keinen Charakter, der diesem Raid zugewiesen ist — gib den Parameter \"charakter\" an.",
            "you have no character assigned to this raid — specify the \"character\" parameter."),
        [ResponseDetail.RaidGroupingCharacterNotFound] = (
            "aucun personnage assigné avec ce nom n'a été trouvé dans ce raid.",
            "es wurde kein zugewiesener Charakter mit diesem Namen in diesem Raid gefunden.",
            "no assigned character with that name was found in this raid."),
    };

    private static readonly (string Fr, string De, string En) UnexpectedFailureReason = (
        "une erreur inattendue est survenue.",
        "ein unerwarteter Fehler ist aufgetreten.",
        "an unexpected error occurred.");

    private static string FailureReason(string? errorCode, string language)
    {
        var (Fr, De, En) = errorCode is not null && FailureReasonsByErrorCode.TryGetValue(errorCode, out var found)
            ? found
            : UnexpectedFailureReason;

        return language switch
        {
            "fr" => Fr,
            "de" => De,
            _ => En,
        };
    }
}

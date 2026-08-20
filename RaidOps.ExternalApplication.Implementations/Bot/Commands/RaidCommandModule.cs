using NetCord;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Guilds.Settings.Queries;
using RaidOps.Application.Contracts.Guilds.Settings.Responses;
using RaidOps.Application.Contracts.Raids.Events.Commands;

namespace RaidOps.ExternalApplication.Implementations.Bot.Commands;

/// <summary>
/// The <c>/raid</c> top-level command — officer-only raid actions, grouped as subcommands so the
/// picker stays to one entry as more get added. Gated by <see cref="Permissions.ManageEvents"/> as
/// a Discord-side default: guild admins can always override this from Integrations settings, and
/// it's only an approximation of RaidOps' own officer <c>RoleMapping</c> (Discord has no notion of
/// it) — the real authorization boundary stays each subcommand's own dispatched command/query,
/// which enforces branch-scoped Officer access regardless of what a member could even see here.
/// </summary>
[SlashCommand("raid", "Officer-only raid commands", DefaultGuildPermissions = Permissions.ManageEvents)]
public class RaidCommandModule(ICommandDispatcher commandDispatcher, IQueryDispatcher queryDispatcher) : ApplicationCommandModule<ApplicationCommandContext>
{
    /// <summary>
    /// <c>/raid invite</c> — lets a raid leader trigger the same "grouping up now" ping as the
    /// site's button (<see cref="TriggerRaidGroupingCommand"/>), from directly inside Discord. The
    /// <c>raid</c> parameter is required and autocompleted (see
    /// <see cref="RaidGroupingAutocompleteProvider"/>) — Discord has no notion of a RaidOps event,
    /// so there's no way to make this optional/inferred without ambiguity when several raids are
    /// upcoming. The <c>character</c> parameter is optional: when omitted, the command resolves to
    /// the requester's own assigned character in that raid. Ephemeral response text (see
    /// <see cref="RaidGroupingCommandText"/>) follows the guild's configured <c>Guild.Language</c>,
    /// resolved via <see cref="GetGuildSettingsQuery"/> — note this is unrelated to Discord's own
    /// per-user locale, which only affects the command's registered name/description in the picker.
    /// </summary>
    [SubSlashCommand("invite", "Ping the players assigned to this raid to form groups now")]
    public async Task InviteAsync(
        [SlashCommandParameter(
            Name = "raid",
            Description = "Which raid to group up",
            AutocompleteProviderType = typeof(RaidGroupingAutocompleteProvider))]
        string raid,
        [SlashCommandParameter(
            Name = "character",
            Description = "Character to group on (defaults to your own in this raid)")]
        string? character = null)
    {
        await RespondAsync(InteractionCallback.DeferredMessage(MessageFlags.Ephemeral));

        if (Context.Guild is null)
        {
            await ModifyResponseAsync(message => message.WithContent("❌ This command can only be used in a Discord server."));
            return;
        }

        var guildId = Context.Guild.Id.ToString();
        var requesterDiscordId = Context.User.Id.ToString();

        var settingsResult = await queryDispatcher.DispatchAsync<GetGuildSettingsQuery, GuildSettingsResponse>(
            new GetGuildSettingsQuery { GuildId = guildId, RequesterDiscordId = requesterDiscordId });
        var language = settingsResult.Value?.Language ?? "en";

        var parts = raid.Split(':', 2);
        if (parts.Length != 2 || !int.TryParse(parts[0], out var guildBranchId) || !int.TryParse(parts[1], out var eventId))
        {
            await ModifyResponseAsync(message => message.WithContent(RaidGroupingCommandText.InvalidRaidSelection(language)));
            return;
        }

        var result = await commandDispatcher.DispatchAsync(new TriggerRaidGroupingCommand
        {
            GuildId = guildId,
            GuildBranchId = guildBranchId,
            EventId = eventId,
            RequesterDiscordId = requesterDiscordId,
            CharacterName = character,
        });

        await ModifyResponseAsync(message => message.WithContent(RaidGroupingCommandText.Result(result.IsSuccess, result.Error, language)));
    }
}

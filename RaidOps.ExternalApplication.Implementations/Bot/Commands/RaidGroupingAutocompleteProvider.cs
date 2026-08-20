using NetCord;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Raids.Events.Queries;
using RaidOps.Application.Contracts.Raids.Events.Responses;

namespace RaidOps.ExternalApplication.Implementations.Bot.Commands;

/// <summary>
/// Suggests the guild's upcoming published raids for the <c>/raid invite</c> subcommand's
/// <c>raid</c> parameter. Each choice's value encodes <c>"{guildBranchId}:{eventId}"</c> — invisible
/// to the Discord user, who only ever sees the formatted label — since Discord has no notion of a
/// RaidOps branch and <see cref="RaidOps.Application.Contracts.Raids.Events.Commands.TriggerRaidGroupingCommand"/>
/// needs both to execute. The displayed time is already the guild's local time (see
/// <see cref="RaidEventChoiceResponse.StartsAtLocal"/>), not UTC.
/// </summary>
public class RaidGroupingAutocompleteProvider(IQueryDispatcher queryDispatcher) : IAutocompleteProvider<AutocompleteInteractionContext>
{
    /// <inheritdoc/>
    public async ValueTask<IEnumerable<ApplicationCommandOptionChoiceProperties>?> GetChoicesAsync(
        ApplicationCommandInteractionDataOption option, AutocompleteInteractionContext context)
    {
        if (context.Guild is null)
            return null;

        var result = await queryDispatcher.DispatchAsync<GetUpcomingPublishedRaidEventChoicesQuery, List<RaidEventChoiceResponse>>(
            new GetUpcomingPublishedRaidEventChoicesQuery { GuildId = context.Guild.Id.ToString() });

        if (result.IsFailed)
            return null;

        var input = option.Value?.ToString() ?? string.Empty;

        return [.. result.Value!
            .Where(e => e.Name.Contains(input, StringComparison.OrdinalIgnoreCase))
            .Select(e => new ApplicationCommandOptionChoiceProperties(
                $"{e.Name} — {e.StartsAtLocal:dd/MM HH:mm} ({e.BranchName})",
                $"{e.GuildBranchId}:{e.Id}"))
            .Take(25)];
    }
}

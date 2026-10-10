using Microsoft.Extensions.Logging;
using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Dev.Commands;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Application.Implementations.Dev.CommandHandlers;

/// <summary>
/// Handles <see cref="ResetGuildOnboardingCommand"/> by deleting every Battle.net account linked
/// to the user (cascading to their characters, specs, and guild memberships — the same "right to
/// erasure" cascade as a manual unlink, see <c>RaidOpsDbContext.OnModelCreating</c>) and fully
/// resetting the guild's registration state (unregistered, every setting cleared) so the
/// get-started flow can be replayed from scratch. No audit log entry is written — this is dev
/// tooling, not a user action.
/// </summary>
public class ResetGuildOnboardingCommandHandler(
    IBnetAccountRepository bnetAccountRepository,
    IGuildsRepository guildsRepository,
    ILogger<ResetGuildOnboardingCommandHandler> logger) : ICommandHandlerAsync<ResetGuildOnboardingCommand>
{
    /// <inheritdoc/>
    public async Task<Result<CommandResponse>> HandleAsync(ResetGuildOnboardingCommand command, CancellationToken cancellationToken = default)
    {
        var bnetAccounts = await bnetAccountRepository.GetAllByDiscordIdAsync(command.UserDiscordId, cancellationToken);
        foreach (var account in bnetAccounts)
            await bnetAccountRepository.DeleteAsync(command.UserDiscordId, account.BnetId, cancellationToken);

        // Not UnregisterAsync: that deliberately preserves settings for a real admin re-registering
        // later. Here we want the guild to read as genuinely unconfigured again.
        await guildsRepository.ResetOnboardingAsync(command.GuildId, cancellationToken);

        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation(
                "Onboarding reset for discord user {DiscordId}: {BnetAccountCount} BNet account(s) unlinked, guild {GuildId} unregistered",
                command.UserDiscordId, bnetAccounts.Count, command.GuildId);
        }

        return Result<CommandResponse>.Ok(new CommandResponse("Onboarding progress reset successfully."));
    }
}

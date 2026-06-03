using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Guilds.Commands;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Application.Implementations.Guilds.CommandHandlers;

/// <summary>
/// Handles <see cref="UnregisterGuildCommand"/> by setting <c>IsRegistered = false</c>
/// on the target guild. Typically dispatched when the bot is removed from a Discord server.
/// </summary>
public class UnregisterGuildCommandHandler(
    IGuildsRepository guildsRepository) : ICommandHandlerAsync<UnregisterGuildCommand>
{
    /// <summary>
    /// Marks the guild identified by <see cref="UnregisterGuildCommand.GuildId"/> as unregistered.
    /// Succeeds even if the guild is not found (idempotent — removal events may fire multiple times).
    /// </summary>
    /// <param name="command">The command containing the guild ID to unregister.</param>
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
    /// <returns>A successful <see cref="CommandResponse"/> in all cases.</returns>
    public async Task<Result<CommandResponse>> HandleAsync(UnregisterGuildCommand command, CancellationToken cancellationToken = default)
    {
        await guildsRepository.UnregisterAsync(command.GuildId, cancellationToken);
        return Result<CommandResponse>.Ok(new CommandResponse("Guild unregistered."));
    }
}

using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Guilds.Registration.Commands;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Application.Implementations.Guilds.Registration.CommandHandlers;

/// <summary>
/// Handles <see cref="UnregisterGuildCommand"/> by setting <c>IsRegistered = false</c>
/// on the target guild. Typically dispatched when the bot is removed from a Discord server.
/// </summary>
public class UnregisterGuildCommandHandler(
    IGuildsRepository guildsRepository) : ICommandHandlerAsync<UnregisterGuildCommand>
{
    /// <inheritdoc/>
    public async Task<Result<CommandResponse>> HandleAsync(UnregisterGuildCommand command, CancellationToken cancellationToken = default)
    {
        await guildsRepository.UnregisterAsync(command.GuildId, cancellationToken);
        return Result<CommandResponse>.Ok(new CommandResponse("Guild unregistered."));
    }
}

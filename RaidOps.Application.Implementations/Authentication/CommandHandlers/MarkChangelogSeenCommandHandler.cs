using RaidOps.Application.Contracts.Authentication.Commands;
using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Application.Implementations.Authentication.CommandHandlers;

/// <summary>
/// Handles <see cref="MarkChangelogSeenCommand"/> by recording the acknowledged entries in the
/// requester's own seen-changelog ledger. No access check beyond authentication is needed — a
/// user can only ever record entries against their own ledger, keyed by their own
/// <see cref="MarkChangelogSeenCommand.RequesterDiscordId"/>.
/// </summary>
public class MarkChangelogSeenCommandHandler(
    ISeenChangelogEntryRepository seenChangelogEntryRepository) : ICommandHandlerAsync<MarkChangelogSeenCommand>
{
    /// <inheritdoc/>
    public async Task<Result<CommandResponse>> HandleAsync(MarkChangelogSeenCommand command, CancellationToken cancellationToken = default)
    {
        await seenChangelogEntryRepository.MarkSeenAsync(command.RequesterDiscordId, command.EntryIds, cancellationToken);

        return Result<CommandResponse>.Ok(new CommandResponse("Changelog entries acknowledged."));
    }
}

using RaidOps.Application.Contracts.Authentication.Commands;
using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Application.Implementations.Authentication.CommandHandlers;

/// <summary>
/// Handles <see cref="MarkTourSeenCommand"/> by recording the tour in the requester's own
/// seen-tours ledger. No access check beyond authentication is needed — a user can only ever
/// record a tour against their own ledger, keyed by their own
/// <see cref="MarkTourSeenCommand.RequesterDiscordId"/>.
/// </summary>
public class MarkTourSeenCommandHandler(
    ISeenTourRepository seenTourRepository) : ICommandHandlerAsync<MarkTourSeenCommand>
{
    /// <inheritdoc/>
    public async Task<Result<CommandResponse>> HandleAsync(MarkTourSeenCommand command, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command.TourId))
            return Result<CommandResponse>.Fail("Tour id is required.");

        await seenTourRepository.MarkSeenAsync(command.RequesterDiscordId, command.TourId, cancellationToken);

        return Result<CommandResponse>.Ok(new CommandResponse("Tour acknowledged."));
    }
}

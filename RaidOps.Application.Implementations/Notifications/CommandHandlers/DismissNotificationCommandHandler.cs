using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Notifications.Commands;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Application.Implementations.Notifications.CommandHandlers;

/// <summary>
/// Handles <see cref="DismissNotificationCommand"/> by recording the dismissal in the user's
/// ledger. No access check beyond authentication is needed — a user can only ever dismiss their
/// own ledger, keyed by their own <see cref="DismissNotificationCommand.RequesterDiscordId"/>.
/// </summary>
public class DismissNotificationCommandHandler(
    INotificationDismissalRepository notificationDismissalRepository) : ICommandHandlerAsync<DismissNotificationCommand>
{
    /// <inheritdoc/>
    public async Task<Result<CommandResponse>> HandleAsync(DismissNotificationCommand command, CancellationToken cancellationToken = default)
    {
        await notificationDismissalRepository.DismissAsync(command.RequesterDiscordId, command.Type, command.GuildId, cancellationToken);

        return Result<CommandResponse>.Ok(new CommandResponse("Notification dismissed."));
    }
}

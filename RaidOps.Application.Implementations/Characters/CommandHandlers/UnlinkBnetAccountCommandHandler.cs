using Microsoft.Extensions.Logging;
using RaidOps.Application.Contracts.Characters.Commands;
using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Services;
using RaidOps.Domain.Enums;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Application.Implementations.Characters.CommandHandlers;

/// <summary>
/// Handles <see cref="UnlinkBnetAccountCommand"/> by recording a "left the guild" audit entry for
/// every roster the soon-to-be-deleted characters belong to, then deleting the linked BNet
/// account. Every character sourced from it — and its expansion states, specs, raid specs, and
/// guild memberships — is hard-deleted via DB cascade (see <c>RaidOpsDbContext.OnModelCreating</c>),
/// not just deactivated: this is a deliberate "right to erasure" action, not a soft unlink. The
/// audit entries must be written before the delete, since the cascade removes the membership rows
/// they're derived from.
/// </summary>
public class UnlinkBnetAccountCommandHandler(
    IBnetAccountRepository bnetAccountRepository,
    ICharacterRepository characterRepository,
    IGuildMembershipRepository membershipRepository,
    IAuditLogService auditLogService,
    ILogger<UnlinkBnetAccountCommandHandler> logger)
    : ICommandHandlerAsync<UnlinkBnetAccountCommand>
{
    /// <inheritdoc/>
    public async Task<Result<CommandResponse>> HandleAsync(
        UnlinkBnetAccountCommand command,
        CancellationToken cancellationToken = default)
    {
        var characters = (await characterRepository.GetByUserWithDetailsAsync(
            command.UserDiscordId, activeOnly: false, cancellationToken))
            .Where(c => c.SourceBnetId == command.BnetId)
            .ToList();

        if (characters.Count > 0)
        {
            var memberships = await membershipRepository.GetByCharacterIdsAsync(
                characters.Select(c => c.Id), cancellationToken);
            var charactersById = characters.ToDictionary(c => c.Id);

            foreach (var membership in memberships)
            {
                var character = charactersById[membership.CharacterId];

                await auditLogService.LogAsync(
                    membership.GuildId,
                    command.UserDiscordId,
                    GuildAuditAction.MemberLeft,
                    new Dictionary<string, string>
                    {
                        ["characterName"] = character.Name,
                        ["characterClassId"] = character.ClassId.ToString(),
                    },
                    cancellationToken);
            }
        }

        await bnetAccountRepository.DeleteAsync(command.UserDiscordId, command.BnetId, cancellationToken);

        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation(
                "BNet account {BnetId} unlinked for discord user {DiscordId}: {CharacterCount} character(s) deleted via cascade",
                command.BnetId, command.UserDiscordId, characters.Count);
        }

        return Result<CommandResponse>.Ok(new CommandResponse("BNet account unlinked successfully."));
    }
}

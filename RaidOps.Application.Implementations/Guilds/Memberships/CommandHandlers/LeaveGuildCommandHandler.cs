using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Guilds.Memberships.Commands;
using RaidOps.Application.Contracts.Services;
using RaidOps.Domain.Enums;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Application.Implementations.Guilds.Memberships.CommandHandlers;

/// <summary>
/// Handles <see cref="LeaveGuildCommand"/> by verifying character ownership and membership,
/// then removing the character from the guild roster.
/// </summary>
public class LeaveGuildCommandHandler(
    ICharacterRepository characterRepository,
    IGuildMembershipRepository membershipRepository,
    IAuditLogService auditLogService) : ICommandHandlerAsync<LeaveGuildCommand>
{
    /// <inheritdoc/>
    public async Task<Result<CommandResponse>> HandleAsync(LeaveGuildCommand command, CancellationToken cancellationToken = default)
    {
        var character = await characterRepository.GetByIdAsync(command.CharacterId, cancellationToken);
        if (character == null)
            return Result<CommandResponse>.Fail(ResponseDetail.CharacterNotFound, $"Character '{command.CharacterId}' does not exist.");

        if (character.UserDiscordId != command.RequesterDiscordId)
            return Result<CommandResponse>.Fail(ResponseDetail.CharacterNotOwned, "You do not own this character.");

        var removed = await membershipRepository.DeleteAsync(command.CharacterId, command.GuildId, cancellationToken);
        if (!removed)
            return Result<CommandResponse>.Fail(ResponseDetail.NotAMember, "This character is not on this guild's roster.");

        await auditLogService.LogAsync(
            command.GuildId,
            command.RequesterDiscordId,
            GuildAuditAction.MemberLeft,
            new Dictionary<string, string>
            {
                ["characterName"] = character.Name,
                ["characterClassId"] = character.ClassId.ToString(),
            },
            cancellationToken);

        return Result<CommandResponse>.Ok(new CommandResponse("Character removed from the guild roster."));
    }
}

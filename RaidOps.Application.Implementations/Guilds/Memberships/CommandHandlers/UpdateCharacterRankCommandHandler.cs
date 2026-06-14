using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Guilds.Memberships.Commands;
using RaidOps.Application.Contracts.Services;
using RaidOps.Domain.Enums;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Application.Implementations.Guilds.Memberships.CommandHandlers;

/// <summary>
/// Handles <see cref="UpdateCharacterRankCommand"/> by verifying character ownership and
/// updating the raid-composition rank of an existing roster membership.
/// </summary>
public class UpdateCharacterRankCommandHandler(
    ICharacterRepository characterRepository,
    IGuildMembershipRepository membershipRepository,
    IAuditLogService auditLogService) : ICommandHandlerAsync<UpdateCharacterRankCommand>
{
    /// <inheritdoc/>
    public async Task<Result<CommandResponse>> HandleAsync(UpdateCharacterRankCommand command, CancellationToken cancellationToken = default)
    {
        var character = await characterRepository.GetByIdAsync(command.CharacterId, cancellationToken);
        if (character == null)
            return Result<CommandResponse>.Fail(ResponseDetail.CharacterNotFound, $"Character '{command.CharacterId}' does not exist.");

        if (character.UserDiscordId != command.RequesterDiscordId)
            return Result<CommandResponse>.Fail(ResponseDetail.CharacterNotOwned, "You do not own this character.");

        var membership = await membershipRepository.GetAsync(command.CharacterId, command.GuildId, cancellationToken);
        if (membership == null)
            return Result<CommandResponse>.Fail(ResponseDetail.NotAMember, "This character is not on this guild's roster.");

        if (membership.CharacterRank == command.CharacterRank)
            return Result<CommandResponse>.Ok(new CommandResponse("Rank unchanged."));

        var oldRank = membership.CharacterRank;
        await membershipRepository.UpdateRankAsync(command.CharacterId, command.GuildId, command.CharacterRank, cancellationToken);

        await auditLogService.LogAsync(
            command.GuildId,
            command.RequesterDiscordId,
            GuildAuditAction.MemberRankUpdated,
            new Dictionary<string, string>
            {
                ["characterName"] = character.Name,
                ["oldRank"] = oldRank.ToString(),
                ["newRank"] = command.CharacterRank.ToString(),
            },
            cancellationToken);

        return Result<CommandResponse>.Ok(new CommandResponse("Character rank updated."));
    }
}

using Microsoft.Extensions.Logging;
using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Guilds.Memberships.Commands;
using RaidOps.Application.Contracts.Services;
using RaidOps.Domain.Enums;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Application.Implementations.Guilds.Memberships.CommandHandlers;

/// <summary>
/// Handles <see cref="UpdateCharacterRankCommand"/> by verifying the requester owns the
/// character or is an officer of the guild, then updating the raid-composition rank of an
/// existing roster membership.
/// </summary>
public class UpdateCharacterRankCommandHandler(
    ICharacterRepository characterRepository,
    IGuildMembershipRepository membershipRepository,
    IGuildAccessService guildAccessService,
    IAuditLogService auditLogService,
    ILogger<UpdateCharacterRankCommandHandler> logger) : ICommandHandlerAsync<UpdateCharacterRankCommand>
{
    /// <inheritdoc/>
    public async Task<Result<CommandResponse>> HandleAsync(UpdateCharacterRankCommand command, CancellationToken cancellationToken = default)
    {
        var character = await characterRepository.GetByIdAsync(command.CharacterId, cancellationToken);
        if (character == null)
            return Result<CommandResponse>.Fail(ResponseDetail.CharacterNotFound, $"Character '{command.CharacterId}' does not exist.");

        var membership = await membershipRepository.GetAsync(command.CharacterId, command.GuildId, cancellationToken);
        if (membership == null)
            return Result<CommandResponse>.Fail(ResponseDetail.NotAMember, "This character is not on this guild's roster.");

        if (character.UserDiscordId != command.RequesterDiscordId)
        {
            var accessLevel = await guildAccessService.GetAccessLevelAsync(command.RequesterDiscordId, command.GuildId, membership.GuildBranchId, cancellationToken);
            if (accessLevel < GuildAccessLevel.Officer)
                return Result<CommandResponse>.Fail(ResponseDetail.Forbidden, "You do not own this character and are not an officer of this guild.");
        }

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
                ["characterClassId"] = character.ClassId.ToString(),
                ["oldRank"] = oldRank.ToString(),
                ["newRank"] = command.CharacterRank.ToString(),
            },
            cancellationToken);

        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation(
                "Character {CharacterId} ({CharacterName}) rank updated from {OldRank} to {NewRank} in guild {GuildId}, requested by discord user {DiscordId}",
                character.Id, character.Name, oldRank, command.CharacterRank, command.GuildId, command.RequesterDiscordId);
        }

        return Result<CommandResponse>.Ok(new CommandResponse("Character rank updated."));
    }
}

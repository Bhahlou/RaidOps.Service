using Microsoft.Extensions.Logging;
using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Guilds.Memberships.Commands;
using RaidOps.Application.Contracts.Services;
using RaidOps.Domain.Enums;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Application.Implementations.Guilds.Memberships.CommandHandlers;

/// <summary>
/// Handles <see cref="LeaveGuildCommand"/> by verifying the requester owns the character or is
/// an officer of the guild, then removing the character from the guild roster.
/// </summary>
public class LeaveGuildCommandHandler(
    ICharacterRepository characterRepository,
    IGuildMembershipRepository membershipRepository,
    IGuildAccessService guildAccessService,
    IAuditLogService auditLogService,
    ILogger<LeaveGuildCommandHandler> logger) : ICommandHandlerAsync<LeaveGuildCommand>
{
    /// <inheritdoc/>
    public async Task<Result<CommandResponse>> HandleAsync(LeaveGuildCommand command, CancellationToken cancellationToken = default)
    {
        var character = await characterRepository.GetByIdAsync(command.CharacterId, cancellationToken);
        if (character == null)
            return Result<CommandResponse>.Fail(ResponseDetail.CharacterNotFound, $"Character '{command.CharacterId}' does not exist.");

        var isOwner = character.UserDiscordId == command.RequesterDiscordId;
        if (!isOwner)
        {
            var accessLevel = await guildAccessService.GetAccessLevelAsync(command.RequesterDiscordId, command.GuildId, cancellationToken);
            if (accessLevel < GuildAccessLevel.Officer)
                return Result<CommandResponse>.Fail(ResponseDetail.Forbidden, "You do not own this character and are not an officer of this guild.");

            var outranksTarget = await guildAccessService.OutranksAsync(command.GuildId, command.RequesterDiscordId, character.UserDiscordId, cancellationToken);
            if (!outranksTarget)
                return Result<CommandResponse>.Fail(ResponseDetail.Forbidden, "You cannot exclude a member with an equal or higher role than yours.");
        }

        var removed = await membershipRepository.DeleteAsync(command.CharacterId, command.GuildId, cancellationToken);
        if (!removed)
            return Result<CommandResponse>.Fail(ResponseDetail.NotAMember, "This character is not on this guild's roster.");

        await auditLogService.LogAsync(
            command.GuildId,
            command.RequesterDiscordId,
            isOwner ? GuildAuditAction.MemberLeft : GuildAuditAction.MemberExcluded,
            new Dictionary<string, string>
            {
                ["characterName"] = character.Name,
                ["characterClassId"] = character.ClassId.ToString(),
            },
            cancellationToken);

        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation(
                "Character {CharacterId} ({CharacterName}) removed from guild {GuildId} roster, requested by discord user {DiscordId} ({Action})",
                character.Id, character.Name, command.GuildId, command.RequesterDiscordId, isOwner ? "left" : "excluded");
        }

        return Result<CommandResponse>.Ok(new CommandResponse("Character removed from the guild roster."));
    }
}

using Microsoft.Extensions.Logging;
using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Guilds.Memberships.Commands;
using RaidOps.Application.Contracts.Services;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Discord;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Application.Implementations.Guilds.Memberships.CommandHandlers;

/// <summary>
/// Handles <see cref="JoinGuildCommand"/> by verifying character ownership, Discord membership,
/// roster eligibility (via <see cref="IGuildJoinEligibilityService"/>), and absence of an existing
/// membership, then adding the character.
/// </summary>
public class JoinGuildCommandHandler(
    ICharacterRepository characterRepository,
    IGuildsRepository guildsRepository,
    IGuildJoinEligibilityService guildJoinEligibilityService,
    IUserGuildsRepository userGuildsRepository,
    IGuildMembershipRepository membershipRepository,
    IAuditLogService auditLogService,
    ILogger<JoinGuildCommandHandler> logger) : ICommandHandlerAsync<JoinGuildCommand>
{
    /// <inheritdoc/>
    public async Task<Result<CommandResponse>> HandleAsync(JoinGuildCommand command, CancellationToken cancellationToken = default)
    {
        // Verify the character exists and belongs to the requester
        var character = await characterRepository.GetByIdAsync(command.CharacterId, cancellationToken);
        if (character == null)
            return Result<CommandResponse>.Fail(ResponseDetail.CharacterNotFound, $"Character '{command.CharacterId}' does not exist.");

        if (character.UserDiscordId != command.RequesterDiscordId)
            return Result<CommandResponse>.Fail(ResponseDetail.CharacterNotOwned, "You do not own this character.");

        // Verify the guild exists and is registered
        var guild = await guildsRepository.GetByIdAsync(command.GuildId, cancellationToken);
        if (guild == null)
            return Result<CommandResponse>.Fail(ResponseDetail.GuildNotFound, $"Guild '{command.GuildId}' does not exist.");

        if (!guild.IsRegistered)
            return Result<CommandResponse>.Fail(ResponseDetail.GuildNotRegistered, "Guild is not registered in RaidOps.");

        // Verify the character's WoW branch is active on this guild and roster-joinable
        // (branch existence/RosterMode/Discord-role access all live in this one place now).
        var branchResult = await guildJoinEligibilityService.ResolveEligibleBranchAsync(
            command.GuildId, character.BranchId, command.RequesterDiscordId, cancellationToken);
        if (branchResult.IsFailed)
            return Result<CommandResponse>.Fail(branchResult.Error!, branchResult.Detail);

        var branch = branchResult.Value!;

        // Verify the requester is a Discord member of this guild
        var userGuilds = await userGuildsRepository.GetByUserDiscordIdAsync(command.RequesterDiscordId, cancellationToken);
        var discordMembership = userGuilds.FirstOrDefault(ug => ug.GuildId == command.GuildId);
        if (discordMembership == null)
            return Result<CommandResponse>.Fail(ResponseDetail.Forbidden, "You are not a member of this Discord server.");

        // Verify no existing membership
        var alreadyMember = await membershipRepository.ExistsAsync(command.CharacterId, command.GuildId, cancellationToken);
        if (alreadyMember)
            return Result<CommandResponse>.Fail(ResponseDetail.AlreadyMember, "This character is already on this guild's roster.");

        // Create membership
        var membership = new GuildMembership
        {
            CharacterId = command.CharacterId,
            GuildId = command.GuildId,
            GuildBranchId = branch.Id,
            CharacterRank = command.CharacterRank,
            JoinedAt = DateTime.UtcNow,
        };

        await membershipRepository.AddAsync(membership, cancellationToken);

        await auditLogService.LogAsync(
            command.GuildId,
            command.RequesterDiscordId,
            GuildAuditAction.MemberJoined,
            new Dictionary<string, string>
            {
                ["characterName"] = character.Name,
                ["characterClassId"] = character.ClassId.ToString(),
            },
            cancellationToken);

        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation(
                "Character {CharacterId} ({CharacterName}) joined guild {GuildId} roster, requested by discord user {DiscordId}",
                character.Id, character.Name, command.GuildId, command.RequesterDiscordId);
        }

        return Result<CommandResponse>.Ok(new CommandResponse("Character added to the guild roster."));
    }
}

using Microsoft.Extensions.Logging;
using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Guilds.Memberships.Commands;
using RaidOps.Application.Contracts.Services;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Discord;
using RaidOps.ExternalApplication.Contracts.Services.DiscordBot;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Application.Implementations.Guilds.Memberships.CommandHandlers;

/// <summary>
/// Handles <see cref="JoinGuildCommand"/> by verifying character ownership, Discord membership,
/// roster eligibility (RosterMode), and absence of an existing membership, then adding the character.
/// </summary>
public class JoinGuildCommandHandler(
    ICharacterRepository characterRepository,
    IGuildsRepository guildsRepository,
    IUserGuildsRepository userGuildsRepository,
    IGuildMembershipRepository membershipRepository,
    IDiscordBotService discordBotService,
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

        // Verify the guild exists, is registered, and is configured
        var guild = await guildsRepository.GetByIdAsync(command.GuildId, cancellationToken);
        if (guild == null)
            return Result<CommandResponse>.Fail(ResponseDetail.GuildNotFound, $"Guild '{command.GuildId}' does not exist.");

        if (!guild.IsRegistered)
            return Result<CommandResponse>.Fail(ResponseDetail.GuildNotRegistered, "Guild is not registered in RaidOps.");

        if (guild.RosterMode == null)
            return Result<CommandResponse>.Fail(ResponseDetail.GuildNotConfigured, "Guild settings have not been configured yet.");

        // Verify the requester is a Discord member of this guild
        var userGuilds = await userGuildsRepository.GetByUserDiscordIdAsync(command.RequesterDiscordId, cancellationToken);
        var discordMembership = userGuilds.FirstOrDefault(ug => ug.GuildId == command.GuildId);
        if (discordMembership == null)
            return Result<CommandResponse>.Fail(ResponseDetail.Forbidden, "You are not a member of this Discord server.");

        // Verify roster access based on RosterMode
        if (guild.RosterMode == RosterMode.DiscordRoleOnly)
        {
            var accessError = CheckDiscordRoleAccess(guild.MinRosterRoleId, command.GuildId, command.RequesterDiscordId, cancellationToken);
            if (accessError != null)
                return accessError;
        }

        // Verify no existing membership
        var alreadyMember = await membershipRepository.ExistsAsync(command.CharacterId, command.GuildId, cancellationToken);
        if (alreadyMember)
            return Result<CommandResponse>.Fail(ResponseDetail.AlreadyMember, "This character is already on this guild's roster.");

        // Create membership
        var membership = new GuildMembership
        {
            CharacterId = command.CharacterId,
            GuildId = command.GuildId,
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

        logger.LogInformation(
            "Character {CharacterId} ({CharacterName}) joined guild {GuildId} roster, requested by discord user {DiscordId}",
            character.Id, character.Name, command.GuildId, command.RequesterDiscordId);

        return Result<CommandResponse>.Ok(new CommandResponse("Character added to the guild roster."));
    }

    private Result<CommandResponse>? CheckDiscordRoleAccess(string? minRosterRoleId, string guildId, string requesterDiscordId, CancellationToken cancellationToken)
    {
        if (minRosterRoleId == null)
            return Result<CommandResponse>.Fail(ResponseDetail.GuildNotConfigured, "Roster role threshold is not configured.");

        try
        {
            var roles = discordBotService.Guilds.GetRoles(guildId, cancellationToken)
                .ToDictionary(r => r.Id.ToString());

            if (!roles.TryGetValue(minRosterRoleId, out var minRole))
                return Result<CommandResponse>.Fail(ResponseDetail.RosterAccessDenied, "The required Discord role no longer exists.");

            var guildUser = discordBotService.Guilds.GetUsers(guildId, cancellationToken)
                .FirstOrDefault(u => u.Id.ToString() == requesterDiscordId);

            if (guildUser == null)
                return Result<CommandResponse>.Fail(ResponseDetail.RosterAccessDenied, "You are not found in this Discord server.");

            var hasAccess = guildUser.RoleIds.Any(rid =>
                roles.TryGetValue(rid.ToString(), out var role) && role.Position >= minRole.Position);

            if (!hasAccess)
                return Result<CommandResponse>.Fail(ResponseDetail.RosterAccessDenied, "You do not have the required Discord role to join this roster.");
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning(ex,
                "Join guild {GuildId} failed for discord user {RequesterDiscordId}: RaidOps bot is not present in this guild",
                guildId, requesterDiscordId);
            return Result<CommandResponse>.Fail(ResponseDetail.GuildBotNotPresent, "The RaidOps bot is not present in this guild.");
        }

        return null;
    }
}

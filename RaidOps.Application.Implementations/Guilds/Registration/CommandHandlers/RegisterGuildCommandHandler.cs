using Microsoft.Extensions.Logging;
using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Guilds.Registration.Commands;
using RaidOps.Application.Contracts.Services;
using RaidOps.Domain.Enums;
using RaidOps.ExternalApplication.Contracts.Services.DiscordBot;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Application.Implementations.Guilds.Registration.CommandHandlers;

/// <summary>
/// Handles <see cref="RegisterGuildCommand"/> by verifying admin rights,
/// confirming the bot is present in the target guild, then marking it as registered.
/// </summary>
public class RegisterGuildCommandHandler(
    IUserGuildsRepository userGuildsRepository,
    IGuildsRepository guildsRepository,
    IDiscordBotService discordBotService,
    IAuditLogService auditLogService,
    ILogger<RegisterGuildCommandHandler> logger) : ICommandHandlerAsync<RegisterGuildCommand>
{
    /// <inheritdoc/>
    public async Task<Result<CommandResponse>> HandleAsync(RegisterGuildCommand command, CancellationToken cancellationToken = default)
    {
        var userGuilds = await userGuildsRepository.GetByUserDiscordIdAsync(command.RequesterDiscordId, cancellationToken);
        var membership = userGuilds.FirstOrDefault(g => g.GuildId == command.GuildId);

        if (membership == null || !membership.IsAdmin)
            return Result<CommandResponse>.Fail(ResponseDetail.Forbidden, "User is not an admin of this guild.");

        try
        {
            discordBotService.Guilds.Get(command.GuildId, cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning(ex,
                "Register guild {GuildId} failed for discord user {RequesterDiscordId}: RaidOps bot is not present in this guild",
                command.GuildId, command.RequesterDiscordId);
            return Result<CommandResponse>.Fail(ResponseDetail.GuildBotNotPresent, "The RaidOps bot is not present in this guild. Please complete the bot invite before registering.");
        }

        var guild = await guildsRepository.RegisterAsync(command.GuildId, cancellationToken);
        if (guild == null)
            return Result<CommandResponse>.Fail(ResponseDetail.GuildNotFound, $"Guild '{command.GuildId}' does not exist.");

        var variables = new Dictionary<string, string> { ["guildName"] = guild.Name };
        if (guild.IconHash != null)
            variables["guildIconHash"] = guild.IconHash;

        await auditLogService.LogAsync(
            command.GuildId,
            command.RequesterDiscordId,
            GuildAuditAction.GuildRegistered,
            variables,
            cancellationToken);

        logger.LogInformation(
            "Guild {GuildId} ({GuildName}) registered by discord user {DiscordId}",
            command.GuildId, guild.Name, command.RequesterDiscordId);

        return Result<CommandResponse>.Ok(new CommandResponse("Guild registered successfully."));
    }
}

using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Guilds.Registration.Commands;
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
    IDiscordBotService discordBotService) : ICommandHandlerAsync<RegisterGuildCommand>
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
        catch (InvalidOperationException)
        {
            return Result<CommandResponse>.Fail(ResponseDetail.GuildBotNotPresent, "The RaidOps bot is not present in this guild. Please complete the bot invite before registering.");
        }

        var registered = await guildsRepository.RegisterAsync(command.GuildId, cancellationToken);
        if (!registered)
            return Result<CommandResponse>.Fail(ResponseDetail.GuildNotFound, $"Guild '{command.GuildId}' does not exist.");

        return Result<CommandResponse>.Ok(new CommandResponse("Guild registered successfully."));
    }
}

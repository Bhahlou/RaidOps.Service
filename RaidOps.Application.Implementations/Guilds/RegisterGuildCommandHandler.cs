using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Guilds.Commands;
using RaidOps.ExternalApplication.Contracts.Services.DiscordBot;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Application.Implementations.Guilds;

/// <summary>
/// Handles <see cref="RegisterGuildCommand"/> by verifying admin rights,
/// confirming the bot is present in the target guild, then marking it as registered.
/// </summary>
public class RegisterGuildCommandHandler(
    IUserGuildsRepository userGuildsRepository,
    IGuildsRepository guildsRepository,
    IDiscordBotService discordBotService) : ICommandHandlerAsync<RegisterGuildCommand>
{
    /// <summary>
    /// <list type="number">
    ///   <item>Verifies the requester is an admin of the target guild.</item>
    ///   <item>Confirms the bot is present in the guild via the Gateway cache.</item>
    ///   <item>Sets <c>IsRegistered = true</c> on the guild.</item>
    /// </list>
    /// </summary>
    /// <param name="command">The registration command containing the guild and requester IDs.</param>
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
    /// <returns>
    /// A successful <see cref="CommandResponse"/> on success;
    /// a failed result if the requester is not an admin, the bot is absent, or the guild does not exist.
    /// </returns>
    public async Task<Result<CommandResponse>> HandleAsync(RegisterGuildCommand command, CancellationToken cancellationToken = default)
    {
        // 1. Verify the requester is an admin of this guild.
        var userGuilds = await userGuildsRepository.GetByUserDiscordIdAsync(command.RequesterDiscordId, cancellationToken);
        var membership = userGuilds.FirstOrDefault(g => g.GuildId == command.GuildId);

        if (membership == null || !membership.IsAdmin)
            return Result<CommandResponse>.Fail("Forbidden", "User is not an admin of this guild.");

        // 2. Confirm the bot is live in the guild (present in the Gateway cache).
        //    The bot joins the guild as soon as the OAuth2 invite is authorized;
        //    if it is absent here, the invite did not complete successfully.
        try
        {
            discordBotService.Guilds.Get(command.GuildId, cancellationToken);
        }
        catch (InvalidOperationException)
        {
            return Result<CommandResponse>.Fail("BotNotPresent", "The RaidOps bot is not present in this guild. Please complete the bot invite before registering.");
        }

        // 3. Persist the registration.
        var registered = await guildsRepository.RegisterAsync(command.GuildId, cancellationToken);
        if (!registered)
            return Result<CommandResponse>.Fail("NotFound", $"Guild '{command.GuildId}' does not exist.");

        return Result<CommandResponse>.Ok(new CommandResponse("Guild registered successfully."));
    }
}

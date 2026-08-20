using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Raids.Events.Commands;
using RaidOps.Application.Contracts.Services;
using RaidOps.Domain.Enums;
using RaidOps.ExternalApplication.Contracts.Services.DiscordBot;

namespace RaidOps.Application.Implementations.Raids.Events.CommandHandlers;

/// <inheritdoc cref="CreateRaidAnnouncementChannelCommand"/>
public class CreateRaidAnnouncementChannelCommandHandler(
    IGuildAccessService guildAccessService,
    IDiscordBotService discordBotService) : ICommandHandlerAsync<CreateRaidAnnouncementChannelCommand>
{
    /// <inheritdoc/>
    public async Task<Result<CommandResponse>> HandleAsync(CreateRaidAnnouncementChannelCommand command, CancellationToken cancellationToken = default)
    {
        var accessLevel = await guildAccessService.GetAccessLevelAsync(command.RequesterDiscordId, command.GuildId, command.GuildBranchId, cancellationToken);
        if (accessLevel != GuildAccessLevel.Officer)
            return Result<CommandResponse>.Fail(ResponseDetail.Forbidden, "User is not an officer of this guild branch.");

        try
        {
            var channel = await discordBotService.Guilds.CreateTextChannelAsync(command.GuildId, command.Name, command.CategoryId, cancellationToken);
            return Result<CommandResponse>.Ok(new CommandResponse(
                "Channel created successfully.",
                new { Id = channel.ChannelId.ToString(), channel.Name, channel.MissingPermissions, channel.CategoryName }));
        }
        catch (InvalidOperationException)
        {
            return Result<CommandResponse>.Fail(ResponseDetail.GuildBotNotPresent, "The RaidOps bot is not present in this guild.");
        }
        catch (Exception ex)
        {
            return Result<CommandResponse>.Fail(ResponseDetail.DiscordChannelCreationFailed, $"Failed to create the channel: {ex.Message}");
        }
    }
}

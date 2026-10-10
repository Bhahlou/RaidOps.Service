using NetCord;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Guilds.Settings.Queries;
using RaidOps.Application.Contracts.Guilds.Settings.Responses;
using RaidOps.ExternalApplication.Contracts.Services.DiscordBot;

namespace RaidOps.ExternalApplication.Implementations.Bot.Commands;

/// <summary>
/// Shared between <see cref="RaidSignupInteractionModule"/> and <see cref="RaidSignupPickerModule"/>
/// — both resolve a spec's synced application-emoji icon and the requester's guild language the
/// exact same way, so it's kept in one place rather than duplicated per module.
/// </summary>
internal static class RaidSignupInteractionHelpers
{
    public static EmojiProperties? SpecEmojiProperties(IDiscordBotService discordBotService, int classId, string specName)
    {
        var id = discordBotService.Emojis.GetId(WowSpecEmojiNames.GetName(classId, specName));
        return id is { } value ? EmojiProperties.Custom(value) : null;
    }

    public static async Task<string> ResolveLanguageAsync(IQueryDispatcher queryDispatcher, string guildId, string requesterDiscordId)
    {
        var settingsResult = await queryDispatcher.DispatchAsync<GetGuildSettingsQuery, GuildSettingsResponse>(
            new GetGuildSettingsQuery { GuildId = guildId, RequesterDiscordId = requesterDiscordId });
        return settingsResult.Value?.Language ?? "en";
    }
}

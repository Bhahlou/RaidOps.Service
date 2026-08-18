using Microsoft.Extensions.Configuration;
using NetCord;
using NetCord.Rest;
using NetCord.Services.ComponentInteractions;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Guilds.Settings.Queries;
using RaidOps.Application.Contracts.Guilds.Settings.Responses;
using RaidOps.Application.Contracts.Raids.Signups.Queries;
using RaidOps.Application.Contracts.Raids.Signups.Responses;
using RaidOps.Domain.Enums;
using RaidOps.ExternalApplication.Contracts.Services.DiscordBot;

namespace RaidOps.ExternalApplication.Implementations.Bot.Commands;

/// <summary>
/// Handles the two live select menus <see cref="RaidSignupInteractionModule"/> opens (as plain
/// ephemeral messages, never modals — Discord modals have no live cross-field reactivity, and a
/// Modal response to a MODAL_SUBMIT interaction is rejected outright, so a genuinely dependent
/// character→spec picker can't be built with modals at all): character-select
/// (<c>raidsignup-pickchar</c>), shown when the member has more than one roster character, and
/// spec-select (<c>raidsignup-pickspec</c>), shown once the character is known — either straight from
/// the button (character already unambiguous) or from this module's own character-select handler —
/// scoped to exactly that character's declared raid specs. A select menu on a message fires its own
/// interaction the instant a value is picked, same category as a button click, so chaining
/// message → message here is entirely ordinary (unlike the modal restriction).
/// </summary>
public class RaidSignupPickerModule(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher,
    IDiscordBotService discordBotService,
    IConfiguration configuration) : ComponentInteractionModule<StringMenuInteractionContext>
{
    private readonly string? _frontendUrl = configuration["FrontendUrl"];

    [ComponentInteraction("raidsignup-pickchar")]
    public async Task HandleCharacterAsync(int guildBranchId, int eventId, string status)
    {
        if (Context.Guild is null)
        {
            // No guild = no Guild.Language to resolve — English-only is unavoidable here specifically.
            await RespondAsync(InteractionCallback.DeferredModifyMessage);
            await ModifyResponseAsync(message => message.WithContent("❌ This can only be used in a Discord server."));
            return;
        }

        var guildId = Context.Guild.Id.ToString();
        var requesterDiscordId = Context.User.Id.ToString();
        var language = await ResolveLanguageAsync(guildId, requesterDiscordId);

        var parsedStatus = RaidSignupInteractionModule.ParseStatus(status);
        if (parsedStatus is null)
        {
            await RespondAsync(InteractionCallback.DeferredModifyMessage);
            await ModifyResponseAsync(message => message.WithContent(RaidSignupCommandText.InvalidAction(language)));
            return;
        }

        var selectedCharacterId = Context.SelectedValues.Count > 0 ? Context.SelectedValues[0] : null;
        if (selectedCharacterId is null || !int.TryParse(selectedCharacterId, out var characterId))
        {
            await RespondAsync(InteractionCallback.DeferredModifyMessage);
            await ModifyResponseAsync(message => message.WithContent(RaidSignupCommandText.NoCharacterSelected(language)));
            return;
        }

        var charactersResult = await queryDispatcher.DispatchAsync<GetMyRosterCharactersQuery, List<RaidSignupCharacterResponse>>(
            new GetMyRosterCharactersQuery { GuildId = guildId, GuildBranchId = guildBranchId, RequesterDiscordId = requesterDiscordId });
        var character = charactersResult.Value?.FirstOrDefault(c => c.CharacterId == characterId);
        var raidSpecs = character?.RaidSpecs ?? [];

        if (raidSpecs.Count > 1)
        {
            var options = raidSpecs.Select(s => new StringMenuSelectOptionProperties(s.SpecName, s.SpecId.ToString())
                .WithEmoji(SpecEmojiProperties(character!.ClassId, s.SpecName))).ToList();
            var component = new StringMenuProperties($"raidsignup-pickspec:{guildBranchId}:{eventId}:{characterId}:{status}", options)
                .WithPlaceholder(RaidSignupCommandText.SpecSelectPlaceholder(language));
            await RespondAsync(InteractionCallback.DeferredModifyMessage);
            await ModifyResponseAsync(message => message
                .WithContent(RaidSignupCommandText.SpecImportHint(_frontendUrl, language))
                .WithComponents([component]));
            return;
        }

        await RespondAsync(InteractionCallback.DeferredModifyMessage);
        await RaidSignupResponseDispatcher.DispatchAndReplyAsync(commandDispatcher, action => ModifyResponseAsync(action), _frontendUrl, new SignupReplyContext(
            guildId, requesterDiscordId, guildBranchId, eventId, parsedStatus.Value, characterId, raidSpecs.Count == 1 ? raidSpecs[0].SpecId : null, language, character));
    }

    [ComponentInteraction("raidsignup-pickspec")]
    public async Task HandleSpecAsync(int guildBranchId, int eventId, int characterId, string status)
    {
        await RespondAsync(InteractionCallback.DeferredModifyMessage);

        if (Context.Guild is null)
        {
            // No guild = no Guild.Language to resolve — English-only is unavoidable here specifically.
            await ModifyResponseAsync(message => message.WithContent("❌ This can only be used in a Discord server."));
            return;
        }

        var guildId = Context.Guild.Id.ToString();
        var requesterDiscordId = Context.User.Id.ToString();
        var language = await ResolveLanguageAsync(guildId, requesterDiscordId);

        var parsedStatus = RaidSignupInteractionModule.ParseStatus(status);
        if (parsedStatus is null)
        {
            await ModifyResponseAsync(message => message.WithContent(RaidSignupCommandText.InvalidAction(language)));
            return;
        }

        var selectedSpecId = Context.SelectedValues.Count > 0 ? Context.SelectedValues[0] : null;
        if (selectedSpecId is null || !int.TryParse(selectedSpecId, out var specId))
        {
            await ModifyResponseAsync(message => message.WithContent(RaidSignupCommandText.NoSpecSelected(language)));
            return;
        }

        // Only needed for a character-specific link in the reply — re-fetched here since this handler
        // only carries characterId positionally (from the customId), not the character's branch/realm.
        var charactersResult = await queryDispatcher.DispatchAsync<GetMyRosterCharactersQuery, List<RaidSignupCharacterResponse>>(
            new GetMyRosterCharactersQuery { GuildId = guildId, GuildBranchId = guildBranchId, RequesterDiscordId = requesterDiscordId });
        var character = charactersResult.Value?.FirstOrDefault(c => c.CharacterId == characterId);

        await RaidSignupResponseDispatcher.DispatchAndReplyAsync(commandDispatcher, action => ModifyResponseAsync(action), _frontendUrl, new SignupReplyContext(
            guildId, requesterDiscordId, guildBranchId, eventId, parsedStatus.Value, characterId, specId, language, character));
    }

    private EmojiProperties? SpecEmojiProperties(int classId, string specName)
    {
        var id = discordBotService.Emojis.GetId(WowSpecEmojiNames.GetName(classId, specName));
        return id is { } value ? EmojiProperties.Custom(value) : null;
    }

    private async Task<string> ResolveLanguageAsync(string guildId, string requesterDiscordId)
    {
        var settingsResult = await queryDispatcher.DispatchAsync<GetGuildSettingsQuery, GuildSettingsResponse>(
            new GetGuildSettingsQuery { GuildId = guildId, RequesterDiscordId = requesterDiscordId });
        return settingsResult.Value?.Language ?? "en";
    }
}

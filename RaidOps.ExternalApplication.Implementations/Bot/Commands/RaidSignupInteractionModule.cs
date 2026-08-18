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
/// Handles clicks on the raid signup-call embed's Accept/Tentative/Decline buttons (see
/// <c>RaidNotificationContentBuilder.BuildSignupCallAsync</c>) — the bot's first message-component
/// interaction, alongside its existing slash commands. Custom ID scheme
/// <c>raidsignup:{guildBranchId}:{eventId}:{status}</c>: <c>[ComponentInteraction]</c> matches only
/// the literal prefix ("raidsignup") — it is NOT a route template, there's no <c>{name}</c>
/// placeholder syntax — every colon-delimited segment after the prefix is bound positionally to the
/// handler method's remaining parameters, in order, with automatic type conversion (confirmed
/// against a previous working bot's own interaction modules, none of which use placeholder syntax
/// either).
/// Accepted/Tentative both commit a specific character+spec (slot-assignment eligibility later
/// requires an exact character match on Accepted responses; Tentative just wants the same
/// information for the roster preview). With exactly one valid (character, declared raid spec)
/// combination this is auto-filled inline; with exactly one character but several specs, the click
/// opens the spec-select menu directly. With more than one character, it instead opens
/// <see cref="RaidSignupPickerModule"/>'s live character-select menu, whose own selection then opens
/// the spec-select menu scoped to that character — plain ephemeral messages throughout, never a
/// modal (Discord modals have no cross-field reactivity while still open, and can't chain a second
/// modal off a modal submission either, so a genuinely dependent character→spec picker isn't
/// buildable with modals at all). Declined never needs a character, so it keeps the simple
/// deferred-response flow.
/// </summary>
public class RaidSignupInteractionModule(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher,
    IDiscordBotService discordBotService,
    IConfiguration configuration) : ComponentInteractionModule<ButtonInteractionContext>
{
    private readonly string? _frontendUrl = configuration["FrontendUrl"];

    [ComponentInteraction("raidsignup")]
    public async Task HandleAsync(int guildBranchId, int eventId, string status)
    {
        if (Context.Guild is null)
        {
            // No guild = no Guild.Language to resolve — English-only is unavoidable here specifically.
            await RespondAsync(InteractionCallback.DeferredMessage(MessageFlags.Ephemeral));
            await ModifyResponseAsync(message => message.WithContent("❌ This can only be used in a Discord server."));
            return;
        }

        var guildId = Context.Guild.Id.ToString();
        var requesterDiscordId = Context.User.Id.ToString();
        var language = await ResolveLanguageAsync(guildId, requesterDiscordId);

        var parsedStatus = ParseStatus(status);
        if (parsedStatus is null)
        {
            await RespondAsync(InteractionCallback.DeferredMessage(MessageFlags.Ephemeral));
            await ModifyResponseAsync(message => message.WithContent(RaidSignupCommandText.InvalidAction(language)));
            return;
        }

        var characterContext = parsedStatus is SignupStatus.Accepted or SignupStatus.Tentative
            ? await ResolveCharacterContextAsync(guildId, guildBranchId, eventId, requesterDiscordId, status, language)
            : CharacterSignupContext.None;

        if (characterContext.RespondedWithPicker)
            return;

        await RespondAsync(InteractionCallback.DeferredMessage(MessageFlags.Ephemeral));
        await RaidSignupResponseDispatcher.DispatchAndReplyAsync(commandDispatcher, action => ModifyResponseAsync(action), _frontendUrl, new SignupReplyContext(
            guildId, requesterDiscordId, guildBranchId, eventId, parsedStatus.Value,
            characterContext.CharacterId, characterContext.SpecId, language, characterContext.Character));
    }

    /// <summary>
    /// Resolves which character/spec to sign up with, replying directly with a picker menu (and
    /// setting <see cref="CharacterSignupContext.RespondedWithPicker"/>) when the choice is
    /// ambiguous — see the class doc for the exact auto-fill-vs-picker rules.
    /// </summary>
    private async Task<CharacterSignupContext> ResolveCharacterContextAsync(
        string guildId, int guildBranchId, int eventId, string requesterDiscordId, string status, string language)
    {
        var charactersResult = await queryDispatcher.DispatchAsync<GetMyRosterCharactersQuery, List<RaidSignupCharacterResponse>>(
            new GetMyRosterCharactersQuery { GuildId = guildId, GuildBranchId = guildBranchId, RequesterDiscordId = requesterDiscordId });
        var characters = charactersResult.Value ?? [];

        if (characters.Count > 1)
        {
            await RespondWithCharacterPickerAsync(guildBranchId, eventId, status, language, characters);
            return CharacterSignupContext.Picker;
        }

        // 0 characters (or the sole character with 0 declared raid specs) is left to the command's own
        // required-field validation below — CharacterRequiredForSignup/SpecRequiredForSignup, same
        // localized messages the web dropdown flow would surface for the same situation.
        if (characters.Count != 1)
            return CharacterSignupContext.None;

        var character = characters[0];
        var raidSpecs = character.RaidSpecs;

        if (raidSpecs.Count > 1)
        {
            await RespondWithSpecPickerAsync(guildBranchId, eventId, status, language, character, raidSpecs);
            return CharacterSignupContext.Picker;
        }

        var specId = raidSpecs.Count == 1 ? raidSpecs[0].SpecId : (int?)null;
        return new CharacterSignupContext(character.CharacterId, specId, character, RespondedWithPicker: false);
    }

    /// <summary>
    /// A character with exactly one declared raid spec already shows that spec's icon — it's the
    /// spec that'll be auto-filled once picked, so it's more informative than the generic class
    /// icon. Ambiguous (&gt;1 spec) characters keep the class icon since we don't know which spec
    /// they'll end up with yet.
    /// </summary>
    private async Task RespondWithCharacterPickerAsync(int guildBranchId, int eventId, string status, string language, List<RaidSignupCharacterResponse> characters)
    {
        var options = characters.Select(c => new StringMenuSelectOptionProperties(c.CharacterName, c.CharacterId.ToString())
            .WithEmoji(c.RaidSpecs.Count == 1 ? SpecEmojiProperties(c.ClassId, c.RaidSpecs[0].SpecName) : ClassEmojiProperties(c.ClassId)))
            .ToList();
        var component = new StringMenuProperties($"raidsignup-pickchar:{guildBranchId}:{eventId}:{status}", options)
            .WithPlaceholder(RaidSignupCommandText.CharacterSelectPlaceholder(language));
        await RespondAsync(InteractionCallback.DeferredMessage(MessageFlags.Ephemeral));
        await ModifyResponseAsync(message => message
            .WithContent(RaidSignupCommandText.CharacterImportHint(_frontendUrl, language))
            .WithComponents([component]));
    }

    private async Task RespondWithSpecPickerAsync(int guildBranchId, int eventId, string status, string language, RaidSignupCharacterResponse character, IReadOnlyList<RaidSignupSpecResponse> raidSpecs)
    {
        var options = raidSpecs.Select(s => new StringMenuSelectOptionProperties(s.SpecName, s.SpecId.ToString())
            .WithEmoji(SpecEmojiProperties(character.ClassId, s.SpecName))).ToList();
        var component = new StringMenuProperties($"raidsignup-pickspec:{guildBranchId}:{eventId}:{character.CharacterId}:{status}", options)
            .WithPlaceholder(RaidSignupCommandText.SpecSelectPlaceholder(language));
        await RespondAsync(InteractionCallback.DeferredMessage(MessageFlags.Ephemeral));
        await ModifyResponseAsync(message => message
            .WithContent(RaidSignupCommandText.SpecImportHint(_frontendUrl, language))
            .WithComponents([component]));
    }

    /// <summary>
    /// Outcome of <see cref="ResolveCharacterContextAsync"/> — either a resolved (possibly null)
    /// character/spec to sign up with, or <see cref="RespondedWithPicker"/> when a picker menu was
    /// sent instead and the caller must stop (the interaction already got its response).
    /// </summary>
    private readonly record struct CharacterSignupContext(int? CharacterId, int? SpecId, RaidSignupCharacterResponse? Character, bool RespondedWithPicker)
    {
        internal static readonly CharacterSignupContext None = new(null, null, null, false);
        internal static readonly CharacterSignupContext Picker = new(null, null, null, true);
    }

    internal static SignupStatus? ParseStatus(string status) => status switch
    {
        "accepted" => SignupStatus.Accepted,
        "tentative" => SignupStatus.Tentative,
        "declined" => SignupStatus.Declined,
        _ => null,
    };

    private EmojiProperties? ClassEmojiProperties(int classId)
    {
        if (!WowClassEmojiNames.ByClassId.TryGetValue(classId, out var emojiName))
            return null;

        var id = discordBotService.Emojis.GetId(emojiName);
        return id is { } value ? EmojiProperties.Custom(value) : null;
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

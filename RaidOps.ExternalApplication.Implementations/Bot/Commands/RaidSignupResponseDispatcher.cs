using NetCord.Rest;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Raids.Signups.Commands;
using RaidOps.Application.Contracts.Raids.Signups.Responses;
using RaidOps.Domain.Enums;

namespace RaidOps.ExternalApplication.Implementations.Bot.Commands;

/// <summary>
/// Dispatches <see cref="SetMyRaidSignupCommand"/> and edits the interaction's ephemeral response
/// with the localized result — the shared tail end of every signup flow, whether the character/spec
/// were auto-filled by <see cref="RaidSignupInteractionModule"/> or picked via
/// <see cref="RaidSignupPickerModule"/>'s select menus.
/// </summary>
internal static class RaidSignupResponseDispatcher
{
    public static async Task DispatchAndReplyAsync(
        ICommandDispatcher commandDispatcher, Func<Action<MessageOptions>, Task> modifyResponseAsync, string? frontendUrl, SignupReplyContext context)
    {
        var result = await commandDispatcher.DispatchAsync(new SetMyRaidSignupCommand
        {
            GuildId = context.GuildId,
            GuildBranchId = context.GuildBranchId,
            EventId = context.EventId,
            RequesterDiscordId = context.RequesterDiscordId,
            Status = context.Status,
            CharacterId = context.CharacterId,
            SpecId = context.SpecId,
        });

        var characterProfileUrl = context.Character is not null
            ? RaidSignupCommandText.CharacterProfileUrl(frontendUrl, context.Character.BranchName, context.Character.RealmSlug, context.Character.CharacterName)
            : null;
        await modifyResponseAsync(message => message
            .WithContent(RaidSignupCommandText.Result(result.IsSuccess, result.Error, context.Language, frontendUrl, characterProfileUrl))
            .WithComponents([]));
    }
}

/// <summary>Everything <see cref="RaidSignupResponseDispatcher.DispatchAndReplyAsync"/> needs about the response being recorded.</summary>
internal readonly record struct SignupReplyContext(
    string GuildId, string RequesterDiscordId, int GuildBranchId, int EventId,
    SignupStatus Status, int? CharacterId, int? SpecId, string Language, RaidSignupCharacterResponse? Character);

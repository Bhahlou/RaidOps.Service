using RaidOps.Application.Contracts.Characters.Responses;
using RaidOps.Application.Contracts.Guilds.Roster.Responses;
using RaidOps.Application.Implementations.Guilds.Access;
using RaidOps.Domain.Models.Discord;
using RaidOps.ExternalApplication.Contracts.Services.DiscordBot;

namespace RaidOps.Application.Implementations.Guilds.Roster;

/// <summary>
/// Maps <see cref="GuildMembership"/> domain entities to <see cref="GuildRosterMemberResponse"/> DTOs.
/// </summary>
internal static class GuildRosterMapper
{
    /// <summary>
    /// Maps a <see cref="GuildMembership"/> to a <see cref="GuildRosterMemberResponse"/>, enriched
    /// with the owning player's Discord display info from <paramref name="usersById"/> and their
    /// guild-local nickname/avatar from the bot's Gateway cache.
    /// </summary>
    internal static GuildRosterMemberResponse ToDto(
        GuildMembership membership,
        Dictionary<string, User> usersById,
        bool canExclude,
        string guildId,
        IDiscordBotService discordBotService,
        CancellationToken cancellationToken)
    {
        var character = membership.Character;
        usersById.TryGetValue(character.UserDiscordId, out var player);
        var member = GuildMemberIdentityResolver.TryGetMember(discordBotService, guildId, character.UserDiscordId, cancellationToken);

        var activeState = character.ExpansionStates.FirstOrDefault(s => s.IsActive)
                       ?? character.ExpansionStates.OrderByDescending(s => s.Level).FirstOrDefault();

        return new GuildRosterMemberResponse
        {
            CharacterId          = character.Id,
            CharacterName        = character.Name,
            ClassId              = character.ClassId,
            ClassName            = character.Class.Name,
            ClassColor           = "#" + character.Class.Color,
            Level                = activeState?.Level ?? 0,
            BranchName           = character.Branch.Name,
            RealmSlug            = character.Realm.Slug,
            AvatarUrl            = character.AvatarUrl,
            PlayerDiscordId      = character.UserDiscordId,
            PlayerName           = member?.Nickname ?? member?.GlobalName ?? member?.Username ?? player?.Name,
            // Prefer the bot's live Gateway cache over the DB snapshot (only refreshed at
            // login/token-refresh) — a member found in cache always wins, even when they have no
            // avatar at all (null is a meaningful live value, not "unknown, fall back to the DB").
            PlayerAvatarHash     = member is not null ? member.AvatarHash : player?.AvatarHash,
            PlayerGuildAvatarUrl = member?.HasGuildAvatar == true ? member.GetGuildAvatarUrl()?.ToString() : null,
            RaidSpecs            = character.RaidSpecs
                .OrderByDescending(rs => rs.IsMain)
                .Select(rs => new CharacterRaidSpecDto
                {
                    SpecId  = rs.SpecId,
                    Name    = rs.Spec.Name,
                    IconUrl = rs.Spec.IconUrl,
                    IsMain  = rs.IsMain,
                })
                .ToList(),
            CharacterRank        = membership.CharacterRank,
            JoinedAt             = membership.JoinedAt,
            CanExclude           = canExclude,
        };
    }
}

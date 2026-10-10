using Microsoft.Extensions.Logging;
using RaidOps.Application.Contracts.Characters.Commands;
using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Services;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Character;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Application.Implementations.Characters.CommandHandlers;

/// <summary>
/// Handles <see cref="SetCharacterRaidSpecsCommand"/> by validating the requested specs against
/// the character's class and replacing its raid-viable spec set. Idempotent — also used to edit
/// a previously set raid spec selection. The requester must own the character or be an officer
/// of a guild it is a roster member of.
/// </summary>
public class SetCharacterRaidSpecsCommandHandler(
    ICharacterRepository characterRepository,
    ISpecRepository specRepository,
    IGuildMembershipRepository membershipRepository,
    IGuildAccessService guildAccessService,
    ILogger<SetCharacterRaidSpecsCommandHandler> logger)
    : ICommandHandlerAsync<SetCharacterRaidSpecsCommand>
{
    /// <inheritdoc/>
    public async Task<Result<CommandResponse>> HandleAsync(
        SetCharacterRaidSpecsCommand command,
        CancellationToken cancellationToken = default)
    {
        var viableIds = command.ViableSpecIds.Distinct().ToList();

        if (viableIds.Count == 0)
            return Result<CommandResponse>.Fail(ResponseDetail.InvalidRequest, "At least one viable spec is required.");

        if (!viableIds.Contains(command.MainSpecId))
            return Result<CommandResponse>.Fail(ResponseDetail.InvalidRequest, "MainSpecId must be included in ViableSpecIds.");

        var character = await characterRepository.GetByIdAsync(command.CharacterId, cancellationToken);
        if (character is null)
        {
            logger.LogWarning(
                "Set raid specs failed for discord user {DiscordId}: character {CharacterId} does not exist",
                command.UserDiscordId, command.CharacterId);
            return Result<CommandResponse>.Fail(ResponseDetail.CharacterNotFound, $"Character '{command.CharacterId}' does not exist.");
        }

        if (character.UserDiscordId != command.UserDiscordId)
        {
            var accessLevel = await CharacterGuildAccessHelper.GetHighestAccessAsync(
                character, command.UserDiscordId, membershipRepository, guildAccessService, cancellationToken);
            if (accessLevel < GuildAccessLevel.Officer)
            {
                logger.LogWarning(
                    "Set raid specs forbidden for discord user {DiscordId} on character {CharacterId}: not owner and access level {AccessLevel} below Officer",
                    command.UserDiscordId, command.CharacterId, accessLevel);
                return Result<CommandResponse>.Fail(ResponseDetail.Forbidden, "You do not own this character and are not an officer of a guild it belongs to.");
            }
        }

        var allSpecs = (await specRepository.GetAllAsync(cancellationToken)).ToDictionary(s => s.Id);

        foreach (var specId in viableIds)
        {
            if (!allSpecs.TryGetValue(specId, out var spec) || spec.ClassId != character.ClassId)
                return Result<CommandResponse>.Fail(ResponseDetail.InvalidRequest, $"Spec '{specId}' is not valid for this character's class.");
        }

        var raidSpecs = viableIds.Select(id => new CharacterRaidSpec
        {
            CharacterId = command.CharacterId,
            SpecId = id,
            IsMain = id == command.MainSpecId,
        });

        await characterRepository.UpsertRaidSpecsAsync(command.CharacterId, raidSpecs, cancellationToken);

        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation(
                "Raid specs updated for character {CharacterId} by discord user {DiscordId}: main spec {MainSpecId}, viable specs [{ViableSpecIds}]",
                command.CharacterId, command.UserDiscordId, command.MainSpecId, string.Join(", ", viableIds));
        }

        return Result<CommandResponse>.Ok(new CommandResponse("Raid specs updated successfully."));
    }
}

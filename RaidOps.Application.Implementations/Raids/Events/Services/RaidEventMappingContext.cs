using RaidOps.Application.Contracts.Services;
using RaidOps.Domain.Models.Character;
using RaidOps.Domain.Models.Discord;
using RaidOps.Domain.Models.Raids;

namespace RaidOps.Application.Implementations.Raids.Events.Services;

/// <summary>Bundles the per-request state shared across every event/assignment mapped by <see cref="RaidEventResponseMapper"/>.</summary>
public sealed record RaidEventMappingContext(
    Guild Guild,
    List<string> RosterPlayerIds,
    Dictionary<string, User> PlayersById,
    IRaidAvailabilityLookup AvailabilityLookup,
    Dictionary<int, Dictionary<string, RaidSignup>> SignupsByEvent,
    Dictionary<int, List<CharacterRaidSpec>> RaidSpecsByCharacter,
    string RequesterDiscordId);

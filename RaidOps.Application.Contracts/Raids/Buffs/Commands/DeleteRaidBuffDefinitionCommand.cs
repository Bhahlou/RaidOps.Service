using RaidOps.Application.Contracts.CQRS;

namespace RaidOps.Application.Contracts.Raids.Buffs.Commands;

/// <summary>Deletes one raid buff definition (and its sources) by its surrogate ID.</summary>
public class DeleteRaidBuffDefinitionCommand : ICommandRequest
{
    /// <summary>Surrogate ID of the definition to delete.</summary>
    public required int Id { get; set; }
}

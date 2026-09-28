using RaidOps.Application.Contracts.Raids.Buffs;
using RaidOps.Application.Contracts.Services;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Application.Implementations.Raids.Buffs.Services;

/// <summary>Loads the reference data a set of raid buff definitions points at and runs <see cref="RaidBuffDefinitionValidator"/> against it.</summary>
public class RaidBuffDefinitionValidationService(
    IWowClassRepository wowClassRepository,
    ISpecRepository specRepository,
    ISpellRepository spellRepository) : IRaidBuffDefinitionValidationService
{
    /// <inheritdoc/>
    public async Task<List<string>> ValidateAsync(int expansionId, IReadOnlyList<RaidBuffDefinitionInput> definitions, CancellationToken cancellationToken = default)
    {
        var classIds = (await wowClassRepository.GetAllAsync(cancellationToken)).Select(c => c.Id).ToHashSet();
        var specClassIdBySpecId = (await specRepository.GetAllAsync(cancellationToken)).ToDictionary(s => s.Id, s => s.ClassId);
        var knownSpellIds = (await spellRepository.GetAvailabilitiesAsync(
                expansionId,
                definitions.Select(d => d.SpellId).Distinct().ToList(),
                cancellationToken))
            .Select(a => a.SpellId)
            .ToHashSet();

        return RaidBuffDefinitionValidator.Validate(definitions, classIds, specClassIdBySpecId, knownSpellIds);
    }
}

using FluentAssertions;
using Moq;
using RaidOps.Application.Implementations.Characters.Services;
using RaidOps.Domain.Models.Character;
using RaidOps.Domain.Models.Reference;
using RaidOps.ExternalApplication.Contracts.Services.BNet.Responses;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.UnitTests.Application.Characters.Services;

public class SpecResolverServiceTests
{
    private readonly Mock<ICharacterRepository> _repo = new();
    private readonly SpecResolverService _sut;
    private readonly CharacterExpansionState _state = new() { Id = 1 };

    public SpecResolverServiceTests()
    {
        _sut = new SpecResolverService(_repo.Object);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static Spec MakeSpec(int id, string name) => new() { Id = id, Name = name };

    private static BnetSpecializationGroupDto ClassicGroup(bool isActive, params (string name, int points)[] trees) =>
        new()
        {
            IsActive = isActive,
            Specializations = [.. trees.Select(t => new BnetSpecializationTreeDto
            {
                SpecializationName = t.name,
                SpentPoints        = t.points
            })]
        };

    private static BnetCharacterSpecializationsResponse ClassicResponse(
        BnetSpecializationGroupDto? active,
        BnetSpecializationGroupDto? inactive)
    {
        var groups = new List<BnetSpecializationGroupDto>();
        if (active   is not null) groups.Add(active);
        if (inactive is not null) groups.Add(inactive);
        return new BnetCharacterSpecializationsResponse { SpecializationGroups = groups };
    }

    private static BnetCharacterSpecializationsResponse ModernResponse(int activeId, params int[] offspecIds) =>
        new()
        {
            ActiveSpecialization = new BnetIdRefDto { Id = activeId },
            Specializations = [.. offspecIds.Select(id => new BnetSpecializationEntryDto { Specialization = new BnetIdRefDto { Id = id } })]
        };

    // ── Classic — dual spec ───────────────────────────────────────────────────

    [Fact]
    public async Task Classic_DualSpec_ReturnsMainFromActiveGroup_OffspecFromInactiveGroup()
    {
        var feral       = MakeSpec(103, "Feral");
        var restoration = MakeSpec(105, "Restoration");

        _repo.Setup(r => r.GetSpecByNameAndClassAsync("Feral Combat", 11, default)).ReturnsAsync(feral);
        _repo.Setup(r => r.GetSpecByNameAndClassAsync("Restoration",  11, default)).ReturnsAsync(restoration);

        var active   = ClassicGroup(isActive: true,  ("Feral Combat", 44), ("Restoration", 17));
        var inactive = ClassicGroup(isActive: false, ("Feral Combat", 46), ("Restoration", 14), ("Balance", 1));
        var response = ClassicResponse(active, inactive);

        var result = await _sut.ResolveAsync(response, classId: 11, _state);

        result.Should().HaveCount(2);
        result.Should().ContainSingle(s => s.SpecId == 103 && s.IsMain);
        result.Should().ContainSingle(s => s.SpecId == 103 && !s.IsMain);
    }

    [Fact]
    public async Task Classic_SingleSpec_NoInactiveGroup_ReturnsOnlyMain()
    {
        var feral = MakeSpec(103, "Feral");
        _repo.Setup(r => r.GetSpecByNameAndClassAsync("Feral Combat", 11, default)).ReturnsAsync(feral);

        var active   = ClassicGroup(isActive: true, ("Feral Combat", 44), ("Restoration", 12));
        var response = ClassicResponse(active, inactive: null);

        var result = await _sut.ResolveAsync(response, classId: 11, _state);

        result.Should().HaveCount(1);
        result.Single().IsMain.Should().BeTrue();
        result.Single().SpecId.Should().Be(103);
    }

    [Fact]
    public async Task Classic_DominantTreeNotFoundInDb_SkipsSpec()
    {
        var restoration = MakeSpec(105, "Restoration");

        _repo.Setup(r => r.GetSpecByNameAndClassAsync("UnknownTree", 11, default)).ReturnsAsync((Spec?)null);
        _repo.Setup(r => r.GetSpecByNameAndClassAsync("Restoration", 11, default)).ReturnsAsync(restoration);

        var active   = ClassicGroup(isActive: true,  ("UnknownTree", 44), ("Restoration", 17));
        var inactive = ClassicGroup(isActive: false, ("Restoration", 30));
        var response = ClassicResponse(active, inactive);

        var result = await _sut.ResolveAsync(response, classId: 11, _state);

        result.Should().HaveCount(1);
        result.Single().IsMain.Should().BeFalse();
    }

    [Fact]
    public async Task Classic_NoGroups_ReturnsEmpty()
    {
        var response = ClassicResponse(active: null, inactive: null);

        var result = await _sut.ResolveAsync(response, classId: 11, _state);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Classic_MainSpecFromActiveGroup_NotFromHighestPointsAcrossAllGroups()
    {
        // The inactive group has 46 pts (higher than active's 44) — should still use active for main.
        var feral = MakeSpec(103, "Feral");
        _repo.Setup(r => r.GetSpecByNameAndClassAsync("Feral Combat", 11, default)).ReturnsAsync(feral);

        var active   = ClassicGroup(isActive: true,  ("Feral Combat", 44));
        var inactive = ClassicGroup(isActive: false, ("Feral Combat", 46));
        var response = ClassicResponse(active, inactive);

        var result = await _sut.ResolveAsync(response, classId: 11, _state);

        result.Should().HaveCount(2);
        result.Should().ContainSingle(s => s.IsMain);
        result.Should().ContainSingle(s => !s.IsMain);
    }

    // ── Modern (MoP+) ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Modern_ActiveAndOffspec_ReturnsBoth()
    {
        var arms        = MakeSpec(71, "Arms");
        var fury        = MakeSpec(72, "Fury");

        _repo.Setup(r => r.GetSpecByIdAsync(71, default)).ReturnsAsync(arms);
        _repo.Setup(r => r.GetSpecByIdAsync(72, default)).ReturnsAsync(fury);

        var response = ModernResponse(activeId: 71, offspecIds: 72);

        var result = await _sut.ResolveAsync(response, classId: 1, _state);

        result.Should().HaveCount(2);
        result.Should().ContainSingle(s => s.SpecId == 71 && s.IsMain);
        result.Should().ContainSingle(s => s.SpecId == 72 && !s.IsMain);
    }

    [Fact]
    public async Task Modern_OnlyActiveSpec_ReturnsOnlyMain()
    {
        var arms = MakeSpec(71, "Arms");
        _repo.Setup(r => r.GetSpecByIdAsync(71, default)).ReturnsAsync(arms);

        var response = ModernResponse(activeId: 71);

        var result = await _sut.ResolveAsync(response, classId: 1, _state);

        result.Should().HaveCount(1);
        result.Single().IsMain.Should().BeTrue();
    }

    [Fact]
    public async Task Modern_ActiveSpecNotFoundInDb_ReturnsEmpty()
    {
        _repo.Setup(r => r.GetSpecByIdAsync(It.IsAny<int>(), default)).ReturnsAsync((Spec?)null);

        var response = ModernResponse(activeId: 999);

        var result = await _sut.ResolveAsync(response, classId: 1, _state);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Modern_SetsCorrectExpansionStateId()
    {
        var state = new CharacterExpansionState { Id = 42 };
        var arms  = MakeSpec(71, "Arms");
        _repo.Setup(r => r.GetSpecByIdAsync(71, default)).ReturnsAsync(arms);

        var response = ModernResponse(activeId: 71);
        var result   = await _sut.ResolveAsync(response, classId: 1, state);

        result.Single().CharacterExpansionStateId.Should().Be(42);
    }
}

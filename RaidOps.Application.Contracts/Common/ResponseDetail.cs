using System.Diagnostics.CodeAnalysis;

namespace RaidOps.Application.Contracts.Common;

/// <summary>
/// Centralises all error codes returned by command and query handlers.
/// Use these constants with <see cref="Result{T}.Fail"/> instead of inline strings.
/// </summary>
[ExcludeFromCodeCoverage]
public static class ResponseDetail
{
    // ── Generic ───────────────────────────────────────────────────────────
    public const string NotFound = nameof(NotFound);
    public const string Forbidden = nameof(Forbidden);
    public const string Unauthorized = nameof(Unauthorized);
    public const string InvalidRequest = nameof(InvalidRequest);

    // ── Auth ──────────────────────────────────────────────────────────────
    public const string UserNotFound = nameof(UserNotFound);
    public const string InvalidRefreshToken = nameof(InvalidRefreshToken);
    public const string InvalidTokenClaims = nameof(InvalidTokenClaims);

    // ── Battle.net ────────────────────────────────────────────────────────
    public const string BnetNotLinked = nameof(BnetNotLinked);
    public const string BnetTokenExpired = nameof(BnetTokenExpired);
    public const string BnetApiError = nameof(BnetApiError);
    public const string InvalidState = nameof(InvalidState);
    public const string StateMismatch = nameof(StateMismatch);

    // ── Guild ─────────────────────────────────────────────────────────────
    public const string GuildNotFound = nameof(GuildNotFound);
    public const string GuildBotNotPresent = nameof(GuildBotNotPresent);
    public const string GuildNotRegistered = nameof(GuildNotRegistered);
    public const string GuildNotConfigured = nameof(GuildNotConfigured);

    // ── Membership ────────────────────────────────────────────────────────
    public const string CharacterNotFound = nameof(CharacterNotFound);
    public const string CharacterNotOwned = nameof(CharacterNotOwned);
    public const string AlreadyMember = nameof(AlreadyMember);
    public const string NotAMember = nameof(NotAMember);
    public const string RosterAccessDenied = nameof(RosterAccessDenied);

    // ── Branch ────────────────────────────────────────────────────────────
    public const string BranchNotFound = nameof(BranchNotFound);
    public const string GuildBranchNotFound = nameof(GuildBranchNotFound);
    public const string GuildBranchNotActive = nameof(GuildBranchNotActive);
    public const string GuildBranchAlreadyActive = nameof(GuildBranchAlreadyActive);

    // ── Calendar ──────────────────────────────────────────────────────────
    public const string AvailabilityExceptionNotFound = nameof(AvailabilityExceptionNotFound);
    public const string RecurringAvailabilityPatternNotFound = nameof(RecurringAvailabilityPatternNotFound);
    public const string PastDeclarationLocked = nameof(PastDeclarationLocked);

    // ── Raids ─────────────────────────────────────────────────────────────
    public const string RaidZoneNotFound = nameof(RaidZoneNotFound);
    public const string RaidSeriesNotFound = nameof(RaidSeriesNotFound);
    public const string RaidEventNotFound = nameof(RaidEventNotFound);
    public const string RaidEventCancelled = nameof(RaidEventCancelled);
    public const string RaidEventHasAssignments = nameof(RaidEventHasAssignments);
    public const string SlotOccupied = nameof(SlotOccupied);
    public const string InvalidGroupOrSlotNumber = nameof(InvalidGroupOrSlotNumber);
    public const string CharacterNotOnRoster = nameof(CharacterNotOnRoster);
    public const string BranchMismatch = nameof(BranchMismatch);
    public const string PlayerAlreadyAssignedInEvent = nameof(PlayerAlreadyAssignedInEvent);
    public const string MemberDeclaredAbsent = nameof(MemberDeclaredAbsent);
    public const string RaidLockoutConflict = nameof(RaidLockoutConflict);
    public const string RaidEventAlreadyPublished = nameof(RaidEventAlreadyPublished);
}

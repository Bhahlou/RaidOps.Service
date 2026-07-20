using System.Text.Json;
using RaidOps.Application.Contracts.Calendar.Availability.Commands;
using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Services;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Calendar;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Application.Implementations.Calendar.Availability.CommandHandlers;

/// <summary>
/// Handles <see cref="UpdateRecurringAvailabilityPatternCommand"/> by verifying roster access and
/// inserting a new pattern version effective from today, non-retroactively. The previous version is
/// closed as of yesterday if it has already applied to at least one past date (so history for those
/// dates stays intact); otherwise — created and edited again on the same day, with nothing to
/// protect — it's deleted outright to avoid a zero-duration version cluttering the table.
/// </summary>
public class UpdateRecurringAvailabilityPatternCommandHandler(
    IGuildAccessService guildAccessService,
    IAvailabilityRepository availabilityRepository,
    IAuditLogService auditLogService) : ICommandHandlerAsync<UpdateRecurringAvailabilityPatternCommand>
{
    /// <inheritdoc/>
    public async Task<Result<CommandResponse>> HandleAsync(UpdateRecurringAvailabilityPatternCommand command, CancellationToken cancellationToken = default)
    {
        var accessLevel = await guildAccessService.GetAccessLevelAsync(command.RequesterDiscordId, command.GuildId, cancellationToken);
        if (accessLevel < GuildAccessLevel.Roster)
            return Result<CommandResponse>.Fail(ResponseDetail.Forbidden, "User is not on this guild's roster.");

        if (command.CycleLengthDays < 1)
            return Result<CommandResponse>.Fail(ResponseDetail.InvalidRequest, "CycleLengthDays must be at least 1.");

        if (command.Days.Any(d => d.OffsetInCycle < 0 || d.OffsetInCycle >= command.CycleLengthDays))
            return Result<CommandResponse>.Fail(ResponseDetail.InvalidRequest, "Every day offset must be within the cycle length.");

        var existing = await availabilityRepository.GetPatternByIdAsync(command.PatternId, command.RequesterDiscordId, command.GuildId, cancellationToken);
        if (existing == null)
            return Result<CommandResponse>.Fail(ResponseDetail.RecurringAvailabilityPatternNotFound, $"Pattern '{command.PatternId}' does not exist.");

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (existing.EffectiveFrom < today)
            await availabilityRepository.ClosePatternAsync(command.PatternId, command.RequesterDiscordId, command.GuildId, today.AddDays(-1), cancellationToken);
        else
            await availabilityRepository.DeletePatternAsync(command.PatternId, command.RequesterDiscordId, command.GuildId, cancellationToken);

        var pattern = await availabilityRepository.AddPatternAsync(new RecurringAvailabilityPattern
        {
            UserDiscordId = command.RequesterDiscordId,
            GuildId = command.GuildId,
            Label = command.Label,
            CycleLengthDays = command.CycleLengthDays,
            AnchorDate = command.AnchorDate,
            EffectiveFrom = today,
            EffectiveUntil = null,
            Days = [.. command.Days.Select(d => new RecurringAvailabilityPatternDay
            {
                OffsetInCycle = d.OffsetInCycle,
                Status = d.Status,
                Reason = d.Reason,
                AvailableFrom = d.AvailableFrom,
                AvailableUntil = d.AvailableUntil,
            })],
        }, cancellationToken);

        await auditLogService.LogAsync(
            command.GuildId,
            command.RequesterDiscordId,
            GuildAuditAction.RecurringAvailabilityPatternUpdated,
            new Dictionary<string, string>
            {
                ["label"] = command.Label ?? string.Empty,
                ["cycleLengthDays"] = command.CycleLengthDays.ToString(),
                ["anchorDate"] = command.AnchorDate.ToString("yyyy-MM-dd"),
                ["days"] = JsonSerializer.Serialize(command.Days.Select(d => new
                {
                    offsetInCycle = d.OffsetInCycle,
                    status = d.Status.ToString(),
                    availableFrom = d.AvailableFrom?.ToString("HH:mm:ss"),
                    availableUntil = d.AvailableUntil?.ToString("HH:mm:ss"),
                })),
            },
            cancellationToken);

        return Result<CommandResponse>.Ok(new CommandResponse("Recurring availability pattern updated successfully.", new { pattern.Id }));
    }
}

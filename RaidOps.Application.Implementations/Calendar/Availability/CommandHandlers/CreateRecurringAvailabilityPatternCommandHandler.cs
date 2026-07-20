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
/// Handles <see cref="CreateRecurringAvailabilityPatternCommand"/> by verifying roster access and
/// persisting the pattern along with its day set.
/// </summary>
public class CreateRecurringAvailabilityPatternCommandHandler(
    IGuildAccessService guildAccessService,
    IAvailabilityRepository availabilityRepository,
    IAuditLogService auditLogService) : ICommandHandlerAsync<CreateRecurringAvailabilityPatternCommand>
{
    /// <inheritdoc/>
    public async Task<Result<CommandResponse>> HandleAsync(CreateRecurringAvailabilityPatternCommand command, CancellationToken cancellationToken = default)
    {
        var accessLevel = await guildAccessService.GetAccessLevelAsync(command.RequesterDiscordId, command.GuildId, cancellationToken);
        if (accessLevel < GuildAccessLevel.Roster)
            return Result<CommandResponse>.Fail(ResponseDetail.Forbidden, "User is not on this guild's roster.");

        if (command.CycleLengthDays < 1)
            return Result<CommandResponse>.Fail(ResponseDetail.InvalidRequest, "CycleLengthDays must be at least 1.");

        if (command.Days.Any(d => d.OffsetInCycle < 0 || d.OffsetInCycle >= command.CycleLengthDays))
            return Result<CommandResponse>.Fail(ResponseDetail.InvalidRequest, "Every day offset must be within the cycle length.");

        var pattern = await availabilityRepository.AddPatternAsync(new RecurringAvailabilityPattern
        {
            UserDiscordId = command.RequesterDiscordId,
            GuildId = command.GuildId,
            Label = command.Label,
            CycleLengthDays = command.CycleLengthDays,
            AnchorDate = command.AnchorDate,
            EffectiveFrom = DateOnly.FromDateTime(DateTime.UtcNow),
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
            GuildAuditAction.RecurringAvailabilityPatternCreated,
            new Dictionary<string, string>
            {
                ["label"] = command.Label ?? string.Empty,
                ["cycleLengthDays"] = command.CycleLengthDays.ToString(),
                ["anchorDate"] = command.AnchorDate.ToString("yyyy-MM-dd"),
                // The front end needs the actual day-by-day statuses (not just a generic "every N
                // days" summary) to say anything useful in the audit log — a bare "Partiel"/cycle
                // count means nothing for a shift rotation. JSON-encoding this list as a single
                // Details value keeps every audit variable a flat string, matching how every other
                // handler's Details dictionary is shaped.
                ["days"] = JsonSerializer.Serialize(command.Days.Select(d => new
                {
                    offsetInCycle = d.OffsetInCycle,
                    status = d.Status.ToString(),
                    availableFrom = d.AvailableFrom?.ToString("HH:mm:ss"),
                    availableUntil = d.AvailableUntil?.ToString("HH:mm:ss"),
                })),
            },
            cancellationToken);

        return Result<CommandResponse>.Ok(new CommandResponse("Recurring availability pattern created successfully.", new { pattern.Id }));
    }
}

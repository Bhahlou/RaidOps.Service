using RaidOps.Application.Contracts.Common;

namespace RaidOps.Application.Contracts.CQRS;

public interface ICommandHandlerAsync<TCommand> where TCommand : ICommandRequest
{
    Task<Result<CommandResponse>> HandleAsync(TCommand command, CancellationToken cancellationToken = default);
}
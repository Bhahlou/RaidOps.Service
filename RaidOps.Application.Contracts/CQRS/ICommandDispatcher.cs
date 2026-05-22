using RaidOps.Application.Contracts.Common;

namespace RaidOps.Application.Contracts.CQRS
{
    public interface ICommandDispatcher
    {
        Task<Result<CommandResponse>> DispatchAsync<TCommand>(TCommand command, CancellationToken cancellationToken = default)
        where TCommand : ICommandRequest;
    }
}
using RaidOps.Application.Contracts.Common;

namespace RaidOps.Application.Contracts.CQRS
{
    public interface IQueryDispatcher
    {
        Task<Result<TResponse>> DispatchAsync<TQuery, TResponse>(TQuery query, CancellationToken cancellationToken = default)
        where TQuery : IQueryRequest<TResponse>;
    }
}
using RaidOps.Application.Contracts.Common;

namespace RaidOps.Application.Contracts.CQRS
{
    public interface IQueryHandlerAsync<TQuery, TResponse>
        where TQuery : IQueryRequest<TResponse>
    {
        Task<Result<TResponse>> HandleAsync(TQuery query, CancellationToken cancellationToken);
    }
}
using Microsoft.AspNetCore.Mvc;
using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;

namespace RaidOps.API.Controllers;

/// <summary>
/// Abstract base controller that all versioned API controllers inherit from.
/// Provides pre-wired <see cref="ICommandDispatcher"/> and <see cref="IQueryDispatcher"/>
/// instances and a helper for translating <see cref="Result{T}"/> values into HTTP responses.
/// </summary>
[ApiController]
[Route("api/v{version:apiVersion}/[controller]")]
public abstract class ApiControllerBase(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher) : ControllerBase
{
    /// <summary>The command dispatcher used to send write-side CQRS commands.</summary>
    protected readonly ICommandDispatcher CommandDispatcher = commandDispatcher;

    /// <summary>The query dispatcher used to send read-side CQRS queries.</summary>
    protected readonly IQueryDispatcher QueryDispatcher = queryDispatcher;

    /// <summary>
    /// Converts a <see cref="Result{T}"/> into an <see cref="IActionResult"/>:
    /// returns <c>200 OK</c> with the value on success, or <c>400 Bad Request</c>
    /// with <c>error</c> and <c>detail</c> fields on failure.
    /// </summary>
    /// <typeparam name="T">The type of the result payload.</typeparam>
    /// <param name="result">The result to convert.</param>
    protected IActionResult ToActionResult<T>(Result<T> result) => result.IsSuccess
        ? Ok(result.Value)
        : BadRequest(new { error = result.Error, detail = result.Detail });
}

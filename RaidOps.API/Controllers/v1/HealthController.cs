using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using RaidOps.Application.Contracts.CQRS;

namespace RaidOps.API.Controllers.v1;

[ApiVersion("1.0")]
public class HealthController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher)
    : ApiControllerBase(commandDispatcher, queryDispatcher)
{
    [HttpGet]
    public IActionResult Get() => Ok(new { status = "ok", version = "v1" });
}
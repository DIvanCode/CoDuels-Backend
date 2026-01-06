using Duely.Application.UseCases.Dtos;
using Duely.Application.UseCases.Features.DuelConfigurations;
using Duely.Infrastructure.Api.Http.Requests.DuelConfigurations;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Duely.Infrastructure.Api.Http.Controllers;

[ApiController]
[Route("duel-configurations")]
[Authorize]
public sealed class DuelConfigurationsController(IMediator mediator) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<DuelConfigurationDto>> CreateAsync(
        [FromBody] CreateDuelConfigurationRequest request,
        CancellationToken cancellationToken)
    {
        var command = new CreateDuelConfigurationCommand
        {
            ShowOpponentCode = request.ShowOpponentCode,
            MaxDurationMinutes = request.MaxDurationMinutes,
            TasksCount = request.TasksCount,
            TasksOrder = request.TasksOrder,
            TasksConfigurations = request.TasksConfigurations
        };

        var result = await mediator.Send(command, cancellationToken);
        return this.HandleResult(result);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<DuelConfigurationDto>> GetAsync(
        [FromRoute] int id,
        CancellationToken cancellationToken)
    {
        var query = new GetDuelConfigurationQuery(id);
        var result = await mediator.Send(query, cancellationToken);
        return this.HandleResult(result);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<DuelConfigurationDto>> UpdateAsync(
        [FromRoute] int id,
        [FromBody] UpdateDuelConfigurationRequest request,
        CancellationToken cancellationToken)
    {
        var command = new UpdateDuelConfigurationCommand
        {
            Id = id,
            ShowOpponentCode = request.ShowOpponentCode,
            MaxDurationMinutes = request.MaxDurationMinutes,
            TasksCount = request.TasksCount,
            TasksOrder = request.TasksOrder,
            TasksConfigurations = request.TasksConfigurations
        };

        var result = await mediator.Send(command, cancellationToken);
        return this.HandleResult(result);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteAsync(
        [FromRoute] int id,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new DeleteDuelConfigurationCommand(id), cancellationToken);
        return this.HandleResult(result);
    }
}


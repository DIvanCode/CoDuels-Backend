using Duely.Application.UseCases.Errors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using FluentResults;

namespace Duely.Infrastructure.Api.Http.Controllers;

public static class ControllerBaseExtensions
{
    public static ActionResult HandleResult(this ControllerBase controller, Result result)
        => result.IsSuccess ? new OkResult() : controller.HandleError(result.Errors);

    public static ActionResult<TResult> HandleResult<TResult>(this ControllerBase controller, Result<TResult> result)
        => result.IsSuccess ? new OkObjectResult(result.Value) : controller.HandleError(result.Errors);

    private static ObjectResult HandleError(this ControllerBase controller, IEnumerable<IError> errors)
    {
        var error = errors.FirstOrDefault();

        var statusCode = error switch
        {
            AuthenticationError => StatusCodes.Status401Unauthorized,
            EntityNotFoundError => StatusCodes.Status404NotFound,
            EntityAlreadyExistsError => StatusCodes.Status409Conflict,
            RateLimitExceededError => StatusCodes.Status429TooManyRequests,
            _ => StatusCodes.Status500InternalServerError
        };

        return CreateDetailedProblemResult(controller, statusCode, error);
    }

    private static ObjectResult CreateDetailedProblemResult(ControllerBase controller, int statusCode, IError error)
    {
        var factory = controller.HttpContext.RequestServices.GetRequiredService<ProblemDetailsFactory>();
        var details = factory.CreateProblemDetails(controller.HttpContext, statusCode, detail: error?.Message ?? string.Empty);
        return controller.Problem(details.Detail, details.Instance, statusCode, details.Title, details.Type);
    }
}
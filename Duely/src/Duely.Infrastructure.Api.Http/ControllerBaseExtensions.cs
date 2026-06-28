using Duely.Domain.Kernel.Errors;
using FluentResults;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Duely.Infrastructure.Api.Http;

internal static class ControllerBaseExtensions
{
    public static ActionResult HandleResult(this ControllerBase controller, Result result)
        => result.IsSuccess ? new OkResult() : controller.HandleError(result.Errors);
    
    public static ActionResult HandleErrorResult(this ControllerBase controller, Result result)
        => controller.HandleError(result.Errors);

    public static ActionResult<TResult> HandleResult<TResult>(this ControllerBase controller, Result<TResult> result)
        => result.IsSuccess ? new OkObjectResult(result.Value) : controller.HandleError(result.Errors);

    private static ObjectResult HandleError(this ControllerBase controller, IEnumerable<IError> errors)
    {
        var error = errors.FirstOrDefault() ?? new UnexpectedError("Непредвиденная ошибка.");

        var statusCode = error switch
        {
            ValidationError or InvalidOperationError => StatusCodes.Status400BadRequest,
            AuthenticationError => StatusCodes.Status401Unauthorized,
            NotFoundError => StatusCodes.Status404NotFound,
            ConflictError => StatusCodes.Status409Conflict,
            ForbiddenError => StatusCodes.Status403Forbidden,
            RateLimitExceededError => StatusCodes.Status429TooManyRequests,
            _ => StatusCodes.Status500InternalServerError
        };

        return CreateDetailedProblemResult(controller, statusCode, error);
    }

    private static ObjectResult CreateDetailedProblemResult(ControllerBase controller, int statusCode, IError error)
    {
        var factory = controller.HttpContext.RequestServices.GetRequiredService<ProblemDetailsFactory>();
        var details = factory.CreateProblemDetails(controller.HttpContext, statusCode, detail: error.Message);
        return controller.Problem(details.Detail, details.Instance, statusCode, details.Title, details.Type);
    }
}
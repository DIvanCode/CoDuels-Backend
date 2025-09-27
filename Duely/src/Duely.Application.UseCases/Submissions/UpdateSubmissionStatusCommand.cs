using Duely.Domain.Models;

using MediatR;
using FluentResults;
namespace Duely.Application.UseCases.Submissions;

public sealed record UpdateSubmissionStatusCommand(
    int SubmissionId,
    SubmissionStatus Status,
    string? Verdict
): IRequest<Result>;
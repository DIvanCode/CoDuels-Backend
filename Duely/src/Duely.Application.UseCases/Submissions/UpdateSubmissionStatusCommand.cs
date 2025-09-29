using Duely.Domain.Models;

using MediatR;
using FluentResults;
namespace Duely.Application.UseCases.Submissions;

public sealed record UpdateSubmissionStatusCommand(
    int SubmissionId,
    string Type,
    SubmissionStatus Status,
    string? Verdict,
    string? Message,            
    string? Error    
): IRequest<Result>;
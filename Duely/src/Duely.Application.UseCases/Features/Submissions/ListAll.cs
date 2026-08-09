using Duely.Application.UseCases.Dtos;
using Duely.Domain.Models;
using Duely.Infrastructure.DataAccess.EntityFramework;
using FluentResults;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Duely.Application.UseCases.Features.Submissions;

public sealed class GetAllSubmissionsQuery : IRequest<Result<List<AdminSubmissionListItemDto>>>
{
    public bool OnlyTesting { get; init; }
}

public sealed class GetAllSubmissionsHandler(Context context)
    : IRequestHandler<GetAllSubmissionsQuery, Result<List<AdminSubmissionListItemDto>>>
{
    public async Task<Result<List<AdminSubmissionListItemDto>>> Handle(
        GetAllSubmissionsQuery query,
        CancellationToken cancellationToken)
    {
        var submissions = context.Submissions.AsNoTracking();
        if (query.OnlyTesting)
        {
            submissions = submissions.Where(submission => submission.Status != SubmissionStatus.Done);
        }

        return await submissions
            .OrderByDescending(submission => submission.SubmitTime)
            .Select(submission => new AdminSubmissionListItemDto
            {
                SubmissionId = submission.Id,
                DuelId = submission.Duel.Id,
                TaskKey = submission.TaskKey,
                Status = submission.Status,
                Language = submission.Language,
                Author = new UserDto
                {
                    Id = submission.User.Id,
                    Nickname = submission.User.Nickname,
                    Rating = submission.User.Rating,
                    CreatedAt = submission.User.CreatedAt
                },
                CreatedAt = submission.SubmitTime,
                Verdict = submission.Verdict,
                IsUpsolving = submission.IsUpsolving
            })
            .ToListAsync(cancellationToken);
    }
}

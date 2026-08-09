using Duely.Application.UseCases.Dtos;
using Duely.Domain.Models;
using Duely.Infrastructure.DataAccess.EntityFramework;
using FluentResults;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Duely.Application.UseCases.Features.Submissions;

public sealed class GetAllSubmissionsQuery : IRequest<Result<List<SubmissionListItemDto>>>
{
    public bool OnlyTesting { get; init; }
}

public sealed class GetAllSubmissionsHandler(Context context)
    : IRequestHandler<GetAllSubmissionsQuery, Result<List<SubmissionListItemDto>>>
{
    public async Task<Result<List<SubmissionListItemDto>>> Handle(
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
            .Select(submission => new SubmissionListItemDto
            {
                SubmissionId = submission.Id,
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

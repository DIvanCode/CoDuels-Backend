using Duely.Infrastructure.DataAccess.EntityFramework;
using MediatR;
using FluentResults;
using Microsoft.EntityFrameworkCore;

namespace Duely.Application.UseCases.Submissions;

public class GetSubmissionHandler : IRequestHandler<GetSubmissionQuery, Result<SubmissionDto>>
{
    private readonly Context _context;

    public GetSubmissionHandler(Context context)
    {
        _context = context;
    }

    public async Task<Result<SubmissionDto>> Handle(GetSubmissionQuery request, CancellationToken cancellationToken)
    {
        var submission = await _context.Submissions.Where(
            s => s.Duel.Id == request.DuelId && s.Id == request.SubmissionId).Select(s => new SubmissionDto {
                SubmissionId = s.Id,
                Verdict = s.Verdict,
                Status = s.Status.ToString().ToLower(),
                CreatedAt = s.SubmitTime,
                Language = s.Language,
                Solution = s.Code
            }).FirstOrDefaultAsync(cancellationToken);
        
        
        if (submission is null)
        {
            return Result.Fail<SubmissionDto>($"Submission {request.SubmissionId} not found");
        }


        return Result.Ok(submission);
    }


}
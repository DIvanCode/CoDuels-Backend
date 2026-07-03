using System.ComponentModel;
using Duely.Domain.Kernel.Entities;
using Duely.Domain.Models.Duels.DomainEvents;
using Duely.Domain.Models.Users.Entities;

namespace Duely.Domain.Models.Duels.Entities;

public sealed class Submission : Entity
{
    private Submission(User user, DuelProblem problem, string source, Language language)
    {
        User = user;
        Problem = problem;
        Source = source;
        Language = language;
        Status = SubmissionStatus.New;
        CreatedAt = DateTime.UtcNow;
    }
    
    public int Id { get; init; }
    public User User { get; init; }
    public DuelProblem Problem { get; init; }
    
    public string Source { get; init; }
    public Language Language { get; init; }
    
    public SubmissionStatus Status { get; private set; }
    public DateTime CreatedAt { get; init; }
    
    // public required bool IsUpsolving { get; init; }
    //
    // public string? Verdict { get; set; }
    // public string? Message { get; set; }
    // public int LastHandledStatusSeqId { get; set; }

    public static Submission Create(User user, DuelProblem problem, string source, Language language)
    {
        var submission = new Submission(user, problem, source, language);
        submission.AddDomainEvent(new SubmissionCreatedDomainEvent(submission));
        return submission;
    }
    
    // ReSharper disable once UnusedMember.Local
#pragma warning disable CS8618, CS9264
    /// <summary>
    /// EF constructor. Do not use explicitly!
    /// </summary>
    [Obsolete(message: "For EF. Do not use explicitly!", error: true)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    private Submission()
    {
    }
#pragma warning restore CS8618, CS9264
}

public enum SubmissionStatus
{
    New = 0,
    Queued = 1,
    Running = 2,
    Done = 3
}

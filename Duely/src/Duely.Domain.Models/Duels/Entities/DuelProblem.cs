using System.ComponentModel;
using Duely.Domain.Kernel.Entities;
using Duely.Domain.Models.Duels.DomainEvents;

namespace Duely.Domain.Models.Duels.Entities;

public sealed class DuelProblem : Entity
{
    public DuelProblem(Duel duel, Problem problem, int position, bool isVisible)
    {
        Duel = duel;
        Problem = problem;
        Position = position;
        IsVisible = isVisible;
    }

    public Duel Duel { get; init; }
    public Problem Problem { get; init; }
    public int Position { get; init; }
    public bool IsVisible { get; private set; }

    public void ChangeVisibility(bool isVisible)
    {
        IsVisible = isVisible;

        AddDomainEvent(new DuelProblemVisibilityChangedDomainEvent(this));
    }
    
    // ReSharper disable once UnusedMember.Local
#pragma warning disable CS8618, CS9264
    /// <summary>
    /// EF constructor. Do not use explicitly!
    /// </summary>
    [Obsolete(message: "For EF. Do not use explicitly!", error: true)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    private DuelProblem()
    {
    }
#pragma warning restore CS8618, CS9264
}

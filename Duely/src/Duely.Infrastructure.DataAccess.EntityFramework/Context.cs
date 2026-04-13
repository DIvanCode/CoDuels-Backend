using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Duely.Domain.Models;
using Duely.Domain.Models.Duels;
using Duely.Domain.Models.Duels.Pending;
using Duely.Domain.Models.Groups;
using Duely.Domain.Models.Outbox;
using Duely.Domain.Models.Tournaments;
using Duely.Domain.Models.UserActions;

namespace Duely.Infrastructure.DataAccess.EntityFramework;

public sealed class Context : DbContext
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Group> Groups => Set<Group>();
    public DbSet<GroupMembership> GroupMemberships => Set<GroupMembership>();
    public DbSet<Duel> Duels => Set<Duel>();
    public DbSet<PendingDuel> PendingDuels => Set<PendingDuel>();
    public DbSet<DuelConfiguration> DuelConfigurations => Set<DuelConfiguration>();
    public DbSet<Tournament> Tournaments => Set<Tournament>();
    public DbSet<Submission> Submissions => Set<Submission>();
    public DbSet<CodeRun> CodeRuns => Set<CodeRun>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<UserAction> UserActions => Set<UserAction>();

    public Context(DbContextOptions<Context> options) : base(options)
    {
    }
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
    }
}

using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Duely.Domain.Models.Duels.Entities;
using Duely.Domain.Models.Groups.Entities;
using Duely.Domain.Models.Tournaments.Entities;
using Duely.Domain.Models.Users.Entities;
using Microsoft.EntityFrameworkCore.Storage;

namespace Duely.Infrastructure.DataAccess.EntityFramework;

public sealed class Context : DbContext
{
    private readonly IDomainEventsDispatcher<Context> _domainEventsDispatcher;

    public Context(DbContextOptions<Context> options, IDomainEventsDispatcher<Context> domainEventsDispatcher)
        : base(options)
    {
        _domainEventsDispatcher = domainEventsDispatcher;
        _domainEventsDispatcher.SetDbContext(this);
    }
    
    public DbSet<User> Users => Set<User>();
    public DbSet<RankedSearch> RankedSearches => Set<RankedSearch>();
    public DbSet<Duel> Duels => Set<Duel>();
    public DbSet<DuelConfiguration> DuelConfigurations => Set<DuelConfiguration>();
    public DbSet<Group> Groups => Set<Group>();
    public DbSet<Tournament> Tournaments => Set<Tournament>();
    // public DbSet<Submission> Submissions => Set<Submission>();
    // public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    
    public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken)
    {
        return Database.BeginTransactionAsync(cancellationToken);
    }

    public Task CommitTransactionAsync(CancellationToken cancellationToken)
    {
        return Database.CommitTransactionAsync(cancellationToken);
    }

    public Task RollbackTransactionAsync(CancellationToken cancellationToken)
    {
        return Database.RollbackTransactionAsync(cancellationToken);
    }
    
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        if (_domainEventsDispatcher is null)
        {
            throw new InvalidOperationException("Domain events dispatcher is not set.");
        }

        await _domainEventsDispatcher.DispatchEventsAsync(cancellationToken);

        return await base.SaveChangesAsync(cancellationToken);
    }
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
    }
}

using System.Reflection;
using Microsoft.EntityFrameworkCore;
// using Duely.Domain.Models.Duels.Entities;
// using Duely.Domain.Models.Groups.Entities;
using Duely.Domain.Models.Users.Entities;
using Duely.Infrastructure.IntegrationEvents.Models;
using Microsoft.EntityFrameworkCore.Storage;

namespace Duely.Infrastructure.DataAccess.EntityFramework;

public sealed class Context : DbContext
{
    private IDbContextTransaction? _transaction;
    private readonly IDomainEventsDispatcher _domainEventsDispatcher;

    public Context(DbContextOptions<Context> options, IDomainEventsDispatcher domainEventsDispatcher)
        : base(options)
    {
        _domainEventsDispatcher = domainEventsDispatcher;
        _domainEventsDispatcher.SetDbContext(this);
    }
    
    public DbSet<User> Users => Set<User>();
    public DbSet<IntegrationEvent> IntegrationEvents => Set<IntegrationEvent>();
    // public DbSet<Group> Groups => Set<Group>();
    // public DbSet<RankedSearch> RankedSearches => Set<RankedSearch>();
    // public DbSet<Duel> Duels => Set<Duel>();
    // public DbSet<DuelConfiguration> DuelConfigurations => Set<DuelConfiguration>();
    // public DbSet<Tournament> Tournaments => Set<Tournament>();
    // public DbSet<Submission> Submissions => Set<Submission>();
    // public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    public async Task BeginTransactionAsync(CancellationToken cancellationToken)
    {
        if (_transaction is not null)
        {
            return;
        }
        
        _transaction = await Database.BeginTransactionAsync(cancellationToken);
    }

    public async Task CommitTransactionAsync(CancellationToken cancellationToken)
    {
        if (_transaction is null)
        {
            return;
        }
        
        await _transaction.CommitAsync(cancellationToken);
        _transaction = null;
    }

    public async Task RollbackTransactionAsync(CancellationToken cancellationToken)
    {
        if (_transaction is null)
        {
            return;
        }
        
        await _transaction.RollbackAsync(cancellationToken);
        _transaction = null;
    }
    
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var transactionExists = _transaction is not null;
        if (!transactionExists)
        {
            await BeginTransactionAsync(cancellationToken);   
        }
        
        var result = await base.SaveChangesAsync(cancellationToken);
        await _domainEventsDispatcher.DispatchEventsAsync(cancellationToken);

        if (!transactionExists)
        {
            await CommitTransactionAsync(cancellationToken);
        }
        
        return result;
    }
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
    }
}

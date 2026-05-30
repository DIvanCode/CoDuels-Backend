using Duely.Domain.Common.DomainEvents;
using Duely.Domain.Common.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Duely.Infrastructure.DataAccess.EntityFramework;

public interface IDomainEventsDispatcher<in TDbContext> where TDbContext : DbContext
{
    void SetDbContext(TDbContext dbContext);
    Task DispatchEventsAsync(CancellationToken cancellationToken = default);
}

internal sealed class DomainEventsDispatcher<TDbContext>
    : IDomainEventsDispatcher<TDbContext> where TDbContext : DbContext
{
    private readonly IPublisher _publisher;

    private TDbContext? _dbContext;

    public DomainEventsDispatcher(IPublisher publisher)
    {
        _publisher = publisher;
    }

    public void SetDbContext(TDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task DispatchEventsAsync(CancellationToken cancellationToken = default)
    {
        if (_dbContext is null)
        {
            throw new InvalidOperationException("DbContext is not set");
        }

        while (HasUnpublishedDomainEvents())
        {
            var entities = _dbContext.ChangeTracker
                .Entries<IEntity>()
                .Where(a => a.Entity.DomainEvents.Count > 0)
                .Select(a => a.Entity)
                .ToArray();

            var domainEvents = new List<IDomainEvent>();

            foreach (var entity in entities)
            {
                domainEvents.AddRange(entity.DomainEvents);

                entity.ClearDomainEvents();
            }

            foreach (var domainEvent in domainEvents)
            {
                await _publisher.Publish(domainEvent, cancellationToken);
            }
        }
    }

    private bool HasUnpublishedDomainEvents()
    {
        if (_dbContext is null)
        {
            throw new InvalidOperationException("DbContext is not set");
        }

        return _dbContext.ChangeTracker
            .Entries<IEntity>()
            .Any(x => x.Entity.DomainEvents.Count > 0);
    }
}

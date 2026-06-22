using Duely.Domain.Kernel.DomainEvents;
using Duely.Domain.Kernel.Entities;
using MediatR;

namespace Duely.Infrastructure.DataAccess.EntityFramework;

public interface IDomainEventsDispatcher
{
    void SetDbContext(Context context);
    Task DispatchEventsAsync(CancellationToken cancellationToken = default);
}

internal sealed class DomainEventsDispatcher(IPublisher publisher) : IDomainEventsDispatcher
{
    private Context? _context;

    public void SetDbContext(Context context)
    {
        _context = context;
    }

    public async Task DispatchEventsAsync(CancellationToken cancellationToken = default)
    {
        if (_context is null)
        {
            throw new InvalidOperationException("Context is not set");
        }

        while (HasUnpublishedDomainEvents())
        {
            var entities = _context.ChangeTracker
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
                await publisher.Publish(domainEvent, cancellationToken);
            }
        }
    }

    private bool HasUnpublishedDomainEvents()
    {
        if (_context is null)
        {
            throw new InvalidOperationException("Context is not set");
        }

        return _context.ChangeTracker
            .Entries<IEntity>()
            .Any(x => x.Entity.DomainEvents.Count > 0);
    }
}

using Duely.Domain.Models.Messages;

namespace Duely.Infrastructure.Gateway.Client.Abstracts;

public interface IMessageSender
{
    Task SendMessage(IEnumerable<int> userIds, Message message, CancellationToken cancellationToken);
}

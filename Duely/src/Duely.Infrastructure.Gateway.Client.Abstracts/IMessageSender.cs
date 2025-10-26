using Duely.Domain.Models.Messages;

namespace Duely.Infrastructure.Gateway.Client.Abstracts;

public interface IMessageSender
{
    Task SendMessage(int userId, Message message, CancellationToken cancellationToken);
}

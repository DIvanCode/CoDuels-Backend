namespace Duely.Infrastructure.Gateway.Client.Abstracts;

public interface IMessageSender
{
    Task SendMessage(Message message, CancellationToken cancellationToken = default);
}

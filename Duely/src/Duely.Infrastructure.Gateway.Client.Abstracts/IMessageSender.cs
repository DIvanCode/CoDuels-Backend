using Duely.Domain.Models.Messages;
using Duely.Domain.Models.Users.Entities;

namespace Duely.Infrastructure.Gateway.Client.Abstracts;

public interface IMessageSender
{
    Task SendMessage(UserId userId, Message message, CancellationToken cancellationToken);
}

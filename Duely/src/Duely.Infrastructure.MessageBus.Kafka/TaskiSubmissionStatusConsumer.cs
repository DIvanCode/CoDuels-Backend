using System.Text.Json;
using Confluent.Kafka;
using Duely.Application.Configuration;
using Duely.Application.UseCases.Submissions;
using Duely.Infrastructure.Gateway.Tasks.Abstracts.Messages;
using MediatR;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;

namespace Duely.Infrastructure.MessageBus.Kafka;
public sealed class TaskiSubmissionStatusConsumer : BackgroundService
{
    private readonly IConsumer<string, TaskiStatusEvent> _consumer;
    private readonly string _topic;
    private readonly IServiceScopeFactory _scopeFactory;
    public TaskiSubmissionStatusConsumer(IServiceScopeFactory scopeFactory, IOptions<KafkaSettings> kafkaSettings)
    {
        _scopeFactory = scopeFactory;
        _topic = kafkaSettings.Value.Topic;
        var config = new ConsumerConfig
        {
            BootstrapServers = kafkaSettings.Value.BootstrapServers,
            GroupId = kafkaSettings.Value.GroupId
        };
        _consumer = new ConsumerBuilder<string, TaskiStatusEvent>(config)
            .SetValueDeserializer(new KafkaValueDeserializer<TaskiStatusEvent>())
            .Build();
    }
    protected override Task ExecuteAsync(CancellationToken cancellationToken){
        Task.Run(() => ConsumeAsync(cancellationToken), cancellationToken);
        return Task.CompletedTask;
    }

    private async Task ConsumeAsync(CancellationToken cancellationToken)
    {
        _consumer.Subscribe(_topic);
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var result = _consumer.Consume(cancellationToken);
                if (result is null) continue;
                var statusEvent = result?.Message?.Value;
                if (statusEvent is null) continue;
                using var scope = _scopeFactory.CreateScope();
                var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
                var cmd = new UpdateSubmissionStatusCommand(statusEvent.SubmissionId,statusEvent.Type,statusEvent.Verdict, statusEvent.Message, statusEvent.Error);
                await mediator.Send(cmd, cancellationToken);

            }
        }
        catch (Exception)
        {

        }
    }
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _consumer.Close();
        await base.StopAsync(cancellationToken);
    }
    private sealed class KafkaValueDeserializer<T> : IDeserializer<T>
    {
        public T Deserialize(ReadOnlySpan<byte> data, bool isNull, SerializationContext context)
        {
            return JsonSerializer.Deserialize<T>(data)!;
        }
    }
}

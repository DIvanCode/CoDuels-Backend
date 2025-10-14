using System.Text.Json;
using Confluent.Kafka;
using Duely.Application.Configuration;
using Duely.Application.UseCases.Submissions;
using Duely.Infrastructure.Gateway.Tasks.Abstracts.Messages;
using MediatR;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Duely.Infrastructure.MessageBus.Kafka;
public sealed class TaskiSubmissionStatusConsumer : BackgroundService
{
    private readonly IConsumer<string, TaskiStatusEvent> _consumer;
    private readonly string _topic;
    private readonly IMediator _mediator;
    public TaskiSubmissionStatusConsumer(IMediator mediator, IOptions<KafkaSettings> kafkaSettings)
    {
        _mediator = mediator;
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
    protected override Task ExecuteAsync(CancellationToken cancellationToken) => ConsumeAsync(cancellationToken);
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
                var cmd = new UpdateSubmissionStatusCommand(statusEvent.SubmissionId,statusEvent.Type,statusEvent.Verdict, statusEvent.Message, statusEvent.Error);
                var sendResult = await _mediator.Send(cmd, cancellationToken);

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

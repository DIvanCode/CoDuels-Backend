using System.Text.Json;
using Confluent.Kafka;
using MediatR;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;
using Duely.Application.UseCases.Features.Submissions;

namespace Duely.Infrastructure.MessageBus.Kafka;
public sealed class TaskiSubmissionStatusConsumer : BackgroundService
{
    private readonly IConsumer<string, TaskiStatusEvent> _consumer;
    private readonly string _topic;
    private readonly IServiceScopeFactory _scopeFactory;

    public TaskiSubmissionStatusConsumer(IServiceScopeFactory scopeFactory, IOptions<KafkaOptions> kafkaOptions)
    {
        _scopeFactory = scopeFactory;

        _topic = kafkaOptions.Value.TaskiTopic;
        var config = new ConsumerConfig
        {
            BootstrapServers = kafkaOptions.Value.BootstrapServers,
            GroupId = kafkaOptions.Value.GroupId
        };

        _consumer = new ConsumerBuilder<string, TaskiStatusEvent>(config)
            .SetValueDeserializer(new KafkaValueDeserializer<TaskiStatusEvent>())
            .Build();
    }
    
    protected override Task ExecuteAsync(CancellationToken cancellationToken)
    {
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
                if (result is null)
                {
                    continue;
                }

                var @event = result?.Message?.Value;
                if (@event is null)
                {
                    continue;
                }

                using var scope = _scopeFactory.CreateScope();
                var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
                if (int.TryParse(@event.SolutionId, out var submissionId))
                {
                    var command = new UpdateSubmissionStatusCommand
                    {
                        SubmissionId = submissionId,
                        Type = @event.Type,
                        Verdict = @event.Verdict,
                        Message = @event.Message,
                        Error = @event.Error
                    };

                    await mediator.Send(command, cancellationToken);
                }

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

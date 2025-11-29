using System.Text.Json;
using Confluent.Kafka;
using MediatR;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;
using Duely.Application.UseCases.Features.UserCodeRuns;

namespace Duely.Infrastructure.MessageBus.Kafka;

public sealed class ExeshSubmissionStatusConsumer : BackgroundService
{
    private readonly IConsumer<string, ExeshStatusEvent> _consumer;
    private readonly string _topic;
    private readonly IServiceScopeFactory _scopeFactory;

    public ExeshSubmissionStatusConsumer(IServiceScopeFactory scopeFactory, IOptions<KafkaOptions> kafkaOptions)
    {
        _scopeFactory = scopeFactory;

        _topic = kafkaOptions.Value.ExeshTopic;
        var config = new ConsumerConfig
        {
            BootstrapServers = kafkaOptions.Value.BootstrapServers,
            GroupId = kafkaOptions.Value.GroupId,
            AutoOffsetReset = AutoOffsetReset.Earliest
        };

        _consumer = new ConsumerBuilder<string, ExeshStatusEvent>(config)
            .SetValueDeserializer(new KafkaValueDeserializer<ExeshStatusEvent>())
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

                var command = new UpdateUserCodeRunStatusCommand
                {
                    ExecutionId = @event.ExecutionId,
                    Type = @event.Type,
                    StepName = @event.StepName,
                    Status = @event.Status,
                    Output = @event.Output,
                    Error = @event.Error
                };

                await mediator.Send(command, cancellationToken);
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
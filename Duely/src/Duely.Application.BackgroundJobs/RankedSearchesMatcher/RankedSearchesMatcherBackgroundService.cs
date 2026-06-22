// using Microsoft.Extensions.Hosting;
// using Microsoft.Extensions.Logging;
// using Microsoft.Extensions.Options;
//
// namespace Duely.Application.BackgroundJobs.RankedSearchesMatcher;
//
// internal sealed class RankedSearchesMatcherBackgroundService(
//     IServiceProvider serviceProvider,
//     IOptions<RankedSearchesMatcherOptions> options,
//     ILogger<RankedSearchesMatcherBackgroundService> logger)
//     : BackgroundService
// {
//     protected override async Task ExecuteAsync(CancellationToken cancellationToken)
//     {
//         while (!cancellationToken.IsCancellationRequested)
//         {
//             await WorkAsync(cancellationToken);
//             await Task.Delay(options.Value.IntervalMs, cancellationToken);
//         }
//     }
//     
//     private async Task WorkAsync(CancellationToken cancellationToken)
//     {
//         // using var scope = serviceProvider.CreateScope();
//         //
//         // var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
//         // var result = await mediator.Send(new TryCreateDuelCommand(), cancellationToken);
//         // if (result.IsFailed)
//         // {
//         //     logger.LogWarning("failed to create duel: {Reason}",
//         //         string.Join("\n", result.Errors.Select(error => error.Message)));
//         // }
//     }
// }

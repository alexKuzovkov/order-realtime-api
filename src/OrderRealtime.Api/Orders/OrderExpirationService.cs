using Microsoft.Extensions.Options;

namespace OrderRealtime.Api.Orders;

public sealed class OrderExpirationOptions
{
    public const string SectionName = "OrderExpiration";
    public int LifetimeSeconds { get; init; } = 15;
    public int ScanIntervalSeconds { get; init; } = 1;
}

public sealed class OrderExpirationService(
    IOrderStore store,
    IOrderService orderService,
    TimeProvider timeProvider,
    IOptions<OrderExpirationOptions> options,
    ILogger<OrderExpirationService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;
        var lifetime = TimeSpan.FromSeconds(settings.LifetimeSeconds);
        using var timer = new PeriodicTimer(
            TimeSpan.FromSeconds(settings.ScanIntervalSeconds), timeProvider);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            foreach (var order in store.GetExpired(timeProvider.GetUtcNow(), lifetime))
            {
                if (await orderService.CancelOrderAsync(order, stoppingToken))
                    logger.LogInformation("Order {OrderId} expired and was cancelled", order.Id);
            }
        }
    }
}

namespace OrderRealtime.Api.Orders;

public sealed record CreateOrderCommand(
    string UserId,
    string ClientOrderId,
    string Symbol,
    decimal Price,
    int Volume);

public sealed record CreateOrderResult(OrderResponse Order, bool WasCreated);

public interface IOrderService
{
    Task<CreateOrderResult> CreateOrderAsync(
        CreateOrderCommand command, CancellationToken cancellationToken);

    Task<bool> CancelOrderAsync(Order order, CancellationToken cancellationToken);
    IReadOnlyCollection<OrderResponse> GetActiveOrders(string userId);
}

// Application boundary: the use case knows nothing about SignalR.
public interface IOrderUpdateNotifier
{
    Task NotifyOrderUpdatedAsync(
        string userId, OrderResponse order, CancellationToken cancellationToken);
}

public sealed class OrderService(
    IOrderStore store,
    IOrderUpdateNotifier notifier,
    IOrderBusinessValidator businessValidator,
    TimeProvider timeProvider,
    ILogger<OrderService> logger) : IOrderService
{
    public async Task<CreateOrderResult> CreateOrderAsync(
        CreateOrderCommand command, CancellationToken cancellationToken)
    {
        var symbol = command.Symbol.Trim().ToUpperInvariant();
        var candidate = Order.Create(
            Guid.NewGuid(),
            command.UserId,
            command.ClientOrderId,
            symbol,
            command.Price,
            command.Volume,
            timeProvider.GetUtcNow());
        await businessValidator.ValidateAsync(command, cancellationToken);
        var addResult = store.GetOrAdd(candidate);

        var response = addResult.Order.ToResponse();
        if (!addResult.WasAdded)
        {
            if (!addResult.Order.HasSameTerms(symbol, command.Price, command.Volume))
                throw new IdempotencyConflictException(command.ClientOrderId);

            logger.LogInformation(
                "Idempotent retry for order {OrderId}; UserId: {UserId}; ClientOrderId: {ClientOrderId}",
                response.Id, command.UserId, command.ClientOrderId);
            return new CreateOrderResult(response, false);
        }

        await notifier.NotifyOrderUpdatedAsync(command.UserId, response, cancellationToken);
        logger.LogInformation(
            "Order {OrderId} created; UserId: {UserId}; ClientOrderId: {ClientOrderId}",
            response.Id, command.UserId, command.ClientOrderId);

        return new CreateOrderResult(response, true);
    }

    public async Task<bool> CancelOrderAsync(
        Order order, CancellationToken cancellationToken)
    {
        if (!order.TryCancel()) return false;

        await notifier.NotifyOrderUpdatedAsync(
            order.UserId, order.ToResponse(), cancellationToken);
        logger.LogInformation(
            "Order {OrderId} cancelled; UserId: {UserId}", order.Id, order.UserId);

        return true;
    }

    public IReadOnlyCollection<OrderResponse> GetActiveOrders(string userId) =>
        [.. store.GetActiveByUser(userId).Select(order => order.ToResponse())];
}

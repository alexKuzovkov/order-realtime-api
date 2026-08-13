using Microsoft.AspNetCore.SignalR;

namespace OrderRealtime.Api.Orders;

public interface IOrderService
{
    Task<OrderDto> CreateOrderAsync(
        string userId, CreateOrderRequest request, CancellationToken cancellationToken);

    Task<bool> CancelOrderAsync(Order order, CancellationToken cancellationToken);
    IReadOnlyCollection<OrderDto> GetActiveOrders(string userId);
}

public sealed class OrderService(
    IOrderStore store,
    IHubContext<OrdersHub, IOrderClient> hubContext,
    TimeProvider timeProvider) : IOrderService
{
    public async Task<OrderDto> CreateOrderAsync(
        string userId, CreateOrderRequest request, CancellationToken cancellationToken)
    {
        var order = store.Add(new Order
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Symbol = request.Symbol.Trim().ToUpperInvariant(),
            Price = request.Price,
            Volume = request.Volume,
            CreatedAt = timeProvider.GetUtcNow()
        });

        var dto = order.ToDto();
        await hubContext.Clients.Group(OrdersHub.UserGroup(userId))
            .ReceiveOrderUpdate(dto)
            .WaitAsync(cancellationToken);

        return dto;
    }

    public async Task<bool> CancelOrderAsync(
        Order order, CancellationToken cancellationToken)
    {
        if (!order.TryCancel()) return false;

        await hubContext.Clients.Group(OrdersHub.UserGroup(order.UserId))
            .ReceiveOrderUpdate(order.ToDto())
            .WaitAsync(cancellationToken);

        return true;
    }

    public IReadOnlyCollection<OrderDto> GetActiveOrders(string userId) =>
        store.GetActiveByUser(userId).Select(order => order.ToDto()).ToArray();
}

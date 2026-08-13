using Microsoft.AspNetCore.SignalR;

namespace OrderRealtime.Api.Orders;

// Infrastructure adapter for the application notification port.
public sealed class SignalROrderUpdateNotifier(
    IHubContext<OrdersHub, IOrderClient> hubContext) : IOrderUpdateNotifier
{
    public Task NotifyOrderUpdatedAsync(
        string userId, OrderResponse order, CancellationToken cancellationToken) =>
        hubContext.Clients.Group(OrdersHub.UserGroup(userId))
            .ReceiveOrderUpdate(order)
            .WaitAsync(cancellationToken);
}

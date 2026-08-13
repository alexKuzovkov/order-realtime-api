using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace OrderRealtime.Api.Orders;

[Authorize]
public sealed class OrdersHub(IOrderService orderService) : Hub<IOrderClient>
{
    public override async Task OnConnectedAsync()
    {
        var userId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new HubException("Authenticated user identifier is missing.");

        // Группа объединяет все вкладки и устройства одного пользователя.
        await Groups.AddToGroupAsync(Context.ConnectionId, UserGroup(userId));
        await Clients.Caller.ReceiveInitialOrders(orderService.GetActiveOrders(userId));
        await base.OnConnectedAsync();
    }

    public static string UserGroup(string userId) => $"orders:user:{userId}";
}

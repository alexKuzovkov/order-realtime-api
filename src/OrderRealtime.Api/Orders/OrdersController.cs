using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace OrderRealtime.Api.Orders;

[ApiController]
[Authorize]
[Route("api/orders")]
public sealed class OrdersController(IOrderService orderService) : ControllerBase
{
    [HttpGet("active")]
    public ActionResult<IReadOnlyCollection<OrderDto>> GetActiveOrders()
    {
        var userId = GetUserId();
        return Ok(orderService.GetActiveOrders(userId));
    }

    [HttpPost]
    [ProducesResponseType<OrderDto>(StatusCodes.Status201Created)]
    public async Task<ActionResult<OrderDto>> CreateOrderAsync(
        [FromBody] CreateOrderRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();

        var order = await orderService.CreateOrderAsync(userId, request, cancellationToken);
        return Created($"/api/orders/{order.Id}", order);
    }

    private string GetUserId() => User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new UnauthorizedAccessException("Authenticated user identifier is missing.");
}

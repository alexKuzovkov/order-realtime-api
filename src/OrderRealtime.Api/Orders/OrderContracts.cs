using System.ComponentModel.DataAnnotations;

namespace OrderRealtime.Api.Orders;

public sealed record CreateOrderRequest
{
    [Required, StringLength(100, MinimumLength = 8)]
    public required string ClientOrderId { get; init; }

    [Required, StringLength(20, MinimumLength = 1)]
    [RegularExpression(@"^[A-Za-z0-9._/-]+$")]
    public required string Symbol { get; init; }

    [Range(typeof(decimal), "0.00000001", "1000000000000",
        ParseLimitsInInvariantCulture = true)]
    public decimal Price { get; init; }

    [Range(1, int.MaxValue)]
    public int Volume { get; init; }
}

public sealed record OrderResponse(
    Guid Id,
    string ClientOrderId,
    string Symbol,
    decimal Price,
    int Volume,
    DateTimeOffset CreatedAt,
    OrderState State);

public interface IOrderClient
{
    Task ReceiveOrderUpdate(OrderResponse order);
    Task ReceiveInitialOrders(IReadOnlyCollection<OrderResponse> orders);
}

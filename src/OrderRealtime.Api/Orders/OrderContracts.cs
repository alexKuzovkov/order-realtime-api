using System.ComponentModel.DataAnnotations;

namespace OrderRealtime.Api.Orders;

public sealed record CreateOrderRequest
{
    [Required, StringLength(20, MinimumLength = 1)]
    [RegularExpression(@"^[A-Za-z0-9._/-]+$")]
    public required string Symbol { get; init; }

    [Range(typeof(decimal), "0.00000001", "1000000000000",
        ParseLimitsInInvariantCulture = true)]
    public decimal Price { get; init; }

    [Range(1, int.MaxValue)]
    public int Volume { get; init; }
}

public sealed record OrderDto(
    Guid Id,
    string Symbol,
    decimal Price,
    int Volume,
    DateTimeOffset CreatedAt,
    bool IsActive);

public interface IOrderClient
{
    Task ReceiveOrderUpdate(OrderDto order);
    Task ReceiveInitialOrders(IReadOnlyCollection<OrderDto> orders);
}

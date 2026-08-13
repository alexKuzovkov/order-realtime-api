namespace OrderRealtime.Api.Orders;

public sealed class Order
{
    private int _isActive = 1;

    public required Guid Id { get; init; }
    public required string Symbol { get; init; }
    public required decimal Price { get; init; }
    public required int Volume { get; init; }
    public required string UserId { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public bool IsActive => Volatile.Read(ref _isActive) == 1;

    // Только один конкурентный поток может успешно отменить ордер.
    public bool TryCancel() => Interlocked.Exchange(ref _isActive, 0) == 1;

    public OrderDto ToDto() => new(Id, Symbol, Price, Volume, CreatedAt, IsActive);
}

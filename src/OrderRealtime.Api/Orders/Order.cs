namespace OrderRealtime.Api.Orders;

public enum OrderState
{
    Active = 1,
    Completed = 2,
    Inactive = 3,
    Faulted = 4
}

public sealed class Order
{
    private int _state = (int)OrderState.Active;

    private Order(
        Guid id,
        string clientOrderId,
        string symbol,
        decimal price,
        int volume,
        string userId,
        DateTimeOffset createdAt)
    {
        Id = id;
        ClientOrderId = clientOrderId;
        Symbol = symbol;
        Price = price;
        Volume = volume;
        UserId = userId;
        CreatedAt = createdAt;
    }

    public Guid Id { get; }
    public string ClientOrderId { get; }
    public string Symbol { get; }
    public decimal Price { get; }
    public int Volume { get; }
    public string UserId { get; }
    public DateTimeOffset CreatedAt { get; }
    public OrderState State => (OrderState)Volatile.Read(ref _state);

    public static Order Create(
        Guid id,
        string userId,
        string clientOrderId,
        string symbol,
        decimal price,
        int volume,
        DateTimeOffset createdAt)
    {
        if (id == Guid.Empty) throw new DomainValidationException("Order ID is required.");
        if (string.IsNullOrWhiteSpace(userId)) throw new DomainValidationException("User ID is required.");
        if (string.IsNullOrWhiteSpace(clientOrderId)) throw new DomainValidationException("ClientOrderId is required.");
        if (clientOrderId.Length is < 8 or > 100) throw new DomainValidationException("ClientOrderId length must be between 8 and 100.");
        if (string.IsNullOrWhiteSpace(symbol)) throw new DomainValidationException("Symbol is required.");
        if (symbol.Length > 20 || symbol.Any(character =>
                !char.IsAsciiLetterOrDigit(character) && character is not ('.' or '_' or '/' or '-')))
            throw new DomainValidationException("Symbol has an invalid format.");
        if (price is <= 0 or > 1_000_000_000_000m)
            throw new DomainValidationException("Price is outside the supported range.");
        if (volume <= 0) throw new DomainValidationException("Volume must be greater than zero.");

        return new Order(id, clientOrderId, symbol, price, volume, userId, createdAt);
    }

    public bool HasSameTerms(string symbol, decimal price, int volume) =>
        Symbol == symbol && Price == price && Volume == volume;

    public bool TryComplete() => TryTransitionFromActive(OrderState.Completed);

    public bool TryDeactivate() => TryTransitionFromActive(OrderState.Inactive);

    public bool TryMarkFaulted() => TryTransitionFromActive(OrderState.Faulted);

    private bool TryTransitionFromActive(OrderState targetState) =>
        Interlocked.CompareExchange(
            ref _state,
            (int)targetState,
            (int)OrderState.Active) == (int)OrderState.Active;

    public OrderResponse ToResponse() => new(
        Id, ClientOrderId, Symbol, Price, Volume, CreatedAt, State);
}

public sealed class DomainValidationException(string message) : Exception(message);

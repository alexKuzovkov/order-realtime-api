using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Memory;

namespace OrderRealtime.Api.Orders;

public interface IOrderStore
{
    OrderAddResult GetOrAdd(Order order);
    IReadOnlyCollection<Order> GetActiveByUser(string userId);
    IReadOnlyCollection<Order> GetExpired(DateTimeOffset now, TimeSpan lifetime);
}

public sealed record OrderAddResult(Order Order, bool WasAdded);

public sealed class MemoryOrderStore(IMemoryCache cache) : IOrderStore
{
    private const string CacheKey = "active-orders";

    private ConcurrentDictionary<(string UserId, string ClientOrderId), Order> Orders => cache.GetOrCreate(
        CacheKey,
        entry =>
        {
            entry.Priority = CacheItemPriority.NeverRemove;
            return new ConcurrentDictionary<(string, string), Order>();
        })!;

    public OrderAddResult GetOrAdd(Order order)
    {
        var key = (order.UserId, order.ClientOrderId);
        var stored = Orders.GetOrAdd(key, order);
        return new OrderAddResult(stored, ReferenceEquals(stored, order));
    }

    public IReadOnlyCollection<Order> GetActiveByUser(string userId) =>
        [.. Orders.Values.Where(order => order.IsActive && order.UserId == userId)];

    public IReadOnlyCollection<Order> GetExpired(DateTimeOffset now, TimeSpan lifetime) =>
        [.. Orders.Values.Where(order =>
            order.IsActive && now - order.CreatedAt >= lifetime)];
}

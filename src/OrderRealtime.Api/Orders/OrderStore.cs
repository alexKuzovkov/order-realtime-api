using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Memory;

namespace OrderRealtime.Api.Orders;

public interface IOrderStore
{
    Order Add(Order order);
    IReadOnlyCollection<Order> GetActiveByUser(string userId);
    IReadOnlyCollection<Order> GetExpired(DateTimeOffset now, TimeSpan lifetime);
}

public sealed class MemoryOrderStore(IMemoryCache cache) : IOrderStore
{
    private const string CacheKey = "active-orders";

    private ConcurrentDictionary<Guid, Order> Orders => cache.GetOrCreate(
        CacheKey,
        entry =>
        {
            entry.Priority = CacheItemPriority.NeverRemove;
            return new ConcurrentDictionary<Guid, Order>();
        })!;

    public Order Add(Order order)
    {
        if (!Orders.TryAdd(order.Id, order))
            throw new InvalidOperationException($"Order {order.Id} already exists.");
        return order;
    }

    public IReadOnlyCollection<Order> GetActiveByUser(string userId) => Orders.Values
        .Where(order => order.IsActive && order.UserId == userId)
        .ToArray();

    public IReadOnlyCollection<Order> GetExpired(DateTimeOffset now, TimeSpan lifetime) =>
        Orders.Values
            .Where(order => order.IsActive && now - order.CreatedAt >= lifetime)
            .ToArray();
}

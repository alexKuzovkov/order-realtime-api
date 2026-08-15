using Microsoft.Extensions.Caching.Memory;
using OrderRealtime.Api.Orders;

namespace OrderRealtime.Api.Tests;

public sealed class MemoryOrderStoreTests
{
    [Fact]
    public void GetById_ReturnsOnlyOrdersOwnedByUser()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var store = new MemoryOrderStore(cache);
        var order = store.GetOrAdd(TestOrder.Create("user-1")).Order;

        Assert.Same(order, store.GetById("user-1", order.Id));
        Assert.Null(store.GetById("user-2", order.Id));
    }

    [Fact]
    public void GetActiveByUser_ReturnsOnlyActiveOrdersOwnedByUser()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var store = new MemoryOrderStore(cache);
        var active = store.GetOrAdd(TestOrder.Create("user-1")).Order;
        var inactive = store.GetOrAdd(TestOrder.Create("user-1")).Order;
        var completed = store.GetOrAdd(TestOrder.Create("user-1")).Order;
        var faulted = store.GetOrAdd(TestOrder.Create("user-1")).Order;
        store.GetOrAdd(TestOrder.Create("user-2"));

        inactive.TryDeactivate();
        completed.TryComplete();
        faulted.TryMarkFaulted();

        var result = store.GetActiveByUser("user-1");

        Assert.Same(active, Assert.Single(result));
    }

    [Fact]
    public void GetExpired_ReturnsOnlyActiveOrdersPastLifetime()
    {
        var now = new DateTimeOffset(2026, 8, 13, 12, 0, 0, TimeSpan.Zero);
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var store = new MemoryOrderStore(cache);
        var expired = store.GetOrAdd(TestOrder.Create(createdAt: now.AddSeconds(-11))).Order;
        store.GetOrAdd(TestOrder.Create(createdAt: now.AddSeconds(-9)));
        var inactive = store.GetOrAdd(TestOrder.Create(createdAt: now.AddMinutes(-1))).Order;
        inactive.TryDeactivate();

        var result = store.GetExpired(now, TimeSpan.FromSeconds(10));

        Assert.Same(expired, Assert.Single(result));
    }
}

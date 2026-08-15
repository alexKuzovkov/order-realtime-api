using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using OrderRealtime.Api.Orders;

namespace OrderRealtime.Api.Tests;

public sealed class OrderServiceTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 8, 13, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task CreateOrderAsync_NormalizesAndStoresOrder_ThenNotifiesUser()
    {
        var store = Substitute.For<IOrderStore>();
        store.GetOrAdd(Arg.Any<Order>()).Returns(call =>
            new OrderAddResult(call.Arg<Order>(), true));
        var notifier = Substitute.For<IOrderUpdateNotifier>();
        notifier.NotifyOrderUpdatedAsync(
                Arg.Any<string>(), Arg.Any<OrderResponse>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        var service = CreateService(store, notifier);
        var command = CreateCommand();

        var result = await service.CreateOrderAsync(command, CancellationToken.None);

        Assert.True(result.WasCreated);
        Assert.Equal("AAPL", result.Order.Symbol);
        Assert.Equal(Now, result.Order.CreatedAt);
        Assert.Equal(OrderState.Active, result.Order.State);
        store.Received(1).GetOrAdd(Arg.Is<Order>(order =>
            order.UserId == command.UserId &&
            order.ClientOrderId == command.ClientOrderId &&
            order.Symbol == "AAPL"));
        await notifier.Received(1).NotifyOrderUpdatedAsync(
            command.UserId,
            Arg.Is<OrderResponse>(order =>
                order.Id == result.Order.Id && order.State == OrderState.Active),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateOrderAsync_WithSameClientOrderId_IsIdempotent()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var store = new MemoryOrderStore(cache);
        var notifier = Substitute.For<IOrderUpdateNotifier>();
        notifier.NotifyOrderUpdatedAsync(
                Arg.Any<string>(), Arg.Any<OrderResponse>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        var service = CreateService(store, notifier);
        var command = CreateCommand();

        var results = await Task.WhenAll(Enumerable.Range(0, 20)
            .Select(_ => service.CreateOrderAsync(command, CancellationToken.None)));

        Assert.Single(results, result => result.WasCreated);
        Assert.Single(results.Select(result => result.Order.Id).Distinct());
        Assert.Single(store.GetActiveByUser(command.UserId));
        await notifier.Received(1).NotifyOrderUpdatedAsync(
            command.UserId, Arg.Any<OrderResponse>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateOrderAsync_WithReusedClientOrderIdAndDifferentPayload_ThrowsConflict()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var store = new MemoryOrderStore(cache);
        var notifier = Substitute.For<IOrderUpdateNotifier>();
        notifier.NotifyOrderUpdatedAsync(
                Arg.Any<string>(), Arg.Any<OrderResponse>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        var service = CreateService(store, notifier);
        var command = CreateCommand();
        await service.CreateOrderAsync(command, CancellationToken.None);
        var conflicting = command with { Volume = command.Volume + 1 };

        await Assert.ThrowsAsync<IdempotencyConflictException>(() =>
            service.CreateOrderAsync(conflicting, CancellationToken.None));

        Assert.Single(store.GetActiveByUser(command.UserId));
        await notifier.Received(1).NotifyOrderUpdatedAsync(
            command.UserId, Arg.Any<OrderResponse>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeactivateOrderAsync_NotifiesExactlyOnce_WhenCalledConcurrently()
    {
        var store = Substitute.For<IOrderStore>();
        var notifier = Substitute.For<IOrderUpdateNotifier>();
        notifier.NotifyOrderUpdatedAsync(
                Arg.Any<string>(), Arg.Any<OrderResponse>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        var service = CreateService(store, notifier);
        var order = TestOrder.Create("user-1", Now);

        var results = await Task.WhenAll(
            Enumerable.Range(0, 10)
                .Select(_ => service.DeactivateOrderAsync(order, CancellationToken.None)));

        Assert.Single(results, result => result);
        Assert.Equal(OrderState.Inactive, order.State);
        await notifier.Received(1).NotifyOrderUpdatedAsync(
            order.UserId,
            Arg.Is<OrderResponse>(update =>
                update.Id == order.Id && update.State == OrderState.Inactive),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public void GetOrder_ReturnsResponseForOwnerOnly()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var store = new MemoryOrderStore(cache);
        var order = store.GetOrAdd(TestOrder.Create("user-1", Now)).Order;
        var service = CreateService(store, Substitute.For<IOrderUpdateNotifier>());

        var ownerResult = Assert.IsType<OrderResponse>(service.GetOrder("user-1", order.Id));
        var otherUserResult = service.GetOrder("user-2", order.Id);

        Assert.Equal(OrderState.Active, ownerResult.State);
        Assert.Null(otherUserResult);
    }

    private static OrderService CreateService(
        IOrderStore store,
        IOrderUpdateNotifier notifier) => new(
            store,
            notifier,
            new PassThroughBusinessValidator(),
            new FixedTimeProvider(Now),
            NullLogger<OrderService>.Instance);

    private static CreateOrderCommand CreateCommand() => new(
        UserId: "user-1",
        ClientOrderId: "client-order-0001",
        Symbol: " aapl ",
        Price: 225.50m,
        Volume: 10);

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class PassThroughBusinessValidator : IOrderBusinessValidator
    {
        public ValueTask ValidateAsync(
            CreateOrderCommand command, CancellationToken cancellationToken) =>
            ValueTask.CompletedTask;
    }
}

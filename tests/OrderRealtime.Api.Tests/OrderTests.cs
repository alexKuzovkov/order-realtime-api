using OrderRealtime.Api.Orders;

namespace OrderRealtime.Api.Tests;

public sealed class OrderTests
{
    [Fact]
    public async Task TryCancel_AllowsOnlyOneConcurrentCancellation()
    {
        var order = TestOrder.Create();

        var attempts = await Task.WhenAll(
            Enumerable.Range(0, 20).Select(_ => Task.Run(order.TryCancel)));

        Assert.Single(attempts, result => result);
        Assert.False(order.IsActive);
    }

    [Theory]
    [InlineData("", "client-order-0001", "AAPL", "User ID is required.")]
    [InlineData("user-1", "", "AAPL", "ClientOrderId is required.")]
    [InlineData("user-1", "client-order-0001", "", "Symbol is required.")]
    public void Create_WithInvalidIdentityFields_RejectsInvalidDomainState(
        string userId, string clientOrderId, string symbol, string expectedMessage)
    {
        var exception = Assert.Throws<DomainValidationException>(() => Order.Create(
            Guid.NewGuid(), userId, clientOrderId, symbol, 1m, 1, DateTimeOffset.UtcNow));

        Assert.Equal(expectedMessage, exception.Message);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-1, 1)]
    [InlineData(1, 0)]
    [InlineData(1, -1)]
    public void Create_WithNonPositivePriceOrVolume_RejectsInvalidDomainState(
        decimal price, int volume)
    {
        Assert.Throws<DomainValidationException>(() => Order.Create(
            Guid.NewGuid(), "user-1", "client-order-0001", "AAPL",
            price, volume, DateTimeOffset.UtcNow));
    }
}

internal static class TestOrder
{
    public static Order Create(
        string userId = "user-1",
        DateTimeOffset? createdAt = null) => Order.Create(
            Guid.NewGuid(),
            userId,
            Guid.NewGuid().ToString("N"),
            "AAPL",
            225.50m,
            10,
            createdAt ?? DateTimeOffset.UtcNow);
}

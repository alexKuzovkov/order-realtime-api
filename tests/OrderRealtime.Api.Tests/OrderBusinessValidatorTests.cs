using Microsoft.Extensions.Options;
using OrderRealtime.Api.Orders;

namespace OrderRealtime.Api.Tests;

public sealed class OrderBusinessValidatorTests
{
    private readonly OrderBusinessValidator _validator = new(Options.Create(
        new OrderLimitsOptions { MaxVolume = 100, MaxNotional = 10_000m }));

    [Fact]
    public async Task ValidateAsync_WhenVolumeExceedsLimit_ThrowsBusinessError()
    {
        var command = ValidCommand() with { Volume = 101 };

        await Assert.ThrowsAsync<BusinessRuleValidationException>(async () =>
            await _validator.ValidateAsync(command, CancellationToken.None));
    }

    [Fact]
    public async Task ValidateAsync_WhenNotionalExceedsLimit_ThrowsBusinessError()
    {
        var command = ValidCommand() with { Price = 101m, Volume = 100 };

        await Assert.ThrowsAsync<BusinessRuleValidationException>(async () =>
            await _validator.ValidateAsync(command, CancellationToken.None));
    }

    [Fact]
    public async Task ValidateAsync_WhenOrderIsWithinLimits_CompletesSuccessfully()
    {
        await _validator.ValidateAsync(ValidCommand(), CancellationToken.None);
    }

    private static CreateOrderCommand ValidCommand() => new(
        "user-1", "client-order-0001", "AAPL", 100m, 100);
}

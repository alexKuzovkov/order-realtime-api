using Microsoft.Extensions.Options;

namespace OrderRealtime.Api.Orders;

public sealed class OrderLimitsOptions
{
    public const string SectionName = "OrderLimits";
    public int MaxVolume { get; init; } = 100_000;
    public decimal MaxNotional { get; init; } = 10_000_000m;
}

public interface IOrderBusinessValidator
{
    ValueTask ValidateAsync(
        CreateOrderCommand command, CancellationToken cancellationToken);
}

public sealed class OrderBusinessValidator(
    IOptions<OrderLimitsOptions> options) : IOrderBusinessValidator
{
    public ValueTask ValidateAsync(
        CreateOrderCommand command, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var limits = options.Value;

        if (command.Volume > limits.MaxVolume)
            throw new BusinessRuleValidationException(
                $"Volume cannot exceed {limits.MaxVolume}.");

        if (command.Price * command.Volume > limits.MaxNotional)
            throw new BusinessRuleValidationException(
                $"Order notional cannot exceed {limits.MaxNotional}.");

        return ValueTask.CompletedTask;
    }
}

public sealed class BusinessRuleValidationException(string message) : Exception(message);

public sealed class IdempotencyConflictException(string clientOrderId) : Exception(
    $"ClientOrderId '{clientOrderId}' was already used with different order parameters.");

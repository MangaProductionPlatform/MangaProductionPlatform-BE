namespace MangaERP.BuildingBlocks.Domain.ValueObjects;

/// <summary>
/// Value object representing a monetary amount for assistant earnings calculations.
/// </summary>
public sealed record Money(decimal Amount, string Currency = "USD")
{
    public static Money Zero() => new(0);

    public static Money Of(decimal amount, string currency = "USD")
    {
        if (amount < 0)
            throw new ArgumentException("Money amount cannot be negative.", nameof(amount));
        return new Money(amount, currency);
    }

    public Money Add(Money other)
    {
        if (Currency != other.Currency)
            throw new InvalidOperationException($"Cannot add amounts of different currencies: {Currency} vs {other.Currency}");
        return new Money(Amount + other.Amount, Currency);
    }

    public override string ToString() => $"{Amount:F2} {Currency}";
}

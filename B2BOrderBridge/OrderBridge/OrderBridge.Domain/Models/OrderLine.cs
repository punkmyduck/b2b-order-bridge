using Shared.Domain;

namespace OrderBridge.Domain.Models;

public class OrderLine : Entity<Guid>
{
    public const int SkuMaxLength = 128;
    public const int NameMaxLength = 128;

    public string Sku { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    public decimal Quantity { get; private set; }
    public decimal UnitPrice { get; private set; }
    public decimal LineTotal { get; private set; }

    public OrderLine(
        Guid id,
        string sku,
        string name,
        decimal quantity,
        decimal unitPrice)
    {
        Id = Guard.NotEmpty(id, nameof(Id));
        Sku = Guard.RequiredString(sku, SkuMaxLength, 1, nameof(Sku));
        Name = Guard.RequiredString(name, NameMaxLength, 1, nameof(Name));

        Quantity = Guard.Positive(quantity, nameof(Quantity));
        UnitPrice = Guard.Money(unitPrice, parameterName: nameof(UnitPrice));

        LineTotal = decimal.Round(Quantity * UnitPrice, 2, MidpointRounding.AwayFromZero);
    }
}


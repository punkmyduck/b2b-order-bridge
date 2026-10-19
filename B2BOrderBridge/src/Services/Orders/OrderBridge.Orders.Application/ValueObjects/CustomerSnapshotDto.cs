using OrderBridge.Orders.Domain.ValueObjects;

namespace OrderBridge.Orders.Application.ValueObjects;

public record CustomerSnapshotDto(
    string CompanyName,
    string TaxId,
    string ContactEmail)
{
    public static CustomerSnapshot ToDomain(CustomerSnapshotDto dto) => new(dto.CompanyName, dto.TaxId, dto.ContactEmail);

    public static CustomerSnapshotDto FromDomain(CustomerSnapshot domain) => new(domain.CompanyName, domain.TaxId, domain.ContactEmail);
}
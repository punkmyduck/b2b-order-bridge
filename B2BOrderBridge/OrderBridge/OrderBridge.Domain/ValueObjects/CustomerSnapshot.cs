using Shared.Domain;

namespace OrderBridge.Domain.ValueObjects;

public sealed record CustomerSnapshot
{
    public const int CompanyNameMaxLength = 128;
    public const int TaxIdMaxLength = 512;
    public const int ContactEmailMaxLength = 256;

    public string CompanyName { get; }
    public string TaxId { get; }
    public string ContactEmail { get; }

    public CustomerSnapshot(
        string companyName,
        string taxId,
        string contactEmail)
    {
        CompanyName = Guard.RequiredString(companyName, maxLength: CompanyNameMaxLength);
        TaxId = Guard.RequiredString(taxId, maxLength: TaxIdMaxLength);
        ContactEmail = Guard.RequiredString(contactEmail, maxLength: ContactEmailMaxLength);
    }
}


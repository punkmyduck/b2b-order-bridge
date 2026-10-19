namespace OrderBridge.Integrations.Application.ValueObjects;

public sealed record CustomerSnapshotDto(string CompanyName, string TaxId, string ContactEmail);

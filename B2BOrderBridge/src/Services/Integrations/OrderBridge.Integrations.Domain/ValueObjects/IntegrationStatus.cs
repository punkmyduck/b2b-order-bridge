namespace OrderBridge.Integrations.Domain.ValueObjects;

public enum IntegrationStatus
{
    WaitingForPrerequisite,
    Pending,
    Processing,
    RetryScheduled,
    Succeeded,
    Failed,
    Cancelled,
}


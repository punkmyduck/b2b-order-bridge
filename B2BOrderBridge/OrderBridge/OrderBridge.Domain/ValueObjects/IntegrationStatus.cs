namespace OrderBridge.Domain.ValueObjects;

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


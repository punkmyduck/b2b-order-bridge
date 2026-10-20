namespace Shared.Messaging.Abstractions.Events;

/// <summary>Stable contract identity and default transport destination. Contains no delivery state.</summary>
public sealed record EventDefinition<TPayload>(
    string EventType,
    int SchemaVersion,
    string Topic)
    where TPayload : class;

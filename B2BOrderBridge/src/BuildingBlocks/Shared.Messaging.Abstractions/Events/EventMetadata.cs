using System.Text.Json.Serialization;

namespace Shared.Messaging.Abstractions.Events;

public sealed record EventMetadata(
    [property: JsonRequired] Guid EventId,
    [property: JsonRequired] string EventType,
    [property: JsonRequired] int SchemaVersion,
    [property: JsonRequired] string Source,
    [property: JsonRequired] DateTimeOffset OccurredAt,
    [property: JsonRequired] string IdempotencyKey,
    [property: JsonRequired] string OrderingKey,
    string? CorrelationId = null,
    Guid? CausationId = null,
    string? TraceParent = null);

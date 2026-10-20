namespace Shared.Messaging.Abstractions.Events;

/// <summary>Explicit permanent input failure. Other exceptions are retried, not discarded.</summary>
public sealed class InvalidEventException(
    string message,
    Exception? inner = null) : Exception(message, inner);

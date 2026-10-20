using System.Text.Json;

namespace Shared.Messaging.Persistence;

public static class MessagingJson
{
    public static JsonSerializerOptions Options { get; } = new(JsonSerializerDefaults.Web);
}

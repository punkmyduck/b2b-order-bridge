using System.Text.Json;
namespace OrderBridge.Integrations.Infrastructure.Messaging;
public static class EventJson
{
    public static JsonSerializerOptions Options { get; } = new(JsonSerializerDefaults.Web);
}

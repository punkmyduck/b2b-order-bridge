using System.Globalization;

namespace OrderBridge.Web.Services;

public static class Display
{
    private static readonly CultureInfo Russian = CultureInfo.GetCultureInfo("ru-RU");
    public static string Money(decimal value, string currency) => $"{value.ToString("N2", Russian)} {currency}";
    public static string Number(decimal value) => value.ToString("0.###", Russian);
    public static string Date(DateTimeOffset? value) => value?.UtcDateTime.ToString("dd.MM.yyyy · HH:mm:ss", Russian) ?? "—";
    public static string Target(string target) => target switch { "OneC" => "1С", "Bitrix24" => "Битрикс24", "Diadoc" => "Диадок", _ => target };
    public static string Operation(string value) => value switch
    {
        "CreateDeal" => "Создание сделки", "CreateSalesOrder" => "Создание заказа",
        "CreateDocument" => "Создание документа", "SendOrderReceivedNotification" => "Уведомление о заказе", _ => value
    };
    public static string Status(string value) => value switch
    {
        "Received" => "Получен", "Pending" => "Ожидает", "Paid" => "Оплачен", "Processing" => "Выполняется",
        "Succeeded" => "Успешно", "Failed" => "Ошибка", "Cancelled" => "Отменён",
        "RetryScheduled" => "Повтор запланирован", "WaitingForPrerequisite" => "Ожидает условия", _ => value
    };
    public static string Tone(string value) => value switch
    {
        "Succeeded" or "Paid" => "success", "Failed" => "danger", "Processing" => "info",
        "Pending" or "RetryScheduled" or "WaitingForPrerequisite" => "warning", _ => "neutral"
    };
}

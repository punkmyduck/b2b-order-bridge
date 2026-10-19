using System.ComponentModel.DataAnnotations;

namespace OrderBridge.Web.Models;

public sealed class CreateOrderForm : IValidatableObject
{
    [Required(ErrorMessage = "Укажите источник заказа."), StringLength(64)]
    public string SourceSystem { get; set; } = "Backoffice";
    [Required(ErrorMessage = "Укажите внешний номер заказа."), StringLength(512)]
    public string ExternalOrderId { get; set; } = "";
    [Required(ErrorMessage = "Укажите компанию."), StringLength(128)]
    public string CompanyName { get; set; } = "";
    [Required(ErrorMessage = "Укажите ИНН."), StringLength(512)]
    public string TaxId { get; set; } = "";
    [Required(ErrorMessage = "Укажите email."), EmailAddress(ErrorMessage = "Укажите корректный email."), StringLength(256)]
    public string ContactEmail { get; set; } = "";
    public string Currency { get; set; } = "RUB";
    public List<LineForm> Lines { get; } = [new()];
    public static IReadOnlyList<string> Currencies { get; } = ["RUB", "USD", "EUR", "CNY", "BYN"];

    public decimal? Total
    {
        get
        {
            try { return Lines.Sum(x => decimal.Round(x.Quantity * x.UnitPrice, 2, MidpointRounding.AwayFromZero)); }
            catch (OverflowException) { return null; }
        }
    }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!Currencies.Contains(Currency))
            yield return new("Выберите поддерживаемую валюту.", [nameof(Currency)]);
        if (Lines.Count == 0)
            yield return new("Добавьте хотя бы одну позицию.", [nameof(Lines)]);
        for (var i = 0; i < Lines.Count; i++)
        {
            var line = Lines[i];
            var prefix = $"Позиция {i + 1}: ";
            if (string.IsNullOrWhiteSpace(line.Sku) || line.Sku.Trim().Length > 128)
                yield return new(prefix + "артикул обязателен, максимум 128 символов.", [nameof(Lines)]);
            if (string.IsNullOrWhiteSpace(line.Name) || line.Name.Trim().Length > 128)
                yield return new(prefix + "название обязательно, максимум 128 символов.", [nameof(Lines)]);
            if (line.Quantity <= 0)
                yield return new(prefix + "количество должно быть больше нуля.", [nameof(Lines)]);
            if (line.UnitPrice < 0 || decimal.Round(line.UnitPrice, 2) != line.UnitPrice)
                yield return new(prefix + "цена должна быть неотрицательной, не более двух знаков после запятой.", [nameof(Lines)]);
        }
        if (Total is null)
            yield return new("Сумма заказа превышает допустимое значение.", [nameof(Lines)]);
    }

    public CreateOrderInput ToInput() => new(SourceSystem.Trim(), ExternalOrderId.Trim(),
        new Customer(CompanyName.Trim(), TaxId.Trim(), ContactEmail.Trim()), Currency,
        Lines.Select(x => new OrderLineInput(x.Sku.Trim(), x.Name.Trim(), x.Quantity, x.UnitPrice)).ToArray());
}

public sealed class LineForm
{
    public string Sku { get; set; } = "";
    public string Name { get; set; } = "";
    public decimal Quantity { get; set; } = 1;
    public decimal UnitPrice { get; set; }
}

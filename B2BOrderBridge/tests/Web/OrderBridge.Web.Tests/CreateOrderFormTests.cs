using System.ComponentModel.DataAnnotations;
using OrderBridge.Web.Models;
using Xunit;

namespace OrderBridge.Web.Tests;

public sealed class CreateOrderFormTests
{
    [Fact]
    public void Invalid_lines_prevent_submission()
    {
        var model = ValidForm();
        model.Lines[0].Quantity = 0;
        model.Lines[0].UnitPrice = 1.234m;
        var errors = new List<ValidationResult>();
        Assert.False(Validator.TryValidateObject(model, new ValidationContext(model), errors, true));
        Assert.Contains(errors, x => x.ErrorMessage!.Contains("количество"));
        Assert.Contains(errors, x => x.ErrorMessage!.Contains("цена"));
    }

    [Fact]
    public void Total_matches_backend_rounding_per_line()
    {
        var model = ValidForm();
        model.Lines[0].Quantity = .5m;
        model.Lines[0].UnitPrice = .01m;
        model.Lines.Add(new() { Sku = "B", Name = "Second", Quantity = .5m, UnitPrice = .01m });
        Assert.Equal(.02m, model.Total);
    }

    [Fact]
    public void Overflow_is_validation_error_instead_of_crashing_the_page()
    {
        var model = ValidForm();
        model.Lines[0].Quantity = decimal.MaxValue;
        model.Lines[0].UnitPrice = 2;
        Assert.Null(model.Total);
        Assert.Contains(model.Validate(new ValidationContext(model)), x => x.ErrorMessage!.Contains("Сумма"));
    }

    [Fact]
    public void Request_trims_strings_and_retains_currency_and_lines()
    {
        var model = ValidForm();
        model.ExternalOrderId = " ORDER-1 ";
        model.Currency = "EUR";
        var input = model.ToInput();
        Assert.Equal("ORDER-1", input.ExternalOrderId);
        Assert.Equal("EUR", input.Currency);
        Assert.Single(input.Lines);
    }

    private static CreateOrderForm ValidForm()
    {
        var model = new CreateOrderForm { ExternalOrderId = "ORDER-1", CompanyName = "Company", TaxId = "123", ContactEmail = "a@example.com" };
        model.Lines[0].Sku = "A";
        model.Lines[0].Name = "Item";
        return model;
    }
}

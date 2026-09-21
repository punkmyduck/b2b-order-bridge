using FluentValidation;
using OrderBridge.Domain.Models;
using OrderBridge.Domain.ValueObjects;

namespace OrderBridge.Application.Features.Orders.Queries;

public sealed class GetPagedOrdersQueryValidator : AbstractValidator<GetPagedOrdersQuery>
{
    public const int SearchTermMaxLength = 200;

    public GetPagedOrdersQueryValidator()
    {
        RuleFor(query => query.Pagination)
            .NotNull()
            .WithMessage("Pagination is required.");
        RuleFor(query => query.SortBy).IsInEnum();
        RuleFor(query => query.SortDirection).IsInEnum();

        When(query => query.Filter is not null, () =>
        {
            RuleFor(query => query.Filter!.SourceSystem)
                .MaximumLength(Order.SourceSystemMaxLength);
            RuleFor(query => query.Filter!.ExternalOrderId)
                .MaximumLength(Order.ExternalOrderIdMaxLength);
            RuleFor(query => query.Filter!.CustomerName)
                .MaximumLength(CustomerSnapshot.CompanyNameMaxLength);
            RuleFor(query => query.Filter!.Currency)
                .Must(currency => string.IsNullOrWhiteSpace(currency) ||
                                  CurrencyCodes.Supported.Contains(currency.Trim().ToUpperInvariant()))
                .WithMessage("Currency must be a supported currency code.");
            RuleFor(query => query.Filter!.SearchTerm)
                .MaximumLength(SearchTermMaxLength);
            RuleFor(query => query.Filter!)
                .Must(filter => filter.TotalAmountFrom is null ||
                                filter.TotalAmountTo is null ||
                                filter.TotalAmountFrom <= filter.TotalAmountTo)
                .WithMessage("TotalAmountFrom must not exceed TotalAmountTo.");
            RuleFor(query => query.Filter!)
                .Must(filter => filter.CreatedAtFrom is null ||
                                filter.CreatedAtTo is null ||
                                filter.CreatedAtFrom <= filter.CreatedAtTo)
                .WithMessage("CreatedAtFrom must not exceed CreatedAtTo.");
        });
    }
}

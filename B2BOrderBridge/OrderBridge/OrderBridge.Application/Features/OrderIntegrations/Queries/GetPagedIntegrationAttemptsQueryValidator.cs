using FluentValidation;
using OrderBridge.Application.Features.OrderIntegrations.Models;

namespace OrderBridge.Application.Features.OrderIntegrations.Queries;

public sealed class GetPagedIntegrationAttemptsQueryValidator
    : AbstractValidator<GetPagedIntegrationAttemptsQuery>
{
    public const int SearchTermMaxLength = 200;

    public GetPagedIntegrationAttemptsQueryValidator()
    {
        RuleFor(query => query.Pagination)
            .NotNull()
            .WithMessage("Pagination is required.");
        RuleFor(query => query.SortBy).IsInEnum();
        RuleFor(query => query.SortDirection).IsInEnum();

        When(query => query.Filter is not null, () =>
        {
            RuleFor(query => query.Filter!.Id)
                .NotEqual(Guid.Empty)
                .When(query => query.Filter!.Id.HasValue);
            RuleFor(query => query.Filter!.OrderIntegrationId)
                .NotEqual(Guid.Empty)
                .When(query => query.Filter!.OrderIntegrationId.HasValue);
            RuleFor(query => query.Filter!.AttemptNumberFrom)
                .GreaterThanOrEqualTo(1)
                .When(query => query.Filter!.AttemptNumberFrom.HasValue);
            RuleFor(query => query.Filter!.AttemptNumberTo)
                .GreaterThanOrEqualTo(1)
                .When(query => query.Filter!.AttemptNumberTo.HasValue);
            RuleFor(query => query.Filter!.Outcome)
                .IsInEnum()
                .When(query => query.Filter!.Outcome.HasValue);
            RuleFor(query => query.Filter!.ResponseStatusCode)
                .InclusiveBetween(100, 599)
                .When(query => query.Filter!.ResponseStatusCode.HasValue);
            RuleFor(query => query.Filter!.ErrorCodeSearchTerm)
                .MaximumLength(IntegrationAttempt.ErrorCodeMaxLength);
            RuleFor(query => query.Filter!.ErrorMessageSearchTerm)
                .MaximumLength(IntegrationAttempt.ErrorMessageMaxLength);
            RuleFor(query => query.Filter!.TraceIdSearchTerm)
                .MaximumLength(IntegrationAttempt.TraceIdMaxLength);
            RuleFor(query => query.Filter!.SearchTerm)
                .MaximumLength(SearchTermMaxLength);
            RuleFor(query => query.Filter!)
                .Must(filter => filter.AttemptNumberFrom is null ||
                                filter.AttemptNumberTo is null ||
                                filter.AttemptNumberFrom <= filter.AttemptNumberTo)
                .WithMessage("AttemptNumberFrom must not exceed AttemptNumberTo.");
            RuleFor(query => query.Filter!)
                .Must(filter => filter.StartedAtFrom is null ||
                                filter.StartedAtTo is null ||
                                filter.StartedAtFrom <= filter.StartedAtTo)
                .WithMessage("StartedAtFrom must not exceed StartedAtTo.");
            RuleFor(query => query.Filter!)
                .Must(filter => filter.CompletedAtFrom is null ||
                                filter.CompletedAtTo is null ||
                                filter.CompletedAtFrom <= filter.CompletedAtTo)
                .WithMessage("CompletedAtFrom must not exceed CompletedAtTo.");
        });
    }
}

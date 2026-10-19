using Shared.Application.Pagination;
using FluentValidation;

namespace OrderBridge.Integrations.Application.Features.OrderIntegrations.Queries;

public sealed class GetPagedOrderIntegrationsQueryValidator
    : AbstractValidator<GetPagedOrderIntegrationsQuery>
{
    public const int SearchTermMaxLength = 200;

    public GetPagedOrderIntegrationsQueryValidator()
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
            RuleFor(query => query.Filter!.OrderId)
                .NotEqual(Guid.Empty)
                .When(query => query.Filter!.OrderId.HasValue);
            RuleFor(query => query.Filter!.IntegrationTarget)
                .IsInEnum()
                .When(query => query.Filter!.IntegrationTarget.HasValue);
            RuleFor(query => query.Filter!.IntegrationOperation)
                .IsInEnum()
                .When(query => query.Filter!.IntegrationOperation.HasValue);
            RuleFor(query => query.Filter!.IntegrationStatus)
                .IsInEnum()
                .When(query => query.Filter!.IntegrationStatus.HasValue);
            RuleFor(query => query.Filter!.AttemptCountFrom)
                .GreaterThanOrEqualTo(0)
                .When(query => query.Filter!.AttemptCountFrom.HasValue);
            RuleFor(query => query.Filter!.AttemptCountTo)
                .GreaterThanOrEqualTo(0)
                .When(query => query.Filter!.AttemptCountTo.HasValue);
            RuleFor(query => query.Filter!)
                .Must(filter => filter.AttemptCountFrom is null ||
                                filter.AttemptCountTo is null ||
                                filter.AttemptCountFrom <= filter.AttemptCountTo)
                .WithMessage("AttemptCountFrom must not exceed AttemptCountTo.");
            RuleFor(query => query.Filter!)
                .Must(filter => filter.CreatedAtFrom is null ||
                                filter.CreatedAtTo is null ||
                                filter.CreatedAtFrom <= filter.CreatedAtTo)
                .WithMessage("CreatedAtFrom must not exceed CreatedAtTo.");
            RuleFor(query => query.Filter!.SearchTerm)
                .MaximumLength(SearchTermMaxLength);
        });
    }
}

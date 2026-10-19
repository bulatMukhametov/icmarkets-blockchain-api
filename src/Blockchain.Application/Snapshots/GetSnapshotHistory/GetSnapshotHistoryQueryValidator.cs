using FluentValidation;

namespace Blockchain.Application.Snapshots.GetSnapshotHistory;

public sealed class GetSnapshotHistoryQueryValidator : AbstractValidator<GetSnapshotHistoryQuery>
{
    public GetSnapshotHistoryQueryValidator()
    {
        RuleFor(x => x.CurrencyCode).NotEmpty();
        RuleFor(x => x.NetworkCode).NotEmpty();
        RuleFor(x => x.Page).GreaterThan(0);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
        RuleFor(x => x.Page)
            .Must((query, page) => ((long)page - 1) * query.PageSize <= int.MaxValue)
            .When(x => x.Page > 0 && x.PageSize is >= 1 and <= 100)
            .WithMessage("Requested page is too large.");
    }
}

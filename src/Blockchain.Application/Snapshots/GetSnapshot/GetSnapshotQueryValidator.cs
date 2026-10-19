using FluentValidation;

namespace Blockchain.Application.Snapshots.GetSnapshot;

public sealed class GetSnapshotQueryValidator : AbstractValidator<GetSnapshotQuery>
{
    public GetSnapshotQueryValidator()
    {
        RuleFor(x => x.CurrencyCode).NotEmpty();
        RuleFor(x => x.NetworkCode).NotEmpty();
        RuleFor(x => x.Id).GreaterThan(0);
    }
}

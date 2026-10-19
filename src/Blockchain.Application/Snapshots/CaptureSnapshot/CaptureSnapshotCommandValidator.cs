using FluentValidation;

namespace Blockchain.Application.Snapshots.CaptureSnapshot;

public sealed class CaptureSnapshotCommandValidator : AbstractValidator<CaptureSnapshotCommand>
{
    public CaptureSnapshotCommandValidator()
    {
        RuleFor(x => x.CurrencyCode).NotEmpty();
        RuleFor(x => x.NetworkCode).NotEmpty();
    }
}

using Blockchain.Application.Snapshots.CaptureSnapshot;
using Blockchain.Application.Snapshots.GetSnapshot;
using Blockchain.Application.Snapshots.GetSnapshotHistory;

namespace Blockchain.UnitTests;

public sealed class RequestValidatorTests
{
    [Fact]
    public async Task Capture_and_read_reject_missing_codes_and_invalid_id()
    {
        var capture = await new CaptureSnapshotCommandValidator().ValidateAsync(new CaptureSnapshotCommand(" ", ""));
        Assert.Equal(new[] { "CurrencyCode", "NetworkCode" }, capture.Errors.Select(error => error.PropertyName));

        var read = await new GetSnapshotQueryValidator().ValidateAsync(new GetSnapshotQuery("", " ", 0));
        Assert.Equal(new[] { "CurrencyCode", "NetworkCode", "Id" }, read.Errors.Select(error => error.PropertyName));
    }

    [Fact]
    public async Task History_reports_all_invalid_fields_together()
    {
        var result = await new GetSnapshotHistoryQueryValidator().ValidateAsync(new GetSnapshotHistoryQuery("", " ", 0, 101));
        Assert.Equal(new[] { "CurrencyCode", "NetworkCode", "Page", "PageSize" },
            result.Errors.Select(error => error.PropertyName));
    }

    [Theory]
    [InlineData(1, 100, true)]
    [InlineData(int.MaxValue, 1, true)]
    [InlineData(int.MaxValue, 100, false)]
    public async Task History_checks_offset_without_integer_overflow(int page, int pageSize, bool valid)
    {
        var result = await new GetSnapshotHistoryQueryValidator().ValidateAsync(new GetSnapshotHistoryQuery("btc", "main", page, pageSize));
        Assert.Equal(valid, result.IsValid);
        if (!valid) Assert.Equal("Page", Assert.Single(result.Errors).PropertyName);
    }
}

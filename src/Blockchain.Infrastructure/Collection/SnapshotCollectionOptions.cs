using System.ComponentModel.DataAnnotations;

namespace Blockchain.Infrastructure.Collection;

public sealed class SnapshotCollectionOptions
{
    public const string Section = "SnapshotCollection";
    public bool Enabled { get; set; }
    [Range(1, 86400)] public int IntervalSeconds { get; set; } = 600;
    [Range(0, 3600)] public int InitialDelaySeconds { get; set; } = 10;
    [Range(1, 600)] public int CycleTimeoutSeconds { get; set; } = 120;
}

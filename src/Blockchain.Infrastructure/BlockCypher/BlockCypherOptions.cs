using System.ComponentModel.DataAnnotations;

namespace Blockchain.Infrastructure.BlockCypher;

public sealed class BlockCypherOptions
{
    public const string Section = "BlockCypher";
    [Required, Url] public string BaseUrl { get; set; } = "https://api.blockcypher.com/v1/";
    [Range(1, 3)] public int RequestsPerSecond { get; set; } = 2;
    [Range(1, 100)] public int RequestsPerHour { get; set; } = 90;
    [Range(1, 30)] public int AttemptTimeoutSeconds { get; set; } = 10;
    [Range(1, 120)] public int TotalTimeoutSeconds { get; set; } = 45;
    [Range(0, 2)] public int MaxRetries { get; set; } = 1;
    [Range(1024, 1048576)] public int MaxResponseBytes { get; set; } = 1048576;
}

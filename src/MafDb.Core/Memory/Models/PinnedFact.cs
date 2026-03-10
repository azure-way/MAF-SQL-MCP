namespace MafDb.Core.Memory.Models;

public sealed class PinnedFact
{
    public string FactId { get; set; } = Guid.NewGuid().ToString("N");
    public string Fact { get; set; } = string.Empty;
    public string SourceTurnId { get; set; } = string.Empty;
    public double Confidence { get; set; }
    public DateTimeOffset LastUsedUtc { get; set; } = DateTimeOffset.UtcNow;
}

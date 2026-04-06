namespace ReleaseKit.Domain.ValueObjects;

/// <summary>
/// 分析階段金鑰（如 "1-3", "2-1", "1-3-a"）
/// </summary>
public sealed record AnalysisPassKey
{
    /// <summary>階段編號</summary>
    public required int Pass { get; init; }

    /// <summary>序號</summary>
    public required int Sequence { get; init; }

    /// <summary>子序號（大型 diff 拆分時使用）</summary>
    public string? SubSequence { get; init; }

    /// <summary>產生 Redis field 名稱</summary>
    public string ToRedisField()
    {
        var key = $"Intermediate:{Pass}-{Sequence}";
        return SubSequence is not null ? $"{key}-{SubSequence}" : key;
    }
}

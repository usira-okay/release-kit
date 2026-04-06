namespace ReleaseKit.Domain.Tests.ValueObjects;

using ReleaseKit.Domain.ValueObjects;

/// <summary>
/// AnalysisPassKey 值物件單元測試
/// </summary>
public class AnalysisPassKeyTests
{
    [Fact]
    public void ToRedisField_WithPassAndSequence_ShouldReturnCorrectFormat()
    {
        var key = new AnalysisPassKey { Pass = 1, Sequence = 3 };

        var result = key.ToRedisField();

        Assert.Equal("Intermediate:1-3", result);
    }

    [Fact]
    public void ToRedisField_WithSubSequence_ShouldIncludeSubSequence()
    {
        var key = new AnalysisPassKey { Pass = 1, Sequence = 3, SubSequence = "a" };

        var result = key.ToRedisField();

        Assert.Equal("Intermediate:1-3-a", result);
    }

    [Fact]
    public void ToRedisField_WithNullSubSequence_ShouldOmitSubSequence()
    {
        var key = new AnalysisPassKey { Pass = 2, Sequence = 1, SubSequence = null };

        var result = key.ToRedisField();

        Assert.Equal("Intermediate:2-1", result);
    }

    [Fact]
    public void Equality_SameValues_ShouldBeEqual()
    {
        var key1 = new AnalysisPassKey { Pass = 1, Sequence = 2, SubSequence = "b" };
        var key2 = new AnalysisPassKey { Pass = 1, Sequence = 2, SubSequence = "b" };

        Assert.Equal(key1, key2);
    }

    [Fact]
    public void Equality_DifferentValues_ShouldNotBeEqual()
    {
        var key1 = new AnalysisPassKey { Pass = 1, Sequence = 2 };
        var key2 = new AnalysisPassKey { Pass = 1, Sequence = 3 };

        Assert.NotEqual(key1, key2);
    }
}

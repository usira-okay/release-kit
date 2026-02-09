using System.Text.Json;
using ReleaseKit.Common.Extensions;
using Xunit;

namespace ReleaseKit.Common.Tests.Extensions;

/// <summary>
/// JsonExtensions 單元測試
/// </summary>
public class JsonExtensionsTests
{
    [Fact]
    public void ToJson_ShouldNotIndent()
    {
        // Arrange
        var obj = new { Name = "Test", Value = 123 };

        // Act
        var json = obj.ToJson();

        // Assert
        Assert.DoesNotContain("\n", json);
        Assert.DoesNotContain("\r", json);
        Assert.DoesNotContain("  ", json); // No double spaces (indentation)
    }

    [Fact]
    public void ToJson_ShouldEncodeChineseCharacters()
    {
        // Arrange
        var obj = new { Name = "測試", Description = "中文字元" };

        // Act
        var json = obj.ToJson();

        // Assert
        Assert.Contains("測試", json);
        Assert.Contains("中文字元", json);
    }

    [Fact]
    public void ToJson_ShouldConvertEnumToString()
    {
        // Arrange
        var obj = new { Status = TestEnum.Active };

        // Act
        var json = obj.ToJson();

        // Assert
        Assert.Contains("\"active\"", json);
    }

    [Fact]
    public void ToTypedObject_ShouldBeCaseInsensitive()
    {
        // Arrange
        var json = "{\"name\":\"Test\",\"VALUE\":456}";

        // Act
        var result = json.ToTypedObject<TestClass>();

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Test", result.Name);
        Assert.Equal(456, result.Value);
    }

    [Fact]
    public void ToTypedObject_ShouldConvertStringToEnum()
    {
        // Arrange
        var json = "{\"status\":\"active\"}";

        // Act
        var result = json.ToTypedObject<TestClassWithEnum>();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(TestEnum.Active, result.Status);
    }

    private enum TestEnum
    {
        Active,
        Inactive
    }

    private class TestClass
    {
        public string? Name { get; set; }
        public int Value { get; set; }
    }

    private class TestClassWithEnum
    {
        public TestEnum Status { get; set; }
    }
}

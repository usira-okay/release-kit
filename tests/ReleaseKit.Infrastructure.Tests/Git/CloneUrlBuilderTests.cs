using ReleaseKit.Common.Configuration;
using ReleaseKit.Infrastructure.Git;

namespace ReleaseKit.Infrastructure.Tests.Git;

/// <summary>
/// CloneUrlBuilder 單元測試
/// </summary>
public class CloneUrlBuilderTests
{
    [Fact]
    public void BuildGitLabCloneUrl_應移除ApiV4路徑並嵌入Token()
    {
        var gitLabOptions = new GitLabOptions
        {
            ApiUrl = "https://gitlab.example.com/api/v4",
            AccessToken = "test-token-123"
        };

        var result = CloneUrlBuilder.BuildGitLabCloneUrl(gitLabOptions, "mygroup/backend-api");

        Assert.Equal("https://oauth2:test-token-123@gitlab.example.com/mygroup/backend-api.git", result);
    }

    [Fact]
    public void BuildGitLabCloneUrl_無ApiV4路徑時應直接使用()
    {
        var gitLabOptions = new GitLabOptions
        {
            ApiUrl = "https://gitlab.example.com",
            AccessToken = "test-token"
        };

        var result = CloneUrlBuilder.BuildGitLabCloneUrl(gitLabOptions, "mygroup/backend-api");

        Assert.Equal("https://oauth2:test-token@gitlab.example.com/mygroup/backend-api.git", result);
    }

    [Fact]
    public void BuildBitbucketCloneUrl_應嵌入Username與Token()
    {
        var bitbucketOptions = new BitbucketOptions
        {
            Username = "bb user",
            AccessToken = "bb-token:456@/#"
        };

        var result = CloneUrlBuilder.BuildBitbucketCloneUrl(bitbucketOptions, "workspace/repo");

        Assert.Equal("https://bb%20user:bb-token%3A456%40%2F%23@bitbucket.org/workspace/repo.git", result);
    }

    [Fact]
    public void BuildBitbucketCloneUrl_未設定Username_應拋出明確錯誤()
    {
        var bitbucketOptions = new BitbucketOptions
        {
            AccessToken = "bb-token"
        };

        var exception = Assert.Throws<InvalidOperationException>(
            () => CloneUrlBuilder.BuildBitbucketCloneUrl(bitbucketOptions, "workspace/repo"));

        Assert.Equal("缺少必要的組態鍵: Bitbucket:Username", exception.Message);
    }
}

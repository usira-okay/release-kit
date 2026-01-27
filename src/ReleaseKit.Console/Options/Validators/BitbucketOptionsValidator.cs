using Microsoft.Extensions.Options;

namespace ReleaseKit.Console.Options;

/// <summary>
/// Bitbucket 設定驗證器
/// </summary>
public class BitbucketOptionsValidator : IValidateOptions<BitbucketOptions>
{
    public ValidateOptionsResult Validate(string? name, BitbucketOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.ApiUrl))
        {
            return ValidateOptionsResult.Fail("Bitbucket:ApiUrl 組態設定不得為空");
        }

        foreach (var project in options.Projects)
        {
            if (string.IsNullOrWhiteSpace(project.ProjectPath))
            {
                return ValidateOptionsResult.Fail("Bitbucket:Projects:ProjectPath 組態設定不得為空");
            }

            if (string.IsNullOrWhiteSpace(project.TargetBranch))
            {
                return ValidateOptionsResult.Fail("Bitbucket:Projects:TargetBranch 組態設定不得為空");
            }
        }

        return ValidateOptionsResult.Success;
    }
}

using Microsoft.Extensions.Options;

namespace ReleaseKit.Console.Options;

/// <summary>
/// GitLab 設定驗證器
/// </summary>
public class GitLabOptionsValidator : IValidateOptions<GitLabOptions>
{
    public ValidateOptionsResult Validate(string? name, GitLabOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.ApiUrl))
        {
            return ValidateOptionsResult.Fail("GitLab:ApiUrl 組態設定不得為空");
        }

        foreach (var project in options.Projects)
        {
            if (string.IsNullOrWhiteSpace(project.ProjectPath))
            {
                return ValidateOptionsResult.Fail("GitLab:Projects:ProjectPath 組態設定不得為空");
            }

            if (string.IsNullOrWhiteSpace(project.TargetBranch))
            {
                return ValidateOptionsResult.Fail("GitLab:Projects:TargetBranch 組態設定不得為空");
            }
        }

        return ValidateOptionsResult.Success;
    }
}

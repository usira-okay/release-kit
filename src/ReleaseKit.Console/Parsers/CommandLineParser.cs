using ReleaseKit.Application.Tasks;

namespace ReleaseKit.Console.Parsers;

/// <summary>
/// 命令列參數解析器
/// </summary>
public class CommandLineParser
{
    private readonly Dictionary<string, TaskType> _taskMappings = new(StringComparer.OrdinalIgnoreCase)
    {
        { "fetch-gitlab-release-branch", TaskType.FetchGitLabReleaseBranch },
        { "fetch-bitbucket-release-branch", TaskType.FetchBitbucketReleaseBranch },
        { "fetch-gitlab-pr", TaskType.FetchGitLabPullRequests },
        { "fetch-bitbucket-pr", TaskType.FetchBitbucketPullRequests },
        { "filter-gitlab-pr-by-user", TaskType.FilterGitLabPullRequestsByUser },
        { "filter-bitbucket-pr-by-user", TaskType.FilterBitbucketPullRequestsByUser },
        { "fetch-azure-workitems", TaskType.FetchAzureDevOpsWorkItems },
        { "get-user-story", TaskType.GetUserStory },
        { "update-googlesheet", TaskType.UpdateGoogleSheets },
    };

    /// <summary>
    /// 解析命令列參數並取得對應的任務類型
    /// </summary>
    /// <param name="args">命令列參數陣列</param>
    /// <returns>解析結果，包含任務類型或錯誤訊息</returns>
    public ParseResult Parse(string[] args)
    {
        if (args == null || args.Length == 0)
        {
            return ParseResult.Failure("請指定要執行的任務。使用方式: ReleaseKit.Console <task-name>");
        }

        if (args.Length > 1)
        {
            return ParseResult.Failure("每次只允許執行單一任務。使用方式: ReleaseKit.Console <task-name>");
        }

        var taskName = args[0];
        
        if (_taskMappings.TryGetValue(taskName, out var taskType))
        {
            return ParseResult.Success(taskType);
        }

        var validTasks = string.Join(", ", _taskMappings.Keys);
        return ParseResult.Failure($"不支援的任務: '{taskName}'。有效的任務: {validTasks}");
    }
}

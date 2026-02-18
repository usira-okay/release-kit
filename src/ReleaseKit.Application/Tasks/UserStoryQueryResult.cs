namespace ReleaseKit.Application.Tasks;

/// <summary>
/// User Story 查詢結果（內部使用）
/// </summary>
internal sealed record UserStoryQueryResult
{
    public bool IsSuccess { get; init; }
    public int UserStoryWorkItemId { get; init; }
    public string? Title { get; init; }
    public string? Type { get; init; }
    public string? State { get; init; }
    public string? Url { get; init; }
    public string? OriginalTeamName { get; init; }
    public string? ErrorMessage { get; init; }

    public static UserStoryQueryResult Success(Domain.Entities.WorkItem workItem)
    {
        return new UserStoryQueryResult
        {
            IsSuccess = true,
            UserStoryWorkItemId = workItem.WorkItemId,
            Title = workItem.Title,
            Type = workItem.Type,
            State = workItem.State,
            Url = workItem.Url,
            OriginalTeamName = workItem.OriginalTeamName,
            ErrorMessage = null
        };
    }

    public static UserStoryQueryResult Failure(string errorMessage)
    {
        return new UserStoryQueryResult
        {
            IsSuccess = false,
            UserStoryWorkItemId = 0,
            Title = null,
            Type = null,
            State = null,
            Url = null,
            OriginalTeamName = null,
            ErrorMessage = errorMessage
        };
    }
}

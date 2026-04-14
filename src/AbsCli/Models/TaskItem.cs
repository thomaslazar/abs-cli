using System.Text.Json;
using System.Text.Json.Serialization;

namespace AbsCli.Models;

public class TaskItem
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";
    [JsonPropertyName("action")]
    public string? Action { get; set; }
    [JsonPropertyName("title")]
    public string? Title { get; set; }
    [JsonPropertyName("description")]
    public string? Description { get; set; }
    [JsonPropertyName("error")]
    public string? Error { get; set; }
    [JsonPropertyName("isFailed")]
    public bool IsFailed { get; set; }
    [JsonPropertyName("isFinished")]
    public bool IsFinished { get; set; }
    [JsonPropertyName("showSuccess")]
    public bool ShowSuccess { get; set; }
    [JsonPropertyName("startedAt")]
    public long? StartedAt { get; set; }
    [JsonPropertyName("finishedAt")]
    public long? FinishedAt { get; set; }
    [JsonPropertyName("data")]
    public JsonElement? Data { get; set; }
}

public class TaskListResponse
{
    [JsonPropertyName("tasks")]
    public List<TaskItem> Tasks { get; set; } = new();
}

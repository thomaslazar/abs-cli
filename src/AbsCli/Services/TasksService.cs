using AbsCli.Api;
using AbsCli.Models;

namespace AbsCli.Services;

public class TasksService
{
    private readonly AbsApiClient _client;

    public TasksService(AbsApiClient client)
    {
        _client = client;
    }

    public async Task<TaskListResponse> ListAsync()
    {
        return await _client.GetAsync(ApiEndpoints.Tasks, AppJsonContext.Default.TaskListResponse);
    }
}

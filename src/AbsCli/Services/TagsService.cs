using System.Text.Json;
using AbsCli.Api;
using AbsCli.Models;

namespace AbsCli.Services;

public class TagsService
{
    private readonly AbsApiClient _client;

    public TagsService(AbsApiClient client)
    {
        _client = client;
    }

    public async Task<TagListResponse> ListAsync()
    {
        return await _client.GetAsync(ApiEndpoints.Tags,
            AppJsonContext.Default.TagListResponse, "admin permission");
    }

    public async Task<TagRenameResponse> RenameAsync(string tag, string newTag)
    {
        var json = JsonSerializer.Serialize(
            new TagRenameRequest { Tag = tag, NewTag = newTag },
            AppJsonContext.Default.TagRenameRequest);
        return await _client.PostAsync(ApiEndpoints.TagRename, json,
            AppJsonContext.Default.TagRenameResponse, "admin permission");
    }

    public async Task<TagDeleteResponse> DeleteAsync(string tag)
    {
        return await _client.DeleteAsync(ApiEndpoints.TagByName(tag),
            AppJsonContext.Default.TagDeleteResponse, "admin permission");
    }
}

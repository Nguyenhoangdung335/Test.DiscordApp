using Microsoft.Extensions.Options;
using Test.DiscordApp.Domain.Config;
using Test.DiscordApp.Domain.Proxy.Github.Response;
using Test.DiscordApp.Infrastructure.ExternalProxy.Base;
using Test.DiscordApp.Domain.Utility;

namespace Test.DiscordApp.Infrastructure.ExternalProxy.Github;

public class GithubProxy(
    IHttpClientFactory httpClientFactory,
    IOptions<GithubConfig> githubConfig,
    IBaseProxy baseProxy
) : IGithubProxy
{
    private HttpClient HttpClient => httpClientFactory.CreateClient(nameof(GithubProxy));

    public async Task<(GithubCommit? Data, string Error)> GetLatestCommit(string branch = "", string trackId = "",
        CancellationToken cancellationToken = default)
    {
        var owner = githubConfig.Value.User;
        var repo = githubConfig.Value.RepositoryName;
        if (branch.IsNullOrEmpty())
            branch = githubConfig.Value.DefaultBranch;
        var url = $"repos/{owner}/{repo}/commits/{branch}";
        var response = await baseProxy.GetAsync<GithubCommit>(HttpClient, url, trackId: trackId,
                cancellationToken: cancellationToken);
        
        return response is null ? (null, "Failed to get latest commit") : (response, string.Empty);
    }
}
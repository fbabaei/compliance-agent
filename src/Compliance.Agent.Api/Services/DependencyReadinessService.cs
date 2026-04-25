using Compliance.Agent.Api.Models;
using Compliance.Agent.Api.Options;
using Microsoft.Extensions.Options;

namespace Compliance.Agent.Api.Services;

public sealed class DependencyReadinessService : IDependencyReadinessService
{
    private readonly AzureOpenAiOptions _options;

    public DependencyReadinessService(IOptions<AzureOpenAiOptions> options)
    {
        _options = options.Value;
    }

    public Task<DependencyReadinessResult> CheckAsync(CancellationToken cancellationToken)
    {
        var checks = new Dictionary<string, string>
        {
            ["AzureOpenAI.Endpoint"] = string.IsNullOrWhiteSpace(_options.Endpoint) ? "missing" : "configured",
            ["AzureOpenAI.ChatDeployment"] = string.IsNullOrWhiteSpace(_options.ChatDeployment) ? "missing" : "configured",
            ["AzureOpenAI.EmbeddingDeployment"] = string.IsNullOrWhiteSpace(_options.EmbeddingDeployment) ? "missing" : "configured",
            ["Storage"] = "stubbed",
            ["Sql"] = "stubbed"
        };

        var ready = checks.Values.Count(v => v == "missing") == 0;
        return Task.FromResult(new DependencyReadinessResult(ready, checks));
    }
}

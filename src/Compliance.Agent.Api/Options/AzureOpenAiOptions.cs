namespace Compliance.Agent.Api.Options;

public sealed class AzureOpenAiOptions
{
    public string Endpoint { get; set; } = string.Empty;
    public string ChatDeployment { get; set; } = "gpt-5.2";
    public string EmbeddingDeployment { get; set; } = "text-embedding-3-small";
}

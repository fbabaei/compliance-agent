namespace Compliance.Agent.Api.Options;

public sealed class GuardrailOptions
{
    public List<string> BlockedPhrases { get; set; } = new();
}

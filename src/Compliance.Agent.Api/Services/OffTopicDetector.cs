using Compliance.Agent.Api.Options;
using Microsoft.Extensions.Options;

namespace Compliance.Agent.Api.Services;

public sealed class OffTopicDetector : IOffTopicDetector
{
    private readonly GuardrailOptions _options;

    public OffTopicDetector(IOptions<GuardrailOptions> options)
    {
        _options = options.Value;
    }

    public bool IsOffTopic(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return true;
        }

        var blocked = _options.BlockedPhrases ?? new List<string>();
        return blocked.Any(phrase =>
            message.Contains(phrase, StringComparison.OrdinalIgnoreCase));
    }
}

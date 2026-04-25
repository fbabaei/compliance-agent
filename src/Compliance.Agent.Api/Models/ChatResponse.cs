namespace Compliance.Agent.Api.Models;

public sealed record ChatResponse(
    Guid SessionId,
    string Message,
    bool IsOffTopic,
    IReadOnlyList<string> MissingFields,
    IReadOnlyList<string> SuggestedActions);

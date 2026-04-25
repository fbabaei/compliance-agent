namespace Compliance.Agent.Api.Models;

public sealed record ChatRequest(Guid? SessionId, string Message, bool UseRag = true);

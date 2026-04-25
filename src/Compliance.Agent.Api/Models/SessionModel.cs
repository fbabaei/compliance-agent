namespace Compliance.Agent.Api.Models;

public sealed record SessionModel(
    Guid Id,
    DateTimeOffset CreatedAtUtc,
    List<ChatMessage> Messages);

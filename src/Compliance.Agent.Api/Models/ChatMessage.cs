namespace Compliance.Agent.Api.Models;

public sealed record ChatMessage(string Role, string Content, DateTimeOffset TimestampUtc);

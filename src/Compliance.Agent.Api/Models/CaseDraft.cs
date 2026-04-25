namespace Compliance.Agent.Api.Models;

public sealed record CaseDraft(
    Guid Id,
    Guid SessionId,
    string SourceType,
    string? OriginalText,
    string? DocumentBlobUrl,
    Dictionary<string, string> Fields,
    List<string> ClassificationCodes,
    List<string> Jurisdictions,
    List<string> MissingFields,
    string Status,
    DateTimeOffset UpdatedAtUtc);

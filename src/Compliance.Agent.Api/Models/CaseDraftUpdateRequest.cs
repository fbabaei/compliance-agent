namespace Compliance.Agent.Api.Models;

public sealed record CaseDraftUpdateRequest(
    Dictionary<string, string>? Fields,
    IReadOnlyList<string>? Jurisdictions,
    IReadOnlyList<string>? MissingFields,
    string? Status);

namespace Compliance.Agent.Api.Models;

public sealed record ExtractionResult(
    CaseDraft Draft,
    IReadOnlyList<string> MissingFields,
    string ReasoningSummary);

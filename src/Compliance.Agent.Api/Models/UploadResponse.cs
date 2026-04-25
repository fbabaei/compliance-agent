namespace Compliance.Agent.Api.Models;

public sealed record UploadResponse(
    Guid SessionId,
    Guid DocumentId,
    Guid DraftId,
    IReadOnlyList<string> MissingFields,
    string Summary,
    string BlobUri);

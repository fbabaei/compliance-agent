namespace Compliance.Agent.Api.Models;

public sealed record DocumentIngestionResult(Guid DocumentId, string BlobLikeUri, string DocumentText);

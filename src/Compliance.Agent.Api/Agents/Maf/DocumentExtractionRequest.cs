namespace Compliance.Agent.Api.Agents.Maf;

/// <summary>
/// Input record for the MAF ExtractionExecutor.
/// Encapsulates everything the Extraction Agent needs to process a document.
/// </summary>
/// <param name="SessionId">The session that owns this extraction request.</param>
/// <param name="DocumentText">Parsed plain-text content of the uploaded document.</param>
/// <param name="BlobUri">Azure Blob Storage URI of the original file (for traceability).</param>
public sealed record DocumentExtractionRequest(
    Guid SessionId,
    string DocumentText,
    string BlobUri);

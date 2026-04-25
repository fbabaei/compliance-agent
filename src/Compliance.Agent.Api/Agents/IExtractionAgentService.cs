using Compliance.Agent.Api.Models;

namespace Compliance.Agent.Api.Agents;

public interface IExtractionAgentService
{
    Task<ExtractionResult> ExtractFromTextAsync(Guid sessionId, string inputText, CancellationToken cancellationToken);
    Task<ExtractionResult> ExtractFromDocumentAsync(Guid sessionId, string documentText, string blobUri, CancellationToken cancellationToken);
}

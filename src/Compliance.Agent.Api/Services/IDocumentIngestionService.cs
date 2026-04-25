using Compliance.Agent.Api.Models;
using Microsoft.AspNetCore.Http;

namespace Compliance.Agent.Api.Services;

public interface IDocumentIngestionService
{
    Task<DocumentIngestionResult> IngestAsync(IFormFile file, CancellationToken cancellationToken);
}

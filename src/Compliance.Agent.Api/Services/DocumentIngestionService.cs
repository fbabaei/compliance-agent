using System.Text;
using Compliance.Agent.Api.Models;
using Microsoft.AspNetCore.Http;

namespace Compliance.Agent.Api.Services;

public sealed class DocumentIngestionService : IDocumentIngestionService
{
    public async Task<DocumentIngestionResult> IngestAsync(IFormFile file, CancellationToken cancellationToken)
    {
        await using var stream = file.OpenReadStream();
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: false);
        var text = await reader.ReadToEndAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(text))
        {
            text = $"Uploaded file '{file.FileName}' could not be parsed as plain text. Continue with OCR/vision in production pipeline.";
        }

        var id = Guid.NewGuid();
        var blobLikeUri = $"blob://uploaded-documents/{id}/{file.FileName}";
        return new DocumentIngestionResult(id, blobLikeUri, text);
    }
}

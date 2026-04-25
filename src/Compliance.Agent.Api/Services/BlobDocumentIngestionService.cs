using Azure.Identity;
using Azure.Storage.Blobs;
using Compliance.Agent.Api.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Compliance.Agent.Api.Options;

namespace Compliance.Agent.Api.Services;

/// <summary>
/// Uploads document files to Azure Blob Storage and returns the blob URI alongside
/// the extracted text content for downstream agent processing.
/// </summary>
public sealed class BlobDocumentIngestionService : IDocumentIngestionService
{
    private readonly BlobContainerClient _container;
    private readonly ILogger<BlobDocumentIngestionService> _logger;

    public BlobDocumentIngestionService(IOptions<BlobStorageOptions> options, ILogger<BlobDocumentIngestionService> logger)
    {
        _logger = logger;
        var containerUri = new Uri(options.Value.ContainerUri);
        _container = new BlobContainerClient(containerUri, new DefaultAzureCredential());
    }

    public async Task<DocumentIngestionResult> IngestAsync(IFormFile file, CancellationToken cancellationToken)
    {
        var documentId = Guid.NewGuid();
        var blobName = $"{documentId}/{file.FileName}";

        _logger.LogInformation("Uploading blob {BlobName} ({Size} bytes)", blobName, file.Length);

        await _container.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
        var blobClient = _container.GetBlobClient(blobName);

        await using var stream = file.OpenReadStream();

        // Read text content before uploading so we can send the stream twice if needed
        using var reader = new StreamReader(stream, leaveOpen: true);
        var extractedText = await reader.ReadToEndAsync(cancellationToken);

        // Re-upload from the same extracted text as UTF-8 bytes
        var contentBytes = System.Text.Encoding.UTF8.GetBytes(extractedText);
        using var uploadStream = new MemoryStream(contentBytes);
        await blobClient.UploadAsync(uploadStream, overwrite: true, cancellationToken: cancellationToken);

        _logger.LogInformation("Blob uploaded: {BlobUri}", blobClient.Uri);

        return new DocumentIngestionResult(documentId, blobClient.Uri.ToString(), extractedText);
    }
}

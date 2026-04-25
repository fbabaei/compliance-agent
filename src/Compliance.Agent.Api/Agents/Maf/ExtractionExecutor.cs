using Compliance.Agent.Api.Models;
using Microsoft.Agents.AI.Workflows;

namespace Compliance.Agent.Api.Agents.Maf;

/// <summary>
/// MAF Executor wrapping the Extraction Agent.
/// Sits as a specialised node in the ComplianceExtractionWorkflow pipeline.
/// Receives a DocumentExtractionRequest, delegates to IExtractionAgentService
/// (GPT-5.2 structured output), and emits an ExtractionResult downstream.
/// </summary>
public sealed class ExtractionExecutor : Executor<DocumentExtractionRequest, ExtractionResult>
{
    private readonly IExtractionAgentService _extraction;
    private readonly ILogger<ExtractionExecutor> _logger;

    public ExtractionExecutor(IExtractionAgentService extraction, ILogger<ExtractionExecutor> logger)
        : base("ExtractionAgent")
    {
        _extraction = extraction;
        _logger = logger;
    }

    public override async ValueTask<ExtractionResult> HandleAsync(
        DocumentExtractionRequest request,
        IWorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("[MAF] ExtractionExecutor handling document of {Length} chars", request.DocumentText.Length);
        var result = await _extraction.ExtractFromDocumentAsync(
            request.SessionId,
            request.DocumentText,
            request.BlobUri,
            cancellationToken);
        _logger.LogInformation("[MAF] ExtractionExecutor completed. MissingFields={Count}", result.MissingFields.Count);
        return result;
    }
}

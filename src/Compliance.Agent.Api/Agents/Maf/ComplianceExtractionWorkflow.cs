using Compliance.Agent.Api.Models;
using Microsoft.Agents.AI.Workflows;

namespace Compliance.Agent.Api.Agents.Maf;

/// <summary>
/// Orchestrates the compliance extraction pipeline using Microsoft Agent Framework (MAF).
///
/// Workflow topology:
///
///   [Input: document text]
///        │
///        ▼
///   ExtractionExecutor  ──── GPT-5.2 structured JSON extraction
///        │
///        ▼
///   [Output: ExtractionResult]
///
/// The ExtractionExecutor is an MAF Executor&lt;string, ExtractionResult&gt; node.
/// The Workflow is built once (singleton lifetime) via WorkflowBuilder, then reused
/// for every call via InProcessExecution.RunAsync which spins up an isolated Run.
///
/// Architecture mapping:
///   - Maps to the "Extraction Agent" node inside the "Microsoft Agent Framework" block
///     in Architecture-Diagram-v2.md
///   - Delegates AI calls to AzureOpenAiExtractionAgentService (GPT-5.2 via Azure OpenAI)
/// </summary>
public sealed class ComplianceExtractionWorkflow
{
    private readonly Workflow _workflow;
    private readonly ExtractionExecutor _extractionExecutor;
    private readonly ILogger<ComplianceExtractionWorkflow> _logger;

    public ComplianceExtractionWorkflow(
        ExtractionExecutor extractionExecutor,
        ILogger<ComplianceExtractionWorkflow> logger)
    {
        _extractionExecutor = extractionExecutor;
        _logger = logger;

        // Build a single-node workflow: the ExtractionExecutor is both
        // the entry point and the terminal node.
        _workflow = new WorkflowBuilder(_extractionExecutor)
            .WithOutputFrom(_extractionExecutor)
            .Build();
    }

    /// <summary>
    /// Runs the extraction workflow for a document ingestion request.
    /// </summary>
    public async Task<ExtractionResult> ExtractAsync(DocumentExtractionRequest request, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("[MAF] ComplianceExtractionWorkflow starting extraction run for session {SessionId}", request.SessionId);

        await using var run = await InProcessExecution.RunAsync(_workflow, request);

        foreach (var evt in run.NewEvents)
        {
            if (evt is ExecutorCompletedEvent completed &&
                completed.ExecutorId == "ExtractionAgent" &&
                completed.Data is ExtractionResult result)
            {
                _logger.LogInformation("[MAF] Extraction workflow run completed");
                return result;
            }
        }

        throw new InvalidOperationException("Extraction workflow completed without yielding an ExtractionResult.");
    }
}

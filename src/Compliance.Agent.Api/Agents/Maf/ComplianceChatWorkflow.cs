using Compliance.Agent.Api.Models;
using Microsoft.Agents.AI.Workflows;

namespace Compliance.Agent.Api.Agents.Maf;

/// <summary>
/// Orchestrates the compliance conversational Q&amp;A pipeline using Microsoft Agent Framework (MAF).
///
/// Workflow topology:
///
///   [Input: ChatRequest]
///        │
///        ▼
///   ChatExecutor  ──── Intent routing + RAG + off-topic guardrails
///        │                        │
///        │ [case-creation intent] ▼
///        │               ExtractionExecutor  (invoked via IExtractionAgentService tool call)
///        │                        │
///        ◄────────────────────────┘
///        ▼
///   [Output: ChatResponse]
///
/// Notes:
/// - ChatExecutor is the entry-point and terminal node.
/// - When the user intends to create a case, the ChatAgentService internally delegates
///   to IExtractionAgentService as a synchronous tool call. The result (ExtractionResult)
///   is surfaced back to the user as part of ChatResponse.MissingFields / SuggestedActions.
/// - Both executors are MAF Executor&lt;TIn,TOut&gt; wrappers that satisfy the MAF workflow contract.
///
/// Architecture mapping:
///   - Maps to the "Chat Agent" + "Extraction Agent" nodes inside the
///     "Microsoft Agent Framework" block in Architecture-Diagram-v2.md.
/// </summary>
public sealed class ComplianceChatWorkflow
{
    private readonly Workflow _workflow;
    private readonly ChatExecutor _chatExecutor;
    private readonly ILogger<ComplianceChatWorkflow> _logger;

    public ComplianceChatWorkflow(
        ChatExecutor chatExecutor,
        ILogger<ComplianceChatWorkflow> logger)
    {
        _chatExecutor = chatExecutor;
        _logger = logger;

        // Build a single-node workflow — the ChatExecutor is both entry point and
        // terminal node. Internal delegation to ExtractionAgent happens inside
        // IChatAgentService as a synchronous tool call.
        _workflow = new WorkflowBuilder(_chatExecutor)
            .WithOutputFrom(_chatExecutor)
            .Build();
    }

    /// <summary>
    /// Runs the chat workflow for a single user message.
    /// </summary>
    public async Task<ChatResponse> RunAsync(ChatRequest request, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("[MAF] ComplianceChatWorkflow starting chat run for session {SessionId}", request.SessionId);

        await using var run = await InProcessExecution.RunAsync(_workflow, request);

        foreach (var evt in run.NewEvents)
        {
            if (evt is ExecutorCompletedEvent completed &&
                completed.ExecutorId == "ChatAgent" &&
                completed.Data is ChatResponse response)
            {
                _logger.LogInformation("[MAF] Chat workflow run completed");
                return response;
            }
        }

        throw new InvalidOperationException("Chat workflow completed without yielding a ChatResponse.");
    }
}

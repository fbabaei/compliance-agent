using Compliance.Agent.Api.Models;
using Microsoft.Agents.AI.Workflows;

namespace Compliance.Agent.Api.Agents.Maf;

/// <summary>
/// MAF Executor wrapping the Chat Agent.
/// Entry-point executor in the ComplianceChatWorkflow.
/// Receives a ChatRequest, delegates to IChatAgentService, and emits a ChatResponse.
/// When the intent is case-creation, the Chat Agent internally invokes the Extraction Agent
/// as a synchronous tool call — the workflow edge to ExtractionExecutor provides the
/// structured-output result back to the Chat Agent for follow-up prompting.
/// </summary>
public sealed class ChatExecutor : Executor<ChatRequest, ChatResponse>
{
    private readonly IChatAgentService _chat;
    private readonly ILogger<ChatExecutor> _logger;

    public ChatExecutor(IChatAgentService chat, ILogger<ChatExecutor> logger)
        : base("ChatAgent")
    {
        _chat = chat;
        _logger = logger;
    }

    public override async ValueTask<ChatResponse> HandleAsync(
        ChatRequest request,
        IWorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("[MAF] ChatExecutor handling message for session {SessionId}", request.SessionId);
        var response = await _chat.ProcessAsync(request, cancellationToken);
        _logger.LogInformation("[MAF] ChatExecutor completed. IsOffTopic={IsOffTopic}", response.IsOffTopic);
        return response;
    }
}

using Compliance.Agent.Api.Models;

namespace Compliance.Agent.Api.Agents;

public interface IChatAgentService
{
    Task<ChatResponse> ProcessAsync(ChatRequest request, CancellationToken cancellationToken);
}

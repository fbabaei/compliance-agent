using Compliance.Agent.Api.Models;
using Compliance.Agent.Api.Repositories;
using Compliance.Agent.Api.Services;

namespace Compliance.Agent.Api.Agents;

public sealed class ChatAgentService : IChatAgentService
{
    private readonly ISessionRepository _sessions;
    private readonly IFAQRepository _faq;
    private readonly IOffTopicDetector _offTopic;
    private readonly IExtractionAgentService _extraction;

    public ChatAgentService(
        ISessionRepository sessions,
        IFAQRepository faq,
        IOffTopicDetector offTopic,
        IExtractionAgentService extraction)
    {
        _sessions = sessions;
        _faq = faq;
        _offTopic = offTopic;
        _extraction = extraction;
    }

    public async Task<ChatResponse> ProcessAsync(ChatRequest request, CancellationToken cancellationToken)
    {
        var session = request.SessionId.HasValue
            ? _sessions.GetSession(request.SessionId.Value) ?? _sessions.CreateSession()
            : _sessions.CreateSession();

        _sessions.AppendMessage(session.Id, new ChatMessage("user", request.Message, DateTimeOffset.UtcNow));

        if (_offTopic.IsOffTopic(request.Message))
        {
            const string offTopicMessage = "I can only help with regulatory compliance workflows. Please ask a compliance-related question or provide case details.";
            _sessions.AppendMessage(session.Id, new ChatMessage("assistant", offTopicMessage, DateTimeOffset.UtcNow));
            return new ChatResponse(session.Id, offTopicMessage, true, Array.Empty<string>(), new[] { "Ask compliance question", "Upload compliance document" });
        }

        if (LooksLikeCaseCreation(request.Message))
        {
            var extraction = await _extraction.ExtractFromTextAsync(session.Id, request.Message, cancellationToken);
            var followUp = extraction.MissingFields.Count > 0
                ? $"I extracted a draft case. Missing fields: {string.Join(", ", extraction.MissingFields)}. Please provide them or say 'proceed'."
                : "I extracted all required fields. Say 'proceed' to finalize the case draft.";

            _sessions.AppendMessage(session.Id, new ChatMessage("assistant", followUp, DateTimeOffset.UtcNow));
            return new ChatResponse(
                session.Id,
                followUp,
                false,
                extraction.MissingFields,
                new[] { "Provide missing fields", "Proceed" });
        }

        var grounding = string.Join(" ", _faq.VectorSearch(request.Message));
        var answer = $"Grounded compliance guidance: {grounding}";
        _sessions.AppendMessage(session.Id, new ChatMessage("assistant", answer, DateTimeOffset.UtcNow));

        return new ChatResponse(
            session.Id,
            answer,
            false,
            Array.Empty<string>(),
            new[] { "Ask follow-up", "Upload document" });
    }

    private static bool LooksLikeCaseCreation(string message)
    {
        var lowered = message.ToLowerInvariant();
        return lowered.Contains("case") || lowered.Contains("incident") || lowered.Contains("transaction") || lowered.Contains("draft");
    }
}

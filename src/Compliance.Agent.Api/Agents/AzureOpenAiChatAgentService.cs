using Azure.AI.OpenAI;
using Azure.Identity;
using Compliance.Agent.Api.Models;
using Compliance.Agent.Api.Options;
using Compliance.Agent.Api.Repositories;
using Compliance.Agent.Api.Services;
using Microsoft.Extensions.Options;
using OpenAI.Chat;
using ModelChatMessage = Compliance.Agent.Api.Models.ChatMessage;

namespace Compliance.Agent.Api.Agents;

/// <summary>
/// Chat agent backed by Azure OpenAI GPT-5.2.
/// Intent routing: off-topic → guardrail | case-creation → ExtractionAgent | else → RAG grounding.
/// RAG grounding fetches FAQ entries from SQL and provides them as context in the system prompt.
/// </summary>
public sealed class AzureOpenAiChatAgentService : IChatAgentService
{
    private readonly ChatClient _chatClient;
    private readonly ISessionRepository _sessions;
    private readonly IFAQRepository _faq;
    private readonly IOffTopicDetector _offTopic;
    private readonly IExtractionAgentService _extraction;
    private readonly ILogger<AzureOpenAiChatAgentService> _logger;

    private const string BaseSystemPrompt = """
        You are a regulatory compliance assistant. You help users with:
        - Answering compliance questions using the provided knowledge base
        - Extracting structured case details from user descriptions
        - Guiding users through the case creation workflow

        Only answer questions related to regulatory compliance. Politely decline off-topic requests.
        Be concise, precise, and reference specific regulatory guidance when available.
        """;

    public AzureOpenAiChatAgentService(
        IOptions<AzureOpenAiOptions> options,
        ISessionRepository sessions,
        IFAQRepository faq,
        IOffTopicDetector offTopic,
        IExtractionAgentService extraction,
        ILogger<AzureOpenAiChatAgentService> logger)
    {
        _sessions = sessions;
        _faq = faq;
        _offTopic = offTopic;
        _extraction = extraction;
        _logger = logger;

        var azureClient = new AzureOpenAIClient(
            new Uri(options.Value.Endpoint),
            new DefaultAzureCredential());
        _chatClient = azureClient.GetChatClient(options.Value.ChatDeployment);
    }

    public async Task<ChatResponse> ProcessAsync(ChatRequest request, CancellationToken cancellationToken)
    {
        var session = request.SessionId.HasValue
            ? _sessions.GetSession(request.SessionId.Value) ?? _sessions.CreateSession()
            : _sessions.CreateSession();

        _sessions.AppendMessage(session.Id, new ModelChatMessage("user", request.Message, DateTimeOffset.UtcNow));

        // Guardrail: off-topic detection
        if (_offTopic.IsOffTopic(request.Message))
        {
            const string offTopicMessage = "I can only help with regulatory compliance workflows. Please ask a compliance-related question or provide case details.";
            _sessions.AppendMessage(session.Id, new ModelChatMessage("assistant", offTopicMessage, DateTimeOffset.UtcNow));
            return new ChatResponse(session.Id, offTopicMessage, true, Array.Empty<string>(),
                new[] { "Ask compliance question", "Upload compliance document" });
        }

        // Intent: case creation → delegate to extraction agent
        if (LooksLikeCaseCreation(request.Message))
        {
            var result = await _extraction.ExtractFromTextAsync(session.Id, request.Message, cancellationToken);
            var followUp = result.MissingFields.Count > 0
                ? $"I extracted a draft case. Missing fields: {string.Join(", ", result.MissingFields)}. Please provide them or say 'proceed'."
                : "I extracted all required fields. Say 'proceed' to finalize the case draft.";

            _sessions.AppendMessage(session.Id, new ModelChatMessage("assistant", followUp, DateTimeOffset.UtcNow));
            return new ChatResponse(session.Id, followUp, false, result.MissingFields,
                new[] { "Provide missing fields", "Proceed" });
        }

        // RAG: retrieve relevant FAQ passages and provide as grounding context
        var groundingPassages = _faq.VectorSearch(request.Message, topK: 3);
        var groundingContext = groundingPassages.Count > 0
            ? "Relevant compliance knowledge:\n" + string.Join("\n- ", groundingPassages)
            : string.Empty;

        var systemWithGrounding = string.IsNullOrEmpty(groundingContext)
            ? BaseSystemPrompt
            : $"{BaseSystemPrompt}\n\n{groundingContext}";

        // Build conversation history for multi-turn context
        var messages = new List<OpenAI.Chat.ChatMessage> { new SystemChatMessage(systemWithGrounding) };
        foreach (var m in session.Messages.TakeLast(10))
        {
            messages.Add(m.Role == "user"
                ? (OpenAI.Chat.ChatMessage)new UserChatMessage(m.Content)
                : new AssistantChatMessage(m.Content));
        }

        ChatCompletion completion;
        try
        {
            completion = await _chatClient.CompleteChatAsync(messages, cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Azure OpenAI chat call failed");
            throw;
        }

        var answer = completion.Content[0].Text;
        _sessions.AppendMessage(session.Id, new ModelChatMessage("assistant", answer, DateTimeOffset.UtcNow));

        return new ChatResponse(session.Id, answer, false, Array.Empty<string>(), Array.Empty<string>());
    }

    private static bool LooksLikeCaseCreation(string message)
    {
        var lower = message.ToLowerInvariant();
        return lower.Contains("case") || lower.Contains("incident") ||
               lower.Contains("transaction") || lower.Contains("draft");
    }
}

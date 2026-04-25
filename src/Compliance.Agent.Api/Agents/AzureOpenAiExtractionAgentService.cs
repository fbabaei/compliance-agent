using System.Text.Json;
using System.Text.RegularExpressions;
using Azure.AI.OpenAI;
using Azure.Identity;
using Compliance.Agent.Api.Models;
using Compliance.Agent.Api.Options;
using Compliance.Agent.Api.Repositories;
using Compliance.Agent.Api.Services;
using Microsoft.Extensions.Options;
using OpenAI.Chat;

namespace Compliance.Agent.Api.Agents;

/// <summary>
/// Extraction agent backed by Azure OpenAI GPT-5.2 with structured JSON output.
/// Calls the model with a system prompt that requests a strict JSON schema response,
/// then maps the result to a CaseDraft.
/// </summary>
public sealed class AzureOpenAiExtractionAgentService : IExtractionAgentService
{
    private readonly ChatClient _chatClient;
    private readonly ICaseDraftRepository _drafts;
    private readonly IJurisdictionValidator _jurisdictionValidator;
    private readonly ISchemaValidator _schemaValidator;
    private readonly ILogger<AzureOpenAiExtractionAgentService> _logger;

    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    // Regex fallback for code extraction when the model omits them
    private static readonly Regex CodePattern = new(@"\b[A-Z]{2,4}-\d{2,6}\b", RegexOptions.Compiled);

    private const string SystemPrompt = """
        You are a regulatory compliance extraction assistant.
        Given input text, return ONLY a JSON object (no markdown fences) with these keys:
        {
          "caseTitle": "<string>",
          "triggerDate": "<ISO-8601 date or empty string>",
          "jurisdiction": "<one of: US, UK, EU, UAE, CA, AU, SG, CH — or empty>",
          "summary": "<concise summary up to 500 chars>",
          "classificationReasoning": "<chain-of-reasoning explaining the regulatory classification>",
          "classificationCodes": ["<e.g. BSAM-001>"],
          "additionalJurisdictions": ["<ISO codes>"]
        }
        Respond ONLY with the JSON object. No explanations outside the JSON.
        """;

    public AzureOpenAiExtractionAgentService(
        IOptions<AzureOpenAiOptions> options,
        ICaseDraftRepository drafts,
        IJurisdictionValidator jurisdictionValidator,
        ISchemaValidator schemaValidator,
        ILogger<AzureOpenAiExtractionAgentService> logger)
    {
        _drafts = drafts;
        _jurisdictionValidator = jurisdictionValidator;
        _schemaValidator = schemaValidator;
        _logger = logger;

        var azureClient = new AzureOpenAIClient(
            new Uri(options.Value.Endpoint),
            new DefaultAzureCredential());
        _chatClient = azureClient.GetChatClient(options.Value.ChatDeployment);
    }

    public async Task<ExtractionResult> ExtractFromTextAsync(Guid sessionId, string inputText, CancellationToken cancellationToken)
    {
        return await CallModelAndBuildDraft(sessionId, inputText, null, cancellationToken);
    }

    public async Task<ExtractionResult> ExtractFromDocumentAsync(Guid sessionId, string documentText, string blobUri, CancellationToken cancellationToken)
    {
        return await CallModelAndBuildDraft(sessionId, documentText, blobUri, cancellationToken);
    }

    private async Task<ExtractionResult> CallModelAndBuildDraft(
        Guid sessionId, string source, string? blobUri, CancellationToken cancellationToken)
    {
        var userContent = source.Length > 4000 ? source[..4000] : source;

        ChatCompletion completion;
        try
        {
            completion = await _chatClient.CompleteChatAsync(
                new List<OpenAI.Chat.ChatMessage>
                {
                    new SystemChatMessage(SystemPrompt),
                    new UserChatMessage(userContent)
                },
                cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Azure OpenAI extraction call failed");
            throw;
        }

        var rawJson = completion.Content[0].Text;
        _logger.LogDebug("Extraction model response: {Json}", rawJson);

        return ParseAndPersist(sessionId, source, blobUri, rawJson);
    }

    private ExtractionResult ParseAndPersist(Guid sessionId, string source, string? blobUri, string rawJson)
    {
        Dictionary<string, string> fields;
        List<string> classificationCodes;
        List<string> jurisdictions;

        try
        {
            using var doc = JsonDocument.Parse(rawJson);
            var root = doc.RootElement;

            fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["caseTitle"] = root.TryGetProperty("caseTitle", out var ct) ? ct.GetString() ?? "" : "",
                ["triggerDate"] = root.TryGetProperty("triggerDate", out var td) ? td.GetString() ?? "" : "",
                ["jurisdiction"] = root.TryGetProperty("jurisdiction", out var j) ? j.GetString() ?? "" : "",
                ["summary"] = root.TryGetProperty("summary", out var s) ? s.GetString() ?? "" : "",
                ["classificationReasoning"] = root.TryGetProperty("classificationReasoning", out var cr) ? cr.GetString() ?? "" : ""
            };

            classificationCodes = root.TryGetProperty("classificationCodes", out var codes)
                ? codes.EnumerateArray().Select(e => e.GetString() ?? "").Where(s => s.Length > 0).ToList()
                : CodePattern.Matches(source).Select(m => m.Value).Distinct().ToList();

            var allJurisdictions = new List<string>();
            if (!string.IsNullOrEmpty(fields["jurisdiction"]))
                allJurisdictions.Add(fields["jurisdiction"]);
            if (root.TryGetProperty("additionalJurisdictions", out var addlJ))
                allJurisdictions.AddRange(addlJ.EnumerateArray().Select(e => e.GetString() ?? "").Where(s => s.Length > 0));

            jurisdictions = _jurisdictionValidator.Validate(allJurisdictions).ToList();
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse extraction JSON; falling back to empty draft");
            fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["summary"] = source.Length > 500 ? source[..500] : source,
                ["classificationReasoning"] = "Parse error — manual review required."
            };
            classificationCodes = CodePattern.Matches(source).Select(m => m.Value).Distinct().ToList();
            jurisdictions = [];
        }

        var draft = new CaseDraft(
            Id: Guid.NewGuid(),
            SessionId: sessionId,
            SourceType: blobUri is null ? "text" : "document",
            OriginalText: source,
            DocumentBlobUrl: blobUri,
            Fields: fields,
            ClassificationCodes: classificationCodes,
            Jurisdictions: jurisdictions,
            MissingFields: new List<string>(),
            Status: "Draft",
            UpdatedAtUtc: DateTimeOffset.UtcNow);

        var missing = _schemaValidator.GetMissingFields(draft).ToList();
        var finalDraft = draft with { MissingFields = missing };
        _drafts.UpsertCaseDraft(finalDraft);

        return new ExtractionResult(finalDraft, missing, "GPT-5.2 structured extraction complete.");
    }
}

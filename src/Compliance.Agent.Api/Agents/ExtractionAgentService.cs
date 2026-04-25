using System.Text.RegularExpressions;
using Compliance.Agent.Api.Models;
using Compliance.Agent.Api.Repositories;
using Compliance.Agent.Api.Services;

namespace Compliance.Agent.Api.Agents;

public sealed class ExtractionAgentService : IExtractionAgentService
{
    private readonly ICaseDraftRepository _drafts;
    private readonly IJurisdictionValidator _jurisdictionValidator;
    private readonly ISchemaValidator _schemaValidator;

    public ExtractionAgentService(
        ICaseDraftRepository drafts,
        IJurisdictionValidator jurisdictionValidator,
        ISchemaValidator schemaValidator)
    {
        _drafts = drafts;
        _jurisdictionValidator = jurisdictionValidator;
        _schemaValidator = schemaValidator;
    }

    public Task<ExtractionResult> ExtractFromTextAsync(Guid sessionId, string inputText, CancellationToken cancellationToken)
    {
        return Task.FromResult(CreateDraft(sessionId, inputText, null));
    }

    public Task<ExtractionResult> ExtractFromDocumentAsync(Guid sessionId, string documentText, string blobUri, CancellationToken cancellationToken)
    {
        return Task.FromResult(CreateDraft(sessionId, documentText, blobUri));
    }

    private ExtractionResult CreateDraft(Guid sessionId, string source, string? blobUri)
    {
        var codeMatches = Regex.Matches(source, @"\b[A-Z]{2,4}-\d{2,6}\b")
            .Select(m => m.Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var jurisdictionCandidates = Regex.Matches(source, @"\b(US|UK|EU|UAE|CA|AU|SG|CH)\b", RegexOptions.IgnoreCase)
            .Select(m => m.Value);

        var jurisdictions = _jurisdictionValidator.Validate(jurisdictionCandidates).ToList();

        var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["summary"] = source.Length > 500 ? source[..500] : source,
            ["classificationReasoning"] = "Preliminary extraction completed. In production, GPT-5.2 structured output returns detailed chain-of-reasoning evidence.",
            ["jurisdiction"] = jurisdictions.FirstOrDefault() ?? string.Empty
        };

        var draft = new CaseDraft(
            Id: Guid.NewGuid(),
            SessionId: sessionId,
            SourceType: blobUri is null ? "text" : "document",
            OriginalText: source,
            DocumentBlobUrl: blobUri,
            Fields: fields,
            ClassificationCodes: codeMatches,
            Jurisdictions: jurisdictions,
            MissingFields: new List<string>(),
            Status: "Draft",
            UpdatedAtUtc: DateTimeOffset.UtcNow);

        var missing = _schemaValidator.GetMissingFields(draft).ToList();
        var draftWithMissing = draft with { MissingFields = missing };
        _drafts.UpsertCaseDraft(draftWithMissing);

        return new ExtractionResult(
            draftWithMissing,
            missing,
            "Field extraction complete. Missing fields should be collected through follow-up prompts.");
    }
}

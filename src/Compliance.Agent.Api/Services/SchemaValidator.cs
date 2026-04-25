using Compliance.Agent.Api.Models;

namespace Compliance.Agent.Api.Services;

public sealed class SchemaValidator : ISchemaValidator
{
    private static readonly string[] RequiredFields =
    {
        "caseTitle",
        "triggerDate",
        "jurisdiction",
        "summary",
        "classificationReasoning"
    };

    public IReadOnlyList<string> GetMissingFields(CaseDraft draft)
    {
        return RequiredFields
            .Where(field => !draft.Fields.TryGetValue(field, out var value) || string.IsNullOrWhiteSpace(value))
            .ToArray();
    }
}

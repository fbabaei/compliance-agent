using Compliance.Agent.Api.Models;

namespace Compliance.Agent.Api.Services;

public interface ISchemaValidator
{
    IReadOnlyList<string> GetMissingFields(CaseDraft draft);
}

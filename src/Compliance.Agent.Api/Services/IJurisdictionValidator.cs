namespace Compliance.Agent.Api.Services;

public interface IJurisdictionValidator
{
    IReadOnlyList<string> Validate(IEnumerable<string> jurisdictions);
}

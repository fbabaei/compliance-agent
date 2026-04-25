namespace Compliance.Agent.Api.Services;

public sealed class JurisdictionValidator : IJurisdictionValidator
{
    private static readonly HashSet<string> Allowed = new(StringComparer.OrdinalIgnoreCase)
    {
        "US", "UK", "EU", "UAE", "CA", "AU", "SG", "CH"
    };

    public IReadOnlyList<string> Validate(IEnumerable<string> jurisdictions)
    {
        return jurisdictions
            .Where(j => !string.IsNullOrWhiteSpace(j) && Allowed.Contains(j))
            .Select(j => j.ToUpperInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}

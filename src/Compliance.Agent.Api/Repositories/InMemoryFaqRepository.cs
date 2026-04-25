namespace Compliance.Agent.Api.Repositories;

public sealed class InMemoryFaqRepository : IFAQRepository
{
    private static readonly string[] Seed =
    {
        "Report potential sanctions screening hits within required SLA for your jurisdiction.",
        "Preserve auditable decision evidence for each classification and jurisdiction mapping.",
        "Escalate high-risk cross-border transactions for enhanced due diligence."
    };

    public IReadOnlyList<string> VectorSearch(string query, int topK = 3)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return Seed.Take(topK).ToArray();
        }

        return Seed
            .OrderByDescending(s => Score(s, query))
            .Take(topK)
            .ToArray();
    }

    private static int Score(string value, string query)
    {
        return query
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Count(token => value.Contains(token, StringComparison.OrdinalIgnoreCase));
    }
}

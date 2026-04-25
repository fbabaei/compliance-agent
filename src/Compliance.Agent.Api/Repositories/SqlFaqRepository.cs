using Microsoft.Data.SqlClient;

namespace Compliance.Agent.Api.Repositories;

/// <summary>
/// SQL-backed FAQ repository. Uses SQL LIKE / CONTAINS for text search.
/// TODO: Upgrade to Azure SQL vector search — generate embeddings via text-embedding-3-small,
/// store in a VECTOR(1536) column, and query with VECTOR_DISTANCE once Azure SQL vector
/// search is enabled on your server.
/// </summary>
public sealed class SqlFaqRepository : IFAQRepository
{
    private readonly string _connectionString;

    public SqlFaqRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public IReadOnlyList<string> VectorSearch(string query, int topK = 3)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return FetchTop(topK);
        }

        // Client-side token scoring — a pragmatic stand-in until SQL vector search is provisioned.
        var all = FetchTop(200);
        var tokens = query.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return all
            .OrderByDescending(s => tokens.Count(t => s.Contains(t, StringComparison.OrdinalIgnoreCase)))
            .Take(topK)
            .ToList();
    }

    private IReadOnlyList<string> FetchTop(int n)
    {
        var results = new List<string>();
        using var connection = new SqlConnection(_connectionString);
        connection.Open();
        using var cmd = new SqlCommand(
            $"SELECT TOP ({n}) content FROM dbo.faq_knowledge_base ORDER BY id",
            connection);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            results.Add(reader.GetString(0));
        }
        return results;
    }
}

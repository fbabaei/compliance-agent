namespace Compliance.Agent.Api.Repositories;

public interface IFAQRepository
{
    IReadOnlyList<string> VectorSearch(string query, int topK = 3);
}

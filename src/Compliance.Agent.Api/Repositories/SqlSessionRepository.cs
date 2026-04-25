using System.Text.Json;
using Compliance.Agent.Api.Models;
using Microsoft.Data.SqlClient;

namespace Compliance.Agent.Api.Repositories;

public sealed class SqlSessionRepository : ISessionRepository
{
    private readonly string _connectionString;
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    public SqlSessionRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public SessionModel CreateSession()
    {
        var session = new SessionModel(Guid.NewGuid(), DateTimeOffset.UtcNow, new List<ChatMessage>());
        using var connection = new SqlConnection(_connectionString);
        connection.Open();
        using var cmd = new SqlCommand(
            "INSERT INTO dbo.sessions (id, created_at_utc, messages_json) VALUES (@id, @created, @msgs)",
            connection);
        cmd.Parameters.AddWithValue("@id", session.Id);
        cmd.Parameters.AddWithValue("@created", session.CreatedAtUtc);
        cmd.Parameters.AddWithValue("@msgs", "[]");
        cmd.ExecuteNonQuery();
        return session;
    }

    public SessionModel? GetSession(Guid id)
    {
        using var connection = new SqlConnection(_connectionString);
        connection.Open();
        using var cmd = new SqlCommand(
            "SELECT id, created_at_utc, messages_json FROM dbo.sessions WHERE id = @id",
            connection);
        cmd.Parameters.AddWithValue("@id", id);
        using var reader = cmd.ExecuteReader();
        if (!reader.Read()) return null;

        var messages = JsonSerializer.Deserialize<List<ChatMessage>>(reader.GetString(2), JsonOpts) ?? [];
        return new SessionModel(reader.GetGuid(0), reader.GetDateTimeOffset(1), messages);
    }

    public void AppendMessage(Guid sessionId, ChatMessage message)
    {
        using var connection = new SqlConnection(_connectionString);
        connection.Open();
        using var read = new SqlCommand(
            "SELECT messages_json FROM dbo.sessions WHERE id = @id",
            connection);
        read.Parameters.AddWithValue("@id", sessionId);
        var existing = read.ExecuteScalar()?.ToString() ?? "[]";
        var messages = JsonSerializer.Deserialize<List<ChatMessage>>(existing, JsonOpts) ?? [];
        messages.Add(message);
        var updated = JsonSerializer.Serialize(messages, JsonOpts);

        using var write = new SqlCommand(
            "UPDATE dbo.sessions SET messages_json = @msgs WHERE id = @id",
            connection);
        write.Parameters.AddWithValue("@msgs", updated);
        write.Parameters.AddWithValue("@id", sessionId);
        write.ExecuteNonQuery();
    }

    public bool DeleteSession(Guid id)
    {
        using var connection = new SqlConnection(_connectionString);
        connection.Open();
        using var cmd = new SqlCommand("DELETE FROM dbo.sessions WHERE id = @id", connection);
        cmd.Parameters.AddWithValue("@id", id);
        return cmd.ExecuteNonQuery() > 0;
    }
}

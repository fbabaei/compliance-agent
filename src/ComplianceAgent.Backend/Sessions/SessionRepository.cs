using Microsoft.Data.SqlClient;

namespace ComplianceAgent.Backend.Sessions;

/// <summary>
/// Minimal Azure SQL repository for sessions and session turns. Uses parameterized queries
/// (no inline SQL) and writes UTC timestamps. Caller is responsible for ensuring the schema
/// in <c>infra/sql/Sessions.sql</c> has been applied.
/// </summary>
public sealed class SessionRepository
{
    private readonly string _connectionString;
    private readonly Func<DateTimeOffset> _now;

    public SessionRepository(string connectionString, Func<DateTimeOffset>? now = null)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException("Connection string is required.", nameof(connectionString));
        }

        _connectionString = connectionString;
        _now = now ?? (() => DateTimeOffset.UtcNow);
    }

    public async Task<Session> CreateAsync(string ownerId, CancellationToken cancellationToken = default)
    {
        var session = Session.NewActive(ownerId, _now);

        const string sql = """
            INSERT INTO dbo.Sessions
                (SessionId, OwnerId, Status, CreatedAtUtc, UpdatedAtUtc, LastActivityUtc)
            VALUES
                (@SessionId, @OwnerId, @Status, @CreatedAtUtc, @UpdatedAtUtc, @LastActivityUtc);
            """;

        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync(cancellationToken);
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@SessionId", session.SessionId);
        cmd.Parameters.AddWithValue("@OwnerId", session.OwnerId);
        cmd.Parameters.AddWithValue("@Status", session.Status);
        cmd.Parameters.AddWithValue("@CreatedAtUtc", session.CreatedAtUtc);
        cmd.Parameters.AddWithValue("@UpdatedAtUtc", session.UpdatedAtUtc);
        cmd.Parameters.AddWithValue("@LastActivityUtc", session.LastActivityUtc);
        await cmd.ExecuteNonQueryAsync(cancellationToken);

        return session;
    }

    public async Task<Session?> GetAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT SessionId, OwnerId, Status, CreatedAtUtc, UpdatedAtUtc, LastActivityUtc
            FROM dbo.Sessions
            WHERE SessionId = @SessionId;
            """;

        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync(cancellationToken);
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@SessionId", sessionId);

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new Session
        {
            SessionId = reader.GetString(0),
            OwnerId = reader.GetString(1),
            Status = reader.GetString(2),
            CreatedAtUtc = reader.GetDateTimeOffset(3),
            UpdatedAtUtc = reader.GetDateTimeOffset(4),
            LastActivityUtc = reader.GetDateTimeOffset(5),
        };
    }

    public async Task UpdateStatusAsync(string sessionId, string newStatus, CancellationToken cancellationToken = default)
    {
        var current = await GetAsync(sessionId, cancellationToken)
            ?? throw new InvalidOperationException($"Session '{sessionId}' not found.");

        if (!SessionStatus.CanTransition(current.Status, newStatus))
        {
            throw new InvalidOperationException(
                $"Invalid status transition '{current.Status}' -> '{newStatus}'.");
        }

        var now = _now();

        const string sql = """
            UPDATE dbo.Sessions
            SET Status = @Status,
                UpdatedAtUtc = @UpdatedAtUtc,
                LastActivityUtc = @LastActivityUtc
            WHERE SessionId = @SessionId;
            """;

        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync(cancellationToken);
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@SessionId", sessionId);
        cmd.Parameters.AddWithValue("@Status", newStatus);
        cmd.Parameters.AddWithValue("@UpdatedAtUtc", now);
        cmd.Parameters.AddWithValue("@LastActivityUtc", now);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task TouchAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        var now = _now();

        const string sql = """
            UPDATE dbo.Sessions
            SET LastActivityUtc = @LastActivityUtc,
                UpdatedAtUtc = @UpdatedAtUtc
            WHERE SessionId = @SessionId;
            """;

        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync(cancellationToken);
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@SessionId", sessionId);
        cmd.Parameters.AddWithValue("@UpdatedAtUtc", now);
        cmd.Parameters.AddWithValue("@LastActivityUtc", now);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<long> AppendTurnAsync(string sessionId, string role, string content, CancellationToken cancellationToken = default)
    {
        if (role is not ("user" or "agent"))
        {
            throw new ArgumentException("Role must be 'user' or 'agent'.", nameof(role));
        }

        var now = _now();

        const string sql = """
            DECLARE @NextIndex INT = (
                SELECT ISNULL(MAX(TurnIndex), -1) + 1
                FROM dbo.SessionTurns
                WHERE SessionId = @SessionId
            );

            INSERT INTO dbo.SessionTurns (SessionId, TurnIndex, Role, Content, CreatedAtUtc)
            OUTPUT INSERTED.TurnId
            VALUES (@SessionId, @NextIndex, @Role, @Content, @CreatedAtUtc);

            UPDATE dbo.Sessions
            SET LastActivityUtc = @CreatedAtUtc,
                UpdatedAtUtc = @CreatedAtUtc
            WHERE SessionId = @SessionId;
            """;

        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync(cancellationToken);
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@SessionId", sessionId);
        cmd.Parameters.AddWithValue("@Role", role);
        cmd.Parameters.AddWithValue("@Content", content ?? string.Empty);
        cmd.Parameters.AddWithValue("@CreatedAtUtc", now);

        var turnId = await cmd.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt64(turnId);
    }

    public async Task<IReadOnlyList<SessionTurn>> GetTurnsAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT TurnId, SessionId, TurnIndex, Role, Content, CreatedAtUtc
            FROM dbo.SessionTurns
            WHERE SessionId = @SessionId
            ORDER BY TurnIndex ASC;
            """;

        var turns = new List<SessionTurn>();

        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync(cancellationToken);
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@SessionId", sessionId);

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            turns.Add(new SessionTurn
            {
                TurnId = reader.GetInt64(0),
                SessionId = reader.GetString(1),
                TurnIndex = reader.GetInt32(2),
                Role = reader.GetString(3),
                Content = reader.GetString(4),
                CreatedAtUtc = reader.GetDateTimeOffset(5),
            });
        }

        return turns;
    }
}

namespace ComplianceAgent.Backend.Sessions;

/// <summary>
/// Chat/session record persisted in dbo.Sessions.
/// </summary>
public sealed class Session
{
    public string SessionId { get; init; } = string.Empty;
    public string OwnerId { get; init; } = string.Empty;
    public string Status { get; set; } = SessionStatus.Active;
    public DateTimeOffset CreatedAtUtc { get; init; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public DateTimeOffset LastActivityUtc { get; set; }

    /// <summary>
    /// Creates a new active session for the given owner. Used by the repository
    /// and by tests to verify default state without touching the database.
    /// </summary>
    public static Session NewActive(string ownerId, Func<DateTimeOffset>? now = null)
    {
        if (string.IsNullOrWhiteSpace(ownerId))
        {
            throw new ArgumentException("OwnerId is required.", nameof(ownerId));
        }

        var timestamp = (now ?? (() => DateTimeOffset.UtcNow)).Invoke();

        return new Session
        {
            SessionId = Guid.NewGuid().ToString("N"),
            OwnerId = ownerId,
            Status = SessionStatus.Active,
            CreatedAtUtc = timestamp,
            UpdatedAtUtc = timestamp,
            LastActivityUtc = timestamp,
        };
    }
}

/// <summary>
/// Single turn (user or agent message) in a session, persisted in dbo.SessionTurns.
/// </summary>
public sealed class SessionTurn
{
    public long TurnId { get; init; }
    public string SessionId { get; init; } = string.Empty;
    public int TurnIndex { get; init; }
    public string Role { get; init; } = "user";
    public string Content { get; init; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; init; }
}

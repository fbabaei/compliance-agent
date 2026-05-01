namespace ComplianceAgent.Backend.Sessions;

/// <summary>
/// Allowed session lifecycle states. Stored as strings in dbo.Sessions.Status.
/// </summary>
public static class SessionStatus
{
    public const string Active = "active";
    public const string AwaitingUser = "awaiting_user";
    public const string Completed = "completed";
    public const string Abandoned = "abandoned";

    public static readonly IReadOnlyCollection<string> All =
        new[] { Active, AwaitingUser, Completed, Abandoned };

    /// <summary>
    /// Returns true if a transition from <paramref name="from"/> to <paramref name="to"/> is valid.
    /// Terminal states (completed, abandoned) cannot transition further.
    /// </summary>
    public static bool CanTransition(string from, string to)
    {
        if (string.IsNullOrWhiteSpace(from) || string.IsNullOrWhiteSpace(to))
        {
            return false;
        }

        if (!All.Contains(from) || !All.Contains(to))
        {
            return false;
        }

        return from switch
        {
            Active => to is AwaitingUser or Completed or Abandoned,
            AwaitingUser => to is Active or Completed or Abandoned,
            Completed => false,
            Abandoned => false,
            _ => false,
        };
    }
}

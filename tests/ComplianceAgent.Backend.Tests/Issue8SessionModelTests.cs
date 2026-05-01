using ComplianceAgent.Backend.Sessions;
using Xunit;

namespace ComplianceAgent.Backend.Tests;

public class Issue8SessionModelTests
{
    [Theory]
    [InlineData(SessionStatus.Active, SessionStatus.AwaitingUser, true)]
    [InlineData(SessionStatus.Active, SessionStatus.Completed, true)]
    [InlineData(SessionStatus.Active, SessionStatus.Abandoned, true)]
    [InlineData(SessionStatus.AwaitingUser, SessionStatus.Active, true)]
    [InlineData(SessionStatus.AwaitingUser, SessionStatus.Completed, true)]
    [InlineData(SessionStatus.AwaitingUser, SessionStatus.Abandoned, true)]
    [InlineData(SessionStatus.Active, SessionStatus.Active, false)]
    [InlineData(SessionStatus.Completed, SessionStatus.Active, false)]
    [InlineData(SessionStatus.Completed, SessionStatus.AwaitingUser, false)]
    [InlineData(SessionStatus.Abandoned, SessionStatus.Active, false)]
    [InlineData(SessionStatus.Abandoned, SessionStatus.Completed, false)]
    public void CanTransition_AllowsExpectedTransitions(string from, string to, bool expected)
    {
        Assert.Equal(expected, SessionStatus.CanTransition(from, to));
    }

    [Theory]
    [InlineData(null, SessionStatus.Active)]
    [InlineData("", SessionStatus.Active)]
    [InlineData(SessionStatus.Active, null)]
    [InlineData(SessionStatus.Active, "unknown_state")]
    [InlineData("unknown_state", SessionStatus.Active)]
    public void CanTransition_RejectsInvalidInputs(string? from, string? to)
    {
        Assert.False(SessionStatus.CanTransition(from!, to!));
    }

    [Fact]
    public void NewActive_SetsDefaultsAndOwnership()
    {
        var fixedNow = new DateTimeOffset(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);

        var session = Session.NewActive("user-123", () => fixedNow);

        Assert.False(string.IsNullOrWhiteSpace(session.SessionId));
        Assert.Equal("user-123", session.OwnerId);
        Assert.Equal(SessionStatus.Active, session.Status);
        Assert.Equal(fixedNow, session.CreatedAtUtc);
        Assert.Equal(fixedNow, session.UpdatedAtUtc);
        Assert.Equal(fixedNow, session.LastActivityUtc);
    }

    [Fact]
    public void NewActive_RequiresOwnerId()
    {
        Assert.Throws<ArgumentException>(() => Session.NewActive(""));
        Assert.Throws<ArgumentException>(() => Session.NewActive("   "));
    }

    [Fact]
    public void SessionStatus_All_ContainsExpectedValues()
    {
        Assert.Contains(SessionStatus.Active, SessionStatus.All);
        Assert.Contains(SessionStatus.AwaitingUser, SessionStatus.All);
        Assert.Contains(SessionStatus.Completed, SessionStatus.All);
        Assert.Contains(SessionStatus.Abandoned, SessionStatus.All);
        Assert.Equal(4, SessionStatus.All.Count);
    }

    [Fact]
    public void SessionRepository_RejectsEmptyConnectionString()
    {
        Assert.Throws<ArgumentException>(() => new SessionRepository(""));
        Assert.Throws<ArgumentException>(() => new SessionRepository("   "));
    }
}

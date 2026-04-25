using System.Collections.Concurrent;
using Compliance.Agent.Api.Models;

namespace Compliance.Agent.Api.Repositories;

public sealed class InMemorySessionRepository : ISessionRepository
{
    private readonly ConcurrentDictionary<Guid, SessionModel> _sessions = new();

    public SessionModel CreateSession()
    {
        var session = new SessionModel(Guid.NewGuid(), DateTimeOffset.UtcNow, new List<ChatMessage>());
        _sessions[session.Id] = session;
        return session;
    }

    public SessionModel? GetSession(Guid id) => _sessions.TryGetValue(id, out var session) ? session : null;

    public bool DeleteSession(Guid id) => _sessions.TryRemove(id, out _);

    public void AppendMessage(Guid sessionId, ChatMessage message)
    {
        _sessions.AddOrUpdate(
            sessionId,
            _ => new SessionModel(sessionId, DateTimeOffset.UtcNow, new List<ChatMessage> { message }),
            (_, existing) =>
            {
                existing.Messages.Add(message);
                return existing;
            });
    }
}

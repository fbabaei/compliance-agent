using Compliance.Agent.Api.Models;

namespace Compliance.Agent.Api.Repositories;

public interface ISessionRepository
{
    SessionModel CreateSession();
    SessionModel? GetSession(Guid id);
    bool DeleteSession(Guid id);
    void AppendMessage(Guid sessionId, ChatMessage message);
}

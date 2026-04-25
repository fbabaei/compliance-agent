namespace Compliance.Agent.Api.Services;

public interface IOffTopicDetector
{
    bool IsOffTopic(string message);
}

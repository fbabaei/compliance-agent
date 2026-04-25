using Compliance.Agent.Api.Models;

namespace Compliance.Agent.Api.Services;

public interface IDependencyReadinessService
{
    Task<DependencyReadinessResult> CheckAsync(CancellationToken cancellationToken);
}

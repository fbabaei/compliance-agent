namespace Compliance.Agent.Api.Models;

public sealed record DependencyReadinessResult(bool IsReady, IReadOnlyDictionary<string, string> Checks);

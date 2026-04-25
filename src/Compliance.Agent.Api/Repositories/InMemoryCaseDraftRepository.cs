using System.Collections.Concurrent;
using Compliance.Agent.Api.Models;

namespace Compliance.Agent.Api.Repositories;

public sealed class InMemoryCaseDraftRepository : ICaseDraftRepository
{
    private readonly ConcurrentDictionary<Guid, CaseDraft> _drafts = new();

    public CaseDraft? GetCaseDraft(Guid draftId) => _drafts.TryGetValue(draftId, out var draft) ? draft : null;

    public void UpsertCaseDraft(CaseDraft draft) => _drafts[draft.Id] = draft;
}

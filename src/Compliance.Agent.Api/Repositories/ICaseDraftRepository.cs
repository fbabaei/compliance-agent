using Compliance.Agent.Api.Models;

namespace Compliance.Agent.Api.Repositories;

public interface ICaseDraftRepository
{
    CaseDraft? GetCaseDraft(Guid draftId);
    void UpsertCaseDraft(CaseDraft draft);
}

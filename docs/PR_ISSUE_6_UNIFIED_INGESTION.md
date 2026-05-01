## Description
Implements the ingestion + schema-validation portion of Issue #6: a unified extraction agent that accepts both file-based and text-based input, calls Azure AI Foundry, validates the JSON contract, and re-prompts the agent on schema failures.

The interactive user-facing follow-up loop (asking the human for missing fields and exiting on `create`/`proceed`) is intentionally out of scope for this PR and is delivered separately on top of this work in PR #24 (Issue #7) for the text-input path.

## Related Issue
Refs #6 (partial — ingestion + agent-side validation retry)

## What Changed
- Unified generic ingestion flow supporting:
  - `--file-input <path>`
  - `--text-input <inline-text>`
  - `--text-file <path>`
  - appsettings `InputFile` and `InputText`
- External prompt files under `src/ComplianceAgent.Backend/prompts/`:
  - `ExtractionAgentInstructions.txt`
  - `ExtractionPrompt.txt`
- `ExtractionService` calling Azure AI Foundry via `Microsoft.Agents.AI` + `Azure.AI.Projects`.
- `JsonContractValidator` enforcing the required schema:
  - `arrangementId`, `country`, `entities`, `description`, `transactionType`, `status`
  - `status` must equal `"draft"`
- Schema-failure retry loop that feeds validation errors back to the agent and re-extracts up to `Validation:MaxAttempts` times.
- Focused xUnit test project for `JsonContractValidator` (4 tests).
- `compliance-agent.sln` + `.gitignore` updates for the clean solution layout.

## Scope vs. Issue #6
Issue #6 has two halves:
1. **Unified file/text ingestion + schema validation** — covered here.
2. **User-facing follow-up loop until required data is captured or user proceeds** — not in this PR. The text-input variant of that loop is delivered in PR #24 (Issue #7) and can be generalized to file input in a follow-up.

This PR keeps the change set small and deterministic so review can focus on the ingestion + validation surface area.

## Files Changed
- `.gitignore`
- `compliance-agent.sln`
- `src/ComplianceAgent.Backend/ComplianceAgent.Backend.csproj`
- `src/ComplianceAgent.Backend/Program.cs`
- `src/ComplianceAgent.Backend/appsettings.template.json`
- `src/ComplianceAgent.Backend/prompts/ExtractionAgentInstructions.txt`
- `src/ComplianceAgent.Backend/prompts/ExtractionPrompt.txt`
- `src/ComplianceAgent.Services/ComplianceAgent.Services.csproj`
- `src/ComplianceAgent.Services/ExtractionResult.cs`
- `src/ComplianceAgent.Services/ExtractionService.cs`
- `src/ComplianceAgent.Services/FoundrySettings.cs`
- `tests/ComplianceAgent.Backend.Tests/ComplianceAgent.Backend.Tests.csproj`
- `tests/ComplianceAgent.Backend.Tests/JsonContractValidatorTests.cs`

## Validation
- `dotnet build .\compliance-agent.sln -c Debug` — 0 warnings, 0 errors.
- `dotnet test .\compliance-agent.sln -c Debug --no-build` — 4/4 passing.

## Checklist
- [x] Targets .NET 8 LTS.
- [x] Uses Azure AI Foundry via `ExtractionService` (no domain-specific logic in backend).
- [x] Supports both file and text inputs.
- [x] External prompt files (no hard-coded prompts in the hot path).
- [x] Strict JSON schema validation with agent-side retry feedback.
- [x] Build clean, tests green.
- [ ] Interactive user-facing follow-up loop — deferred (see PR #24 / Issue #7).

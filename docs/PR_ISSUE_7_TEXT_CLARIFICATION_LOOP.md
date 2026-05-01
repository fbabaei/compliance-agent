## Description
Implements Issue #7 with a generic, deterministic text-input clarification loop that runs after the existing JSON validation step. When the agent's draft still has missing required fields, the backend asks one focused follow-up question per round, accepts a free-text answer, merges it into the prompt, and re-runs extraction. The loop is bounded by `Clarification.MaxRounds` and can be disabled with `--no-clarify`.

## Related Issue
Fixes #7

## What Changed
- Added `ClarificationSettings` (`Enabled`, `MaxRounds`) to backend configuration.
- Added static `ClarificationLoop` helper with:
  - `DetectMissingFields(json)` — finds required fields that are null, empty string, or empty array (excluding `status`).
  - `BuildFollowUpQuestion(missing)` — produces a single, generic follow-up question listing the missing field names.
  - `SummarizeExtractedFields(json)` — renders the current draft so the user sees what was already extracted before answering (matches Technical-Design §5.3 "Here's what I extracted: [summary]").
  - `IsFinalizeAnswer(answer)` — recognizes the design's exit keywords (`create`, `proceed`, `done`, `finalize`, `skip`, plus empty input).
  - `MergeAnswerIntoPrompt(basePrompt, suffix, question, answer)` — appends user context to the existing extraction prompt without inventing values.
- Wired the clarification loop into `Program.cs` to run after the JSON-validation retry loop and before final draft persistence.
- Added CLI flags `--no-clarify` and repeatable `--answer <text>` for non-interactive scripted runs and tests.
- Added optional prompt file `prompts/ClarificationPromptSuffix.txt` consumed by the loop (built-in default used if absent).
- Added xUnit tests in `Issue7ClarificationTests.cs` covering missing-field detection, complete-draft early exit, empty-string/empty-array handling, follow-up question rendering, and prompt merge.

## Requirements Coverage (Issue #7)
- Generic, deterministic logic — no domain-specific terms in code, prompts, or settings.
- Reuses Azure AI Foundry agent execution path via `ExtractionService` (no new SDK or NuGet dependency added).
- CLI-only experience; no API endpoint changes (sub-issue #15 remains out of scope).
- Bounded by configurable `MaxRounds` and exits early when no fields are missing or the user types `skip`.
- Honors existing JSON contract — clarification merges new context into the extraction prompt and lets the validator continue to enforce schema.

## Files Changed
- `src/ComplianceAgent.Backend/Program.cs`
- `src/ComplianceAgent.Backend/appsettings.template.json`
- `src/ComplianceAgent.Backend/prompts/ClarificationPromptSuffix.txt`
- `tests/ComplianceAgent.Backend.Tests/Issue7ClarificationTests.cs`
- `README.md`
- `docs/PR_ISSUE_7_TEXT_CLARIFICATION_LOOP.md`

(Plus shared infrastructure already introduced by issue #6 / #9 work: `compliance-agent.sln`, `src/ComplianceAgent.Services/*`, `src/ComplianceAgent.Backend/prompts/Extraction*.txt`, `tests/ComplianceAgent.Backend.Tests/ComplianceAgent.Backend.Tests.csproj`, `tests/ComplianceAgent.Backend.Tests/JsonContractValidatorTests.cs`, `.gitignore`.)

## Validation
- `dotnet build .\\compliance-agent.sln` ✅ (0 warnings, 0 errors)
- `dotnet test .\\tests\\ComplianceAgent.Backend.Tests\\ComplianceAgent.Backend.Tests.csproj` ✅ (20/20 tests passing)

## Checklist
- [x] Generic and deterministic — no domain-specific vocabulary.
- [x] Uses existing Azure AI Foundry agent path.
- [x] No new NuGet packages.
- [x] Loop is bounded by `MaxRounds` and skippable.
- [x] CLI flags supported for non-interactive runs.
- [x] Unit tests cover all helper methods.
- [x] No PR opened from this branch.

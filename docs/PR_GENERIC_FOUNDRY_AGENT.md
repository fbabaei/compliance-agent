## Description
Implements Issue #6 with a simple, generic unified ingestion flow that uses Azure AI Foundry agent execution, supports file and text input modes, and validates JSON output.

## Related Issue
Fixes #6

## What Changed
- Unified generic ingestion flow with support for:
  - `--file-input`
  - `--text-input`
  - `--text-file`
  - appsettings `InputFile` and `InputText`
- Prompt-file loading support from backend prompts folder.
- Strict JSON contract validation with retry feedback loop.
- Simplified Foundry settings model by removing unused prompt version state.
- Focused xUnit test project for `JsonContractValidator`.
- Ignore updates for temporary local artifacts to keep commits clean.

## Requirements Coverage (Issue #6)
- Removed domain-specific wording from backend source and prompt files.
- Kept implementation simple and deterministic.
- Uses Azure AI Foundry agent path via `ExtractionService`.
- Accepts plain text input (`--text-input`, `--text-file`, and appsettings `InputText`).
- Prompt files are externalized under `src/ComplianceAgent.Backend/prompts`.
- Validates JSON output schema before final output.

## Files Changed
- `.gitignore`
- `compliance-agent.sln`
- `src/ComplianceAgent.Backend/Program.cs`
- `src/ComplianceAgent.Backend/appsettings.template.json`
- `src/ComplianceAgent.Services/FoundrySettings.cs`
- `src/ComplianceAgent.Backend/prompts/ExtractionAgentInstructions.txt`
- `src/ComplianceAgent.Backend/prompts/ExtractionPrompt.txt`
- `tests/ComplianceAgent.Backend.Tests/ComplianceAgent.Backend.Tests.csproj`
- `tests/ComplianceAgent.Backend.Tests/JsonContractValidatorTests.cs`

## Validation
- `dotnet build .\\compliance-agent.sln` ✅
- `dotnet test .\\tests\\ComplianceAgent.Backend.Tests\\ComplianceAgent.Backend.Tests.csproj` ✅

## Checklist
- [x] Feature remains simple and generic.
- [x] Uses Azure AI Foundry agent execution path.
- [x] Supports text input.
- [x] Uses external prompt files.
- [x] Validates JSON output.
- [x] No domain-specific terms in updated source/prompt files.

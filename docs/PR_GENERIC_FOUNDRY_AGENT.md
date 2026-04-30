## Description
Implements Issue #5 with a simple, generic backend flow that uses Azure AI Foundry agent execution, supports text input, loads external prompt files, and validates JSON output.

## Related Issue
Fixes #5

## What Changed
- Generic backend extraction flow with support for:
  - `--file-input`
  - `--text-input`
  - `--text-file`
  - appsettings `InputFile` and `InputText`
- Prompt-file loading support from backend prompts folder.
- Strict JSON contract validation with retry feedback loop.
- Generic defaults and naming updates for Foundry agent configuration.
- Focused xUnit test project for `JsonContractValidator`.
- Ignore updates for temporary local artifacts to keep commits clean.

## Requirements Coverage (Issue #5)
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
- [x] No Tax/DAC/MDR terms in updated source/prompt files.

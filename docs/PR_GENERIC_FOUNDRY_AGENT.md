## Summary
This change keeps the backend feature implementation simple and generic while preserving Azure AI Foundry agent usage.

## What this PR includes
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

## Guidelines compliance
- Removed domain-specific wording in backend source and prompt files.
- Kept implementation simple and deterministic.
- Uses Azure AI Foundry agent execution path via `ExtractionService`.
- Accepts plain text input modes.
- Prompt files are externalized under `src/ComplianceAgent.Backend/prompts`.
- Validates JSON output schema before final output.

## Changed files
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

## Notes
- Local temporary output files are now ignored via `.gitignore`.
- Existing unrelated working-tree changes were intentionally left untouched.

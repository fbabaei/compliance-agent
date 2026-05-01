# Compliance Agent Backend

Minimal backend service starter under `src\ComplianceAgent.Backend` that creates an Azure AI Foundry agent with the **Code Interpreter** tool and analyzes file content.

## Prerequisites

- .NET 8 SDK
- Azure AI Foundry project + model deployment
- Microsoft Entra access (run `az login`)

## Setup

1. Copy `src\ComplianceAgent.Backend\appsettings.template.json` to `src\ComplianceAgent.Backend\appsettings.json`.
2. Set `Foundry.Endpoint` and `Foundry.Model`.
3. Set `InputFile` to the file you want to analyze (default: `data\Output.json`).

## Run

```powershell
dotnet run --project .\src\ComplianceAgent.Backend\ComplianceAgent.Backend.csproj
```

The app will:

1. read the file content
2. create a Foundry agent with `HostedCodeInterpreterTool`
3. ask the agent to summarize the file
4. print assistant output and code-interpreter tool output (when available)

## Text-Input Clarification Loop (Issue #7)

After the agent produces a draft, the backend runs a generic clarification loop. When the draft still has required fields that are null, empty, or empty arrays, the backend asks one follow-up question per round, accepts free-text input, and re-runs extraction with the merged context.

Configuration in `appsettings.json`:

```json
"Clarification": {
  "Enabled": true,
  "MaxRounds": 3
}
```

CLI flags:

- `--no-clarify` — disable the clarification loop for the run.
- `--answer "<text>"` — repeatable; supplies scripted answers for non-interactive runs (one answer per round, in order).
- Type `create`, `proceed`, `done`, `finalize`, `skip`, or just press Enter at the prompt to finalize the current draft.

Each round prints a summary of the fields extracted so far and lists the missing fields before asking for additional context — matching the human-in-the-loop pattern in `docs/Technical-Design.md` §5.3.

Examples:

```powershell
# Interactive run
dotnet run --project .\src\ComplianceAgent.Backend\ComplianceAgent.Backend.csproj -- --text-input "Some draft text"

# Disable clarification
dotnet run --project .\src\ComplianceAgent.Backend\ComplianceAgent.Backend.csproj -- --text-input "Some draft text" --no-clarify

# Scripted answers
dotnet run --project .\src\ComplianceAgent.Backend\ComplianceAgent.Backend.csproj -- --text-input "Some draft text" --answer "United States" --answer "Acme Corp"
```

The loop is bounded by `Clarification.MaxRounds` and only fires when the draft is otherwise valid JSON.

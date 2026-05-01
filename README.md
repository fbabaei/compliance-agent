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

## Session DB Model (Issue #8)

Adds the Azure SQL schema and a thin repository for chat/session persistence so multi-turn
interactions survive reconnects and process restarts.

### Schema (`infra/sql/Sessions.sql`)

- `dbo.Sessions` — `SessionId` (PK), `OwnerId`, `Status`, `CreatedAtUtc`, `UpdatedAtUtc`,
  `LastActivityUtc`, plus `IX_Sessions_OwnerId_Status`.
- `dbo.SessionTurns` — `TurnId` (identity PK), `SessionId` (FK), `TurnIndex`, `Role`
  (`user` / `agent`), `Content`, `CreatedAtUtc`, plus `IX_SessionTurns_SessionId_TurnIndex`.

Apply once against your Azure SQL database (idempotent):

```powershell
sqlcmd -S "<server>.database.windows.net" -d "<db>" -G -i .\infra\sql\Sessions.sql
```

### Configuration

Add a `Sql.ConnectionString` to `appsettings.json` (Active Directory auth recommended). See
`appsettings.template.json` for the placeholder.

### Status lifecycle

`active` ⇄ `awaiting_user` → `completed` | `abandoned` (terminal). Enforced by
`SessionStatus.CanTransition` and `SessionRepository.UpdateStatusAsync`.

### Repository

`ComplianceAgent.Backend.Sessions.SessionRepository` exposes:
`CreateAsync`, `GetAsync`, `UpdateStatusAsync`, `TouchAsync`, `AppendTurnAsync`, `GetTurnsAsync`.
All queries are parameterized.

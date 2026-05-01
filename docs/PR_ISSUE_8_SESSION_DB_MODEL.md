## Description
Adds the chat / session DB model for Azure SQL: schema, POCOs, a thin parameterized repository, and unit tests. Enables resilient multi-turn interactions across reconnects and restarts as called out in [Technical-Design.md](docs/Technical-Design.md) §3, §7, §D4.

## Related Issue
Fixes #8

## What Changed
- **Schema** — `infra/sql/Sessions.sql` (idempotent):
  - `dbo.Sessions` — `SessionId` (PK), `OwnerId`, `Status`, `CreatedAtUtc`, `UpdatedAtUtc`, `LastActivityUtc`, plus `IX_Sessions_OwnerId_Status`.
  - `dbo.SessionTurns` — `TurnId` (identity PK), `SessionId` FK, `TurnIndex`, `Role` (`user`/`agent`), `Content`, `CreatedAtUtc`, plus `IX_SessionTurns_SessionId_TurnIndex`.
- **Model** — `src/ComplianceAgent.Backend/Sessions/`:
  - `SessionStatus` — constants (`active`, `awaiting_user`, `completed`, `abandoned`) + pure `CanTransition(from,to)` helper. Terminal states reject further transitions.
  - `Session`, `SessionTurn` — POCOs aligned with the schema. `Session.NewActive(ownerId, now)` factory for clean defaults.
  - `SessionRepository` — minimal `Microsoft.Data.SqlClient` repository with parameterized queries: `CreateAsync`, `GetAsync`, `UpdateStatusAsync`, `TouchAsync`, `AppendTurnAsync`, `GetTurnsAsync`. Status transitions are enforced server-side via `CanTransition`.
- **Configuration** — `appsettings.template.json` gets a `Sql.ConnectionString` placeholder using AAD `Authentication=Active Directory Default` (managed-identity friendly, no secrets).
- **Solution** — adds `compliance-agent.sln` and a focused xUnit test project.
- **Tests** — `tests/ComplianceAgent.Backend.Tests/Issue8SessionModelTests.cs` (20 cases, pure unit tests; no live SQL):
  - 11 transition cases via `[Theory]` (valid + invalid + unknown states).
  - `NewActive` defaults + ownership requirement.
  - `SessionStatus.All` membership.
  - `SessionRepository` connection-string guard.
- **Docs** — README "Session DB Model (Issue #8)" section: schema, how to apply, configuration, lifecycle.

## Requirements Coverage (Issue #8)
| Requirement | Implementation |
| --- | --- |
| Session identity | `Sessions.SessionId NVARCHAR(64) PRIMARY KEY` |
| Status | `Sessions.Status` + `SessionStatus.CanTransition` enforcement |
| Timestamps | `CreatedAtUtc`, `UpdatedAtUtc`, `LastActivityUtc` (DATETIMEOFFSET) |
| Ownership | `Sessions.OwnerId` + `IX_Sessions_OwnerId_Status` |
| Resilient multi-turn (reconnect/restart) | `SessionTurns` with monotonic `TurnIndex` and durable storage; repository replay via `GetTurnsAsync` |

## Files Changed
- `compliance-agent.sln`
- `infra/sql/Sessions.sql`
- `src/ComplianceAgent.Backend/ComplianceAgent.Backend.csproj`
- `src/ComplianceAgent.Backend/Sessions/Session.cs`
- `src/ComplianceAgent.Backend/Sessions/SessionRepository.cs`
- `src/ComplianceAgent.Backend/Sessions/SessionStatus.cs`
- `src/ComplianceAgent.Backend/appsettings.template.json`
- `tests/ComplianceAgent.Backend.Tests/ComplianceAgent.Backend.Tests.csproj`
- `tests/ComplianceAgent.Backend.Tests/Issue8SessionModelTests.cs`
- `README.md`

## Validation
- `dotnet build .\compliance-agent.sln -c Debug` — 0 warnings, 0 errors.
- `dotnet test .\compliance-agent.sln -c Debug --no-build` — 20/20 passing.

## Scope
- DB model only. Wiring into the extraction / chat flow is intentionally out of scope and will land in follow-up PRs.
- No live SQL integration tests — repository is exercised through unit tests on its pure helpers.

## Checklist
- [x] Targets .NET 8 LTS.
- [x] Parameterized SQL only (no string concatenation).
- [x] AAD-auth connection string template (no secrets).
- [x] Idempotent schema script.
- [x] Status transitions enforced.
- [x] Build clean, tests green (20/20).

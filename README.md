# MafDb

A .NET 10 sample that uses the Microsoft Agentic Framework to query SQL Server (AdventureWorks) with a deterministic, LangChain-style SQL workflow.

## Projects

- `src/MafDb.Core` - agent construction, SQL tools, and session persistence abstractions.
- `src/MafDb.ConsoleApp` - interactive CLI chat with persisted sessions.
- `src/MafDb.McpServer` - MCP server exposing the database agent as a tool over stdio.
- `tests/MafDb.Core.Tests` - unit tests for workflow, validation, context selection, and cache behavior.

## Prerequisites

- .NET SDK 10.0
- SQL Server instance with an `AdventureWorks2025` database
- Azure OpenAI deployment (chat model)
- Azure authentication available for `DefaultAzureCredential` (for example `az login`)

## Configuration

Both executable projects load settings from `appsettings.json` and environment variables.

Required keys:

- `AzureOpenAI:Endpoint`
- `AzureOpenAI:DeploymentName`
- `SqlServer:ConnectionString`

Additional key for `MafDb.ConsoleApp` session persistence:

- `SqlServer:PersistenceConnectionString`

Memory configuration (selective memory + fallback):

- `Memory:Mode = StateGraph|FullHistory` (default `StateGraph`)
- `Memory:RecentWindowSize = 8`
- `Memory:MaxPinnedFacts = 6`
- `Memory:MaxRetrievedTurns = 3`
- `Memory:ContextTokenBudget = 2500`
- `Memory:SummaryTokenBudget = 800`
- `Memory:EnableFallbackOnError = true`

Workflow configuration (deterministic SQL pipeline):

- `Workflow:Mode = Deterministic|ToolCalling` (default `Deterministic`)
- `Workflow:MaxRepairRetries = 3`
- `Workflow:SchemaCacheTtlMinutes = 30`
- `Workflow:ReturnSqlInUserText = false`
- `Workflow:FailClosedOnValidation = true`

Recommended: keep secrets out of committed `appsettings.json` and provide them via environment variables or secret management.

## Database Setup

1. Restore or create the AdventureWorks database (backup file available at `backup/AdventureWorks2025.bak`).
2. For persisted chat sessions in the console app, create the `SessionPersistence` database and run:

```sql
scripts/create-session-table.sql
```

## Build

```bash
dotnet restore
dotnet build -c Release
```

## Run

### Console app

```bash
dotnet run --project src/MafDb.ConsoleApp
```

Behavior:

- Prompts for an existing session ID or starts a new one.
- Saves session state after each turn.
- Supports `quit`/`exit` to stop.

### MCP server

```bash
dotnet run --project src/MafDb.McpServer
```

The server runs over stdio transport and exposes `AskDatabaseAgent`.

## Runtime Workflow

Default runtime (`Memory:Mode=StateGraph`, `Workflow:Mode=Deterministic`) uses:

1. Load persisted conversation state
2. Load/refresh session schema cache
3. Classify intent and select relevant memory context
4. Generate structured SQL plan (JSON)
5. Validate SQL (`SELECT`/`WITH` only, no multi-statement or mutating keywords)
6. Execute SQL
7. Retry repair loop on SQL errors (max 3)
8. Compose natural-language answer
9. Persist updated state/diagnostics

Compatibility mode (`Memory:Mode=FullHistory` or `Workflow:Mode=ToolCalling`) uses legacy full-history tool-calling behavior.

## Safety Notes

- SQL execution is read-only by design.
- Deterministic SQL validator rejects non-`SELECT`/`WITH`, multi-statement, and mutating SQL.
- Query execution uses rollback transaction semantics.
- Result output is capped at 100 rows.

## Quality Gates

Run before completing changes:

```bash
dotnet restore
dotnet build -c Release
dotnet test -c Release --no-build
dotnet format --verify-no-changes
```

## Repository Layout

- `scripts/` - utility SQL scripts
- `backup/` - sample AdventureWorks backup
- `tests/` - workflow and guardrail tests
- `MafDb.slnx` - solution entry point

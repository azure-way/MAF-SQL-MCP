# MafDb

A .NET 10 sample that uses the Microsoft Agentic Framework to query a SQL Server database (AdventureWorks) through a natural-language agent.

## Projects

- `src/MafDb.Core` - agent construction, SQL tools, and session persistence abstractions.
- `src/MafDb.ConsoleApp` - interactive CLI chat with persisted sessions.
- `src/MafDb.McpServer` - MCP server exposing the database agent as a tool over stdio.

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

## Safety Notes

- SQL execution is intended for read-only usage.
- The agent instructions restrict generated SQL to `SELECT` statements.
- Query execution is wrapped in a transaction that is rolled back.
- Tool output is limited to 100 rows.

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
- `MafDb.slnx` - solution entry point

# agents.md  
> Instructions for Codex (and other AI coding agents) contributing to this repository.  
  
## 1) Mission  
  
Build and maintain a production-quality **.NET 10** application using the **Microsoft Agentic Framework** with a focus on:  
  
1. Correctness  
2. Security  
3. Maintainability  
4. Observability  
5. Performance  
  
When in doubt, prefer safe and simple solutions.  
  
---  
  
## 2) Agent Operating Rules (Codex)  
  
- Read existing code patterns before introducing new patterns.  
- Keep changes **small and focused**.  
- Do not refactor unrelated areas.  
- Preserve backward compatibility unless explicitly asked to break it.  
- If requirements are ambiguous, implement the safest option and note assumptions.  
  
---  
  
## 3) Tech Baseline  
  
- **SDK**: .NET 10  
- **Language**: C# (latest supported by repo settings)  
- **App style**: ASP.NET Core + Dependency Injection + Options pattern  
- **Agent stack**: Microsoft Agentic Framework (plus Microsoft.Extensions.* abstractions where present)  
- **Testing**: xUnit (or existing test framework in repo)  
- **Observability**: ILogger + OpenTelemetry (if configured)  
  
---  
  
## 4) Architecture Conventions  
  
Prefer clean boundaries:  
  
- `Domain` → pure business logic, no model/provider SDK dependencies  
- `Application` → orchestration/use-cases  
- `Infrastructure` → external systems (LLM providers, DB, queues, APIs)  
- `Api`/`Host` → transport layer and DI wiring  
- `Agents` → agent definitions, tools, memory, orchestration flows  
  
If repository structure differs, follow existing structure while preserving these boundaries.  
  
---  
  
## 5) Coding Standards  
  
- Use async/await for I/O; avoid `.Result` / `.Wait()`.  
- Propagate `CancellationToken` on async public/internal APIs.  
- Use nullable reference types correctly; avoid `!` unless justified.  
- Prefer small methods and explicit naming.  
- Use structured logging:  
  - ✅ `logger.LogInformation("Processed {OrderId}", orderId);`  
  - ❌ string concatenation for logs  
- Avoid hardcoded configuration values; use options/config providers.  
  
---  
  
## 6) Microsoft Agentic Framework Guidelines  
  
- Keep prompts/templates versioned (prefer files over giant inline literals).  
- Use strongly typed contracts for tool input/output.  
- Validate tool inputs before execution.  
- Add timeout/retry/circuit-breaker behavior around model/tool calls.  
- Keep provider-specific logic behind interfaces/adapters.  
- Add correlation IDs and context for each agent run.  
- Require explicit confirmation/guardrails for destructive actions.  
- Never allow tools to execute unrestricted commands.  
  
---  
  
## 7) Testing Expectations  
  
For non-trivial changes, update/add tests:  
  
- **Unit tests**: business rules, tools, validators, planners  
- **Integration tests**: orchestration flow + DI wiring  
- **No live LLM dependency in CI tests** (use fake/mocked clients)  
- Keep tests deterministic (stable inputs/outputs)  
  
---  
  
## 8) Quality Gates (run before completion)  
  
```bash  
dotnet restore  
dotnet build -c Release  
dotnet test -c Release --no-build  
dotnet format --verify-no-changes  
```  
  
If any command cannot be run, state why in the final summary.  
  
---  
  
## 9) Dependency Policy  
  
- Prefer built-in .NET/Microsoft packages first.  
- Do not add new dependencies unless necessary.  
- If adding packages:  
  - Explain why  
  - Use consistent versioning strategy  
  - Avoid duplicate libraries for the same concern  
  
---  
  
## 10) Security & Compliance  
  
- Never commit secrets/tokens/keys.  
- Use secure configuration and secret stores.  
- Redact sensitive values from logs.  
- Apply least-privilege for external APIs/tools.  
- Validate all external input.  
  
---  
  
## 11) Done Criteria for Codex Tasks  
  
A task is complete only when:  
  
- [ ] Code compiles  
- [ ] Tests pass (or documented why not runnable)  
- [ ] Formatting/linting passes  
- [ ] Behavior changes are covered by tests  
- [ ] Docs/comments updated if needed  
- [ ] Final summary includes:  
  - what changed  
  - why it changed  
  - risks/assumptions  
  - follow-up items (if any)  
  
---  
  
## 12) Preferred Response Format for Codex  
  
When finishing a coding task, provide:  
  
1. **Plan**  
2. **Files changed**  
3. **Key implementation notes**  
4. **Commands/tests run**  
5. **Assumptions and next steps**  
  

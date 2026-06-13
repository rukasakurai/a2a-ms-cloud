---
argument-hint: Describe the A2A + Foundry .NET task (host an endpoint, call one over the wire, pick package versions)
description: Implement genuine protocol-level Agent2Agent (A2A) v1.0 in .NET with Microsoft Foundry — host a Foundry agent behind a real A2A endpoint (agent card + JSON-RPC) and call it over the wire — without confusing it with in-process Agent Framework function composition. Use when wiring A2A in C#, choosing aligned A2A/Foundry package versions, or being precise about the A2A protocol version.
name: foundry-a2a-dotnet
---
# Microsoft Foundry A2A in .NET

## When to Use

- You are writing C#/.NET that uses the **Agent2Agent (A2A)** protocol with **Microsoft Foundry** (Foundry Agent Service / Microsoft Agent Framework).
- You need to **host** a Foundry agent behind a real A2A endpoint, or **call** a remote A2A agent over the wire from another agent.
- You must pick **aligned package versions** for A2A + Foundry, or be precise about **which A2A protocol version** ("A2A" is ambiguous after breaking changes).
- You are tempted to call `agent.AsAIFunction()` "A2A" — it is **not** (see below).

## Core Rules (verified)

1. **Be explicit about the A2A protocol version.** A2A had breaking changes; `a2aproject/A2A` **v1.0.0** (2026-03-12) changed the agent-card shape vs v0.3. Target **v1.0** (advertised as `protocolVersion: "1.0"`, not `"1.0.0"`). New integrations should target v1.0.

2. **`AsAIFunction()` is NOT A2A.** Attaching a Foundry `AIAgent` directly as a tool via `weatherAgent.AsAIFunction()` is **in-process function composition** — no agent card, no connection, no endpoint, nothing on the wire. Genuine A2A requires an agent **card + transport (HTTP/JSON-RPC)**. Only call it "A2A" when a card is discovered and the call crosses the wire.

3. **Use an aligned package set, or restore breaks.** The A2A bridge has versions whose dependencies are mutually exclusive with Foundry 1.5.0. Verified-good set (see `references/package-matrix.md`):
   - `Microsoft.Agents.AI.Foundry` `1.5.0`
   - `Microsoft.Agents.AI.A2A` `1.5.0-preview.260507.1` (→ Abstractions `1.5.0` + `A2A 1.0.0-preview2`)
   - `A2A` + `A2A.AspNetCore` `1.0.0-preview2`
   - The older bridge `1.0.0-preview.251114.1` pulls Abstractions `1.0.0-preview` + `A2A 0.3.3` and will **not** align with Foundry 1.5.0.

4. **To self-host an A2A endpoint from a console app**, target `Microsoft.NET.Sdk` and add `<FrameworkReference Include="Microsoft.AspNetCore.App" />`. Set the bind URL with `app.Urls.Add(url)` (the `WebApplicationBuilder.WebHost.UseUrls(...)` extension is not available in this minimal setup).

5. **Verify the API by reflection, not web search.** General web/LLM search hallucinates this API (e.g. invents `AddAgentHost` / `MapA2A<TInterface,TImpl>`). The real surface is small and is documented in `references/api-surface.md`; confirm against the actual assemblies before coding.

## Minimal pattern

**Server (expose a Foundry agent over A2A):** implement `A2A.IAgentHandler`, build an `A2A.AgentCard`, register with `services.AddA2AAgent<THandler>(card)`, then `app.MapA2A(path)` + `app.MapWellKnownAgentCard(card)`. Reply with `new MessageResponder(eventQueue, ctx.ContextId).ReplyAsync(text)`; read input from `ctx.UserText`.

**Client (call it over the wire):** `new A2ACardResolver(uri, http).GetAgentCardAsync()` for discovery, then `resolver.GetAIAgentAsync(http)` to get an `AIAgent` whose invocations travel over A2A. Attach it as a tool with `.AsAIFunction()` to make another agent delegate via A2A.

Full signatures, the exact emitted agent-card JSON, and Foundry version-negotiation details are in `references/`.

## Foundry-specific gotchas

- **Foundry serves both v1.0 and v0.3 and defaults to v0.3.** When calling a *Foundry-native* A2A endpoint, negotiate v1.0 with the `A2A-Version: 1.0` header, `?a2a-version=1.0`, or by fetching the v1.0 card (`…/agentCard/v1.0`). See `references/agent-card-v1.md`.
- **`A2A.AgentSkill` collides with `Microsoft.Agents.AI.AgentSkill`.** Fully qualify `new A2A.AgentSkill { … }` when both namespaces are in scope.
- Foundry's `AIAgent` surfaces `CreateSessionAsync()` → `AgentSession` and `RunAsync(string, session)` → `AgentResponse` (use `.Text`).

## References

- `references/package-matrix.md` — exact package versions and why they align (or don't).
- `references/api-surface.md` — verified server + client API signatures.
- `references/agent-card-v1.md` — the v1.0 agent-card JSON shape and Foundry version negotiation.
- [A2A protocol](https://a2a-protocol.org/latest/) · [a2aproject/A2A releases](https://github.com/a2aproject/A2A/releases) · [A2A .NET SDK](https://github.com/a2aproject/a2a-dotnet)
- [Connect to an A2A endpoint (Foundry)](https://learn.microsoft.com/azure/foundry/agents/how-to/tools/agent-to-agent) · [Enable incoming A2A (Foundry)](https://learn.microsoft.com/azure/foundry/agents/how-to/enable-agent-to-agent-endpoint)

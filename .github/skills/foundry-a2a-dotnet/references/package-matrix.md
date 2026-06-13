# A2A + Foundry .NET package matrix

Verified by `dotnet restore` + `dotnet build` on .NET 10 (2026-06).

## Verified-good set (target A2A protocol v1.0)

| Package | Version | Role |
| --- | --- | --- |
| `Microsoft.Agents.AI.Foundry` | `1.5.0` | Foundry provider for Microsoft Agent Framework. Transitively pulls `Azure.AI.Projects 2.0.0`, `Microsoft.Extensions.AI 10.5.1`, `Microsoft.Agents.AI.Abstractions 1.5.0`. |
| `Microsoft.Agents.AI.A2A` | `1.5.0-preview.260507.1` | Agent Framework ⇄ A2A bridge (turns a remote A2A card into an `AIAgent`). Depends on `Abstractions 1.5.0` + `A2A 1.0.0-preview2`. |
| `A2A` | `1.0.0-preview2` | Open A2A protocol implementation (client + server), v1.0. |
| `A2A.AspNetCore` | `1.0.0-preview2` | ASP.NET Core hosting integration (`AddA2AAgent`, `MapA2A`, `MapWellKnownAgentCard`). |

Plus, to self-host the endpoint from a console exe:

```xml
<ItemGroup>
  <FrameworkReference Include="Microsoft.AspNetCore.App" />
</ItemGroup>
```

## Why this set aligns

`Microsoft.Agents.AI.Foundry 1.5.0` is built on `Microsoft.Agents.AI.Abstractions 1.5.0`.
The A2A bridge must depend on the **same** Abstractions version:

- `Microsoft.Agents.AI.A2A 1.5.0-preview.260507.1` → `Abstractions 1.5.0` + `A2A 1.0.0-preview2` ✅
- `Microsoft.Agents.AI.A2A 1.0.0-preview.251114.1` → `Abstractions 1.0.0-preview.251114.1` + `A2A 0.3.3-preview` ❌
  (incompatible Abstractions **and** an older A2A protocol version)

## A2A protocol version vs package version (don't conflate)

Three independent "versions" exist:

1. **A2A protocol spec** — `a2aproject/A2A` releases. **v1.0.0** (2026-03-12), latest **v1.0.1**; breaking vs v0.3. This is the value in the agent card's `protocolVersion` (`"1.0"` / `"0.3"`).
2. **A2A .NET SDK** — the `A2A` / `A2A.AspNetCore` NuGet package (`1.0.0-preview2`, implements protocol v1.0).
3. **Foundry / bridge** — `Microsoft.Agents.AI.A2A` package version (`1.5.0-preview…`), which in turn pins an `A2A` SDK version.

When you say "A2A vN" in code or docs, specify **which** of these you mean. This repo targets **protocol v1.0**.

## Finding newer aligned versions

If Foundry bumps to a new Abstractions version, find the matching bridge:

```bash
# list bridge versions
curl -s https://api.nuget.org/v3-flatcontainer/microsoft.agents.ai.a2a/index.json

# inspect a candidate's dependencies (Abstractions + A2A must match Foundry)
curl -s https://api.nuget.org/v3-flatcontainer/microsoft.agents.ai.a2a/<version>/microsoft.agents.ai.a2a.nuspec \
  | grep -iE 'dependency id="(Microsoft.Agents.AI.Abstractions|A2A)"'
```

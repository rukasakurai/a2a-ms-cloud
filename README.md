# a2a-ms-cloud

**Intent.** Demonstrate agent-to-agent (A2A) on the **Microsoft Cloud**
(including Azure) by making the Microsoft Foundry Agent Service A2A
public-preview announcement concrete — so the capability can be understood by
running it rather than only reading about it. Concretely, that means both
Foundry **agent shapes from the announcement** — a **Prompt agent** and a
**Hosted agent** — reachable over **A2A protocol v1.0** with the **cloud**
serving the endpoint (agent card + responses protocol), and agents calling each
other **over the wire**.

> Microsoft Foundry Agent Service adds agent-to-agent (A2A) communication in
> public preview for Prompt agents and Hosted agents that use the responses
> protocol.
> — [Azure Updates](https://azure.microsoft.com/en-us/updates?id=563716)

> [!WARNING]
> **Work in progress — does not yet satisfy the intent.** Today the A2A
> endpoints are **self-hosted by the local console app on `localhost`**, bridging
> to Foundry-hosted agents; the **cloud does not serve the A2A endpoint**. So the
> sample exercises A2A *semantics* against cloud-hosted agents, but not the
> Foundry-native cloud-served A2A endpoint the announcement introduces. Tracking:
> [#9](https://github.com/rukasakurai/a2a-ms-cloud/issues/9). The sections below
> describe the **current** localhost-based implementation.

## A2A protocol version

A2A has had breaking changes across versions, so this repo is **explicit about
the version it targets** rather than saying "A2A" ambiguously.

- **Target: A2A protocol `v1.0`** — the open standard
  [a2aproject/A2A v1.0.0](https://github.com/a2aproject/A2A/releases) (released
  2026-03-12), advertised as `protocolVersion: "1.0"` in the agent card.
- The version is pinned in code in
  [`src/A2aDemo/A2aProtocol.cs`](src/A2aDemo/A2aProtocol.cs) and surfaced in the
  console output and the published agent card.

This aligns with the libraries and platform:

| Component | Version | A2A protocol |
| --- | --- | --- |
| `A2A` / `A2A.AspNetCore` (open A2A .NET SDK) | `1.0.0-preview2` | v1.0 |
| `Microsoft.Agents.AI.A2A` (Foundry ⇄ A2A bridge) | `1.5.0-preview.260507.1` | v1.0 (depends on `A2A 1.0.0-preview2`) |
| Microsoft Foundry Agent Service | public preview | v1.0 **and** v0.3 |

> **Calling a Foundry-native A2A endpoint?** Foundry serves both v1.0 and v0.3 on
> the same path and **defaults to v0.3** unless the client negotiates v1.0 — via
> the `A2A-Version: 1.0` header, `?a2a-version=1.0`, or by fetching the v1.0 agent
> card so the client SDK negotiates automatically. New integrations should target
> v1.0. See
> [Enable incoming A2A on a Foundry agent](https://learn.microsoft.com/azure/foundry/agents/how-to/enable-agent-to-agent-endpoint#a2a-protocol-versions).
> This sample hosts its own v1.0 endpoint with the A2A .NET SDK, so the whole run
> is v1.0 end to end.

## What the announcement means

Foundry Agent Service can now have one agent call another agent over the open
[A2A protocol](https://a2a-protocol.org/latest/) using a standardized
request/response (the **responses protocol**) instead of bespoke, point-to-point
integrations. The two agent shapes in the announcement are:

- **Prompt agents** — server-side agents created in your Foundry project (an
  agent name plus versions). They speak the responses protocol by default and can
  be exposed as A2A endpoints.
- **Hosted agents** — agents you build (for example with the
  [Microsoft Agent Framework](https://github.com/microsoft/agent-framework)) and
  deploy to Foundry, where they run as a managed, server-side resource and can be
  exposed over A2A if they implement the responses protocol.

Two related capabilities ship under the A2A umbrella:

1. **Call a remote A2A endpoint (the A2A tool).** Agent A calls Agent B through
   an A2A connection. Agent B's answer goes back to Agent A, and Agent A stays in
   control of the conversation and summarizes the result for the user. This is
   the documented replacement for the classic *Connected Agents* tool.
2. **Expose your agent as an A2A endpoint** so other A2A clients can call it.

The defining behavior in both cases is **call-and-return**: the caller delegates
a sub-task, receives the callee's answer, and remains responsible for the user
dialogue. (A multi-agent *workflow* is different — there control is handed off
entirely to the other agent.)

## What this repo implements

A **.NET 10** console app (`src/A2aDemo`) that demonstrates **protocol-level A2A**
(not just similar in-process semantics):

- `WeatherPromptAgent` — a **Prompt agent**: a server-side, project-hosted
  (declarative) agent created and versioned through the
  `AgentAdministrationClient` (`DeclarativeAgentDefinition`). It answers weather
  questions and is **published behind a genuine A2A endpoint** using the open A2A
  .NET SDK: an agent card is served at `/.well-known/agent-card.json` (discovery)
  and a JSON-RPC endpoint at `/weather`. The shared
  [`FoundryAgentA2AHandler`](src/A2aDemo/FoundryAgentA2AHandler.cs) implements
  A2A's `IAgentHandler` and runs the Foundry agent for each inbound A2A message.
  Here the Prompt agent acts as an **A2A server**.
- `CoordinatorAgent` — an in-process Agent Framework agent that **delegates to the
  specialist by calling its A2A endpoint**. The agent card is resolved into an
  `AIAgent` with the A2A bridge (`A2ACardResolver.GetAIAgentAsync`), so the
  coordinator's tool call travels over HTTP + JSON-RPC — real A2A, not in-process
  composition. Here the coordinator acts as an **A2A client**.
- `HostedConciergeAgent` — a **Hosted agent**: one you **build in code** with the
  Microsoft Agent Framework (a model deployment plus a **local C# tool**,
  `PackingTips`), as opposed to the declarative Prompt agent. It demonstrates the
  Hosted agent in **both A2A roles at once**:
  - **A2A client** — it is given the weather-over-A2A tool, so when it runs it
    itself calls `WeatherPromptAgent` over A2A to get the forecast.
  - **A2A server** — it is published behind its own A2A endpoint (`/concierge`,
    served by the same `FoundryAgentA2AHandler`), so the top-level caller discovers
    its card and invokes it over the wire.

  A single call exercises a **two-hop A2A chain**:
  `caller → (A2A) → HostedConciergeAgent → (A2A) → WeatherPromptAgent`, with the
  Hosted agent in the middle acting as server (to the caller) and client (to the
  weather specialist), then folding in packing advice from its local tool.

A single run proves protocol-level A2A: **agent-card discovery** followed by
**JSON-RPC calls between agents**, including the Hosted agent acting as both an
A2A client and an A2A server. For comparison, the same run also performs the
**in-process** path (`weatherAgent.AsAIFunction()`) and labels it clearly as
**NOT A2A** — it only mimics the call-and-return semantics locally, with no agent
card, A2A connection, or endpoint. (That in-process path is what earlier versions
of this sample did and previously mislabeled as "A2A".)

The Microsoft Foundry resources are provisioned with **Bicep** and **Azure
Developer CLI (azd)**. Both A2A endpoints are self-hosted by the app on localhost,
so no portal-created A2A connection is required to run the demo.

> **On the term "Hosted agent".** In the announcement, a Hosted agent is one you
> build in code (for example with the Microsoft Agent Framework) rather than a
> declarative Prompt agent. This sample demonstrates that **shape** —
> `HostedConciergeAgent` is built in code with its own local tool — and, like the
> Prompt agent's endpoint, it is **self-hosted on localhost** for the demo rather
> than deployed as a Foundry-managed Hosted resource. The A2A protocol mechanics
> (agent card + JSON-RPC, both client and server roles) are identical either way.

## Repository layout

```
.
├── azure.yaml                  # azd configuration (Bicep infra, no hosted service)
├── infra/
│   ├── main.bicep              # subscription-scope: resource group + module + outputs
│   ├── resources.bicep         # Foundry account, project, model deployment, RBAC
│   └── main.parameters.json    # azd -> Bicep parameter mapping
└── src/
    └── A2aDemo/                # .NET 10 console app demonstrating A2A protocol v1.0
        ├── A2aDemo.csproj
        ├── A2aProtocol.cs          # pinned A2A protocol version + alignment notes
        ├── AgentCards.cs           # A2A agent cards for the Prompt and Hosted agents
        ├── LocalTools.cs           # local C# tool carried by the Hosted agent
        ├── FoundryAgentA2AHandler.cs  # A2A server: shared IAgentHandler for any agent
        └── Program.cs              # orchestration: host A2A endpoints, call over the wire
```

## How the demo flows

```
                 (1) create + bind                 (2) publish over A2A
  Foundry project ───────────────► WeatherPromptAgent ───────────────► A2A endpoint :5247
  (Prompt agent)                     (AIAgent)                          /.well-known/agent-card.json
                                                                        /weather (JSON-RPC)
                                                                              ▲
                                                  (3) discover card +         │ HTTP + JSON-RPC
                                                     call OVER A2A           │ (A2A protocol v1.0)
  user question ──► CoordinatorAgent ──► weatherOverA2A.AsAIFunction() ───────┘
                       (in-process)         (A2A-backed proxy)


  (4) Hosted agent as BOTH A2A server and A2A client (two-hop chain):

  caller ──discover card + call OVER A2A──►  HostedConciergeAgent  ──call OVER A2A──►  WeatherPromptAgent
  (A2A client)        :5248/concierge        (A2A server here,                          A2A endpoint :5247
                                              A2A client below)
                                                  │  + PackingTips (local C# tool)
                                                  └──────────────► combined reply (weather + packing)
```

## Infrastructure

`infra/resources.bicep` provisions the modern, agent-centric Foundry resource
model (provider `Microsoft.CognitiveServices`):

| Resource | ARM type | Notes |
| --- | --- | --- |
| Foundry account | `Microsoft.CognitiveServices/accounts` (`kind: AIServices`) | `allowProjectManagement: true`, Entra-only auth |
| Foundry project | `Microsoft.CognitiveServices/accounts/projects` | Agent host |
| Model deployment | `Microsoft.CognitiveServices/accounts/deployments` | `gpt-4.1-mini` (GlobalStandard, capacity `modelCapacity`, default 30K TPM) |
| Role assignment | `Microsoft.Authorization/roleAssignments` | **Azure AI User** for the deploying principal |

The Bicep outputs `AZURE_AI_PROJECT_ENDPOINT` and `AZURE_AI_MODEL_DEPLOYMENT_NAME`
are written to the azd environment and consumed by the app.

## Prerequisites

- An Azure subscription with access to Microsoft Foundry and the `gpt-4.1-mini`
  model in your chosen region.
- [Azure Developer CLI (azd)](https://learn.microsoft.com/azure/developer/azure-developer-cli/install-azd)
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)

## Deploy and run

```bash
# 1. Authenticate.
azd auth login

# 2. Provision the Foundry account, project, and model deployment.
azd up

# 3. Load the provisioned values into your shell.
eval "$(azd env get-values | sed 's/^/export /')"

# 4. Run the agent-to-agent demo.
dotnet run --project src/A2aDemo

# Ask a custom question:
dotnet run --project src/A2aDemo -- "Will I need an umbrella in Seattle?"
```

By default the app self-hosts the Prompt agent's A2A endpoint on
`http://127.0.0.1:5247` and the Hosted agent's on `http://127.0.0.1:5248`.
Override the ports with `A2A_LOCAL_PORT` and `A2A_HOSTED_PORT` if needed.

Expected output (text varies with the model):

```
Prompt agent     : WeatherPromptAgent (version 1)
...
WeatherPromptAgent is published over A2A (protocol v1.0) at http://127.0.0.1:5247
  agent card : http://127.0.0.1:5247/.well-known/agent-card.json
  JSON-RPC   : http://127.0.0.1:5247/weather

Discovered A2A agent card: WeatherPromptAgent (protocol v1.0, skills: weather_report)
CoordinatorAgent is delegating to WeatherPromptAgent over the A2A protocol (HTTP + JSON-RPC)...

CoordinatorAgent (via A2A): It's currently cloudy in Amsterdam with a high near 15°C.

HostedConciergeAgent is published over A2A (protocol v1.0) at http://127.0.0.1:5248
  agent card : http://127.0.0.1:5248/.well-known/agent-card.json
  JSON-RPC   : http://127.0.0.1:5248/concierge

Discovered A2A agent card: HostedConciergeAgent (protocol v1.0, skills: travel_concierge)
Calling HostedConciergeAgent over A2A; it in turn calls WeatherPromptAgent over A2A (caller -> concierge -> weather, all via HTTP + JSON-RPC)...

HostedConciergeAgent (via A2A, acting as both A2A server and A2A client): It's cloudy in Amsterdam with a high near 15°C — pack a waterproof jacket and a compact umbrella.

For comparison (NOT A2A): the same coordinator delegating via in-process function composition...

CoordinatorAgent (in-process, not A2A): It's currently cloudy in Amsterdam with a high near 15°C.
```

The app authenticates with `DefaultAzureCredential`, so the identity from
`azd auth login` / `az login` is used at runtime — no API keys are stored.

To remove all provisioned resources:

```bash
azd down
```

## Calling a Foundry-native A2A endpoint

This sample hosts its own A2A endpoint with the A2A .NET SDK. To instead call a
**Foundry-hosted** A2A endpoint (or expose a Foundry agent as one), enable
incoming A2A on the agent and/or create an A2A connection in your project, then
attach the A2A tool. Remember the **version negotiation** note above: target
`A2A-Version: 1.0` (or fetch the v1.0 agent card) so you get v1.0 rather than the
v0.3 default. See
[Connect to an A2A agent endpoint](https://learn.microsoft.com/azure/foundry/agents/how-to/tools/agent-to-agent)
and
[Enable incoming A2A on a Foundry agent](https://learn.microsoft.com/azure/foundry/agents/how-to/enable-agent-to-agent-endpoint).

## References

- [A2A protocol](https://a2a-protocol.org/latest/) · [a2aproject/A2A releases](https://github.com/a2aproject/A2A/releases)
- [A2A .NET SDK (a2aproject/a2a-dotnet)](https://github.com/a2aproject/a2a-dotnet)
- [Connect to an A2A agent endpoint from Foundry Agent Service](https://learn.microsoft.com/azure/foundry/agents/how-to/tools/agent-to-agent)
- [Enable incoming A2A on a Foundry agent](https://learn.microsoft.com/azure/foundry/agents/how-to/enable-agent-to-agent-endpoint)
- [Microsoft Agent Framework](https://github.com/microsoft/agent-framework)

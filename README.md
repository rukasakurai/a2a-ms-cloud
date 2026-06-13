# a2a-ms-cloud

A small, deployable implementation that makes the Microsoft Foundry Agent
Service **agent-to-agent (A2A)** public-preview announcement concrete by
demonstrating **in-process agent-as-function delegation** with the same
call-and-return semantics as the A2A tool, so the pattern can be understood
by running it rather than only reading about it.

> Microsoft Foundry Agent Service adds agent-to-agent (A2A) communication in
> public preview for Prompt agents and Hosted agents that use the responses
> protocol.
> — [Azure Updates](https://azure.microsoft.com/en-us/updates?id=563716)

## What the announcement means

Foundry Agent Service can now have one agent call another agent over the open
[A2A protocol](https://a2a-protocol.org/latest/) using a standardized
request/response (the **responses protocol**) instead of bespoke, point-to-point
integrations. The two agent shapes in the announcement are:

- **Prompt agents** — server-side agents created in your Foundry project (an
  agent name plus versions). They speak the responses protocol by default.
- **Hosted agents** — agents you build (for example with the
  [Microsoft Agent Framework](https://github.com/microsoft/agent-framework)) and
  deploy to Foundry, where they run as a managed, server-side resource and speak
  the responses protocol.

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

A **.NET 10** console app (`src/A2aDemo`) that demonstrates **Microsoft Agent
Framework agent-as-function delegation** with the same call-and-return semantics
as the A2A tool. It pairs a server-side Prompt agent (one of the announcement's
two agent shapes) with an in-process coordinator:

- `WeatherPromptAgent` — a **Prompt agent**: a server-side, project-hosted
  (declarative) agent created and versioned through the
  `AgentAdministrationClient` (`DeclarativeAgentDefinition`). It is persisted in
  the Foundry project until deleted and answers weather questions.
- `CoordinatorAgent` — a **Responses Agent**: composed in-process with the
  [Microsoft Agent Framework](https://github.com/microsoft/agent-framework). No
  server-side resource is created (so it is *not* the announcement's server-side
  Hosted agent shape); model, instructions, and tools are provided at runtime.
  The Prompt agent is attached as a tool via `weatherAgent.AsAIFunction()`, so the
  coordinator can call the specialist agent and summarize the reply — the same
  call-and-return shape the A2A tool models in Foundry Agent Service.

The single run exercises **agent-as-function delegation**: the in-process
Responses Agent coordinator delegates to the server-side Prompt
specialist and stays in control of the user dialogue. This demonstrates the same
call-and-return semantics as the remote A2A tool using **in-process function-tool
composition** — it does not implement protocol-level A2A (no A2A connection,
A2A tool, or A2A endpoint is used). The remote A2A path is summarized under
[Calling a remote A2A endpoint](#calling-a-remote-a2a-endpoint) below. The app
deletes the Prompt agent it creates on exit so repeated runs stay clean.

The Microsoft Foundry resources are provisioned with **Bicep** and **Azure
Developer CLI (azd)**.

## Repository layout

```
.
├── azure.yaml                  # azd configuration (Bicep infra, no hosted service)
├── infra/
│   ├── main.bicep              # subscription-scope: resource group + module + outputs
│   ├── resources.bicep         # Foundry account, project, model deployment, RBAC
│   └── main.parameters.json    # azd -> Bicep parameter mapping
└── src/
    └── A2aDemo/                # .NET 10 console app demonstrating agent-as-function delegation
        ├── A2aDemo.csproj
        └── Program.cs
```

## Infrastructure

`infra/resources.bicep` provisions the modern, agent-centric Foundry resource
model (provider `Microsoft.CognitiveServices`):

| Resource | ARM type | Notes |
| --- | --- | --- |
| Foundry account | `Microsoft.CognitiveServices/accounts` (`kind: AIServices`) | `allowProjectManagement: true`, Entra-only auth |
| Foundry project | `Microsoft.CognitiveServices/accounts/projects` | Agent host |
| Model deployment | `Microsoft.CognitiveServices/accounts/deployments` | `gpt-4.1-mini` (GlobalStandard) |
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

Expected output (text varies with the model):

```
Prompt agent     : WeatherPromptAgent (version 1)
...
CoordinatorAgent (Responses Agent) is delegating to WeatherPromptAgent (Prompt) via agent-as-function composition...

CoordinatorAgent: It's currently cloudy in Amsterdam with a high near 15°C.
```

The app authenticates with `DefaultAzureCredential`, so the identity from
`azd auth login` / `az login` is used at runtime — no API keys are stored.

To remove all provisioned resources:

```bash
azd down
```

## Calling a remote A2A endpoint

To use the **remote** A2A tool instead of in-process composition, create an A2A
connection in your Foundry project (Tools → Connect tool → Custom →
Agent2Agent) and attach an `A2APreviewTool` that references the connection ID
when you create the agent version. See
[Connect to an A2A agent endpoint from Foundry Agent Service](https://learn.microsoft.com/azure/foundry/agents/how-to/tools/agent-to-agent).

## References

- [Connect to an A2A agent endpoint from Foundry Agent Service](https://learn.microsoft.com/azure/foundry/agents/how-to/tools/agent-to-agent)
- [Enable incoming A2A on a Foundry agent](https://learn.microsoft.com/azure/foundry/agents/how-to/enable-agent-to-agent-endpoint)
- [A2A protocol](https://a2a-protocol.org/latest/)
- [Microsoft Agent Framework](https://github.com/microsoft/agent-framework)

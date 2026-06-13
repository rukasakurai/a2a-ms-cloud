# A2A v1.0 agent card + Foundry version negotiation

## v1.0 agent-card shape (what the SDK emits)

Built from `A2A.AgentCard` and served at `/.well-known/agent-card.json`. This is
the **actual JSON** emitted by `A2A 1.0.0-preview2` (captured at runtime):

```json
{
  "name": "WeatherPromptAgent",
  "description": "Specialist agent that answers weather questions.",
  "version": "1.0.0",
  "documentationUrl": null,
  "iconUrl": null,
  "supportedInterfaces": [
    { "url": "http://127.0.0.1:5247/weather", "protocolBinding": "JSONRPC", "tenant": null, "protocolVersion": "1.0" }
  ],
  "capabilities": { "streaming": false, "pushNotifications": null, "extensions": null, "extendedAgentCard": null },
  "provider": null,
  "skills": [
    { "id": "weather_report", "name": "Weather report", "description": "...", "tags": ["weather","forecast"], "examples": [...], "inputModes": null, "outputModes": null, "securityRequirements": null }
  ],
  "defaultInputModes": ["text/plain"],
  "defaultOutputModes": ["text/plain"],
  "securitySchemes": null,
  "securityRequirements": null,
  "signatures": null
}
```

### v1.0 vs v0.3 differences that matter

- **`supportedInterfaces[]`** with per-interface `protocolBinding` + `protocolVersion`
  replaces the old top-level `url` / `preferredTransport` / `protocolVersion`.
- **`extendedAgentCard`** moved under `capabilities` (was `supportsAuthenticatedExtendedCard`).
- `protocolVersion` value is **`"1.0"`** / **`"0.3"`** (string, not `"1.0.0"`).
  The card's `version` field is your *agent's* version and is unrelated to the protocol version.

### Building the card (note the namespace collision)

```csharp
var card = new A2A.AgentCard
{
    Name = "WeatherPromptAgent",
    Version = "1.0.0",
    DefaultInputModes = ["text/plain"],
    DefaultOutputModes = ["text/plain"],
    Capabilities = new AgentCapabilities { Streaming = false },
    SupportedInterfaces =
    [
        new AgentInterface { Url = jsonRpcUrl, ProtocolBinding = "JSONRPC", ProtocolVersion = "1.0" },
    ],
    Skills =
    [
        // Fully qualify: A2A.AgentSkill collides with Microsoft.Agents.AI.AgentSkill.
        new A2A.AgentSkill { Id = "weather_report", Name = "Weather report", Description = "...", Tags = ["weather"] },
    ],
};
```

`A2A.ProtocolBindingNames` constants: `JsonRpc = "JSONRPC"`, `HttpJson = "HTTP+JSON"`, `Grpc = "GRPC"`.

## Foundry version negotiation (calling a Foundry-native A2A endpoint)

Foundry serves **both v1.0 and v0.3** on the same base path
(`…/endpoint/protocols/a2a`) and **defaults to v0.3** if the client doesn't
negotiate. Three ways to select v1.0:

1. **Agent-card discovery (recommended):** fetch the v1.0 card at `…/agentCard/v1.0`
   (v0.3 at `…/agentCard/v0.3`). Most A2A client SDKs then negotiate from the card's
   `protocolVersion` automatically.
2. **HTTP header:** `A2A-Version: 1.0`.
3. **Query string:** `?a2a-version=1.0`.

> If none is specified, Foundry serves **v0.3 by default**. Always negotiate v1.0
> explicitly for new integrations.

### Enabling incoming A2A on a Foundry agent

Not yet in the portal; use REST `PATCH …/agents/{name}?api-version=v1` (or Python SDK)
to set the `agent_card` and add `"a2a"` to `agent_endpoint.protocols`
(alongside `"responses"`). Setting the agent card via the .NET/Python SDK isn't
supported yet — use REST for the card.

Source: <https://learn.microsoft.com/azure/foundry/agents/how-to/enable-agent-to-agent-endpoint>

# A2A .NET API surface (verified)

Confirmed by reflecting over the actual assemblies and by a localhost runtime
round-trip (server hosting + card discovery + JSON-RPC call). General web/LLM
search **hallucinates** this API (e.g. `AddAgentHost`, `MapA2A<TInterface,TImpl>`,
`AsA2A<T>()`) — none of those exist. Trust reflection over search.

## Server — host an agent over A2A

Namespaces: `A2A` (core), `A2A.AspNetCore` (hosting).

### Implement `A2A.IAgentHandler`

```csharp
internal sealed class WeatherAgentHandler(AIAgent agent) : IAgentHandler
{
    public async Task ExecuteAsync(
        RequestContext context, AgentEventQueue eventQueue, CancellationToken ct)
    {
        var responder = new MessageResponder(eventQueue, context.ContextId);
        AgentSession session = await agent.CreateSessionAsync();
        AgentResponse response = await agent.RunAsync(context.UserText ?? string.Empty, session);
        await responder.ReplyAsync(response.Text ?? string.Empty, ct);
    }

    public Task CancelAsync(
        RequestContext context, AgentEventQueue eventQueue, CancellationToken ct)
        => Task.CompletedTask;
}
```

- Read user input from `context.UserText` (string).
- Reply with `MessageResponder(eventQueue, contextId).ReplyAsync(string|List<Part>, ct)`.
- For task-style (long-running) work use `TaskUpdater` instead of `MessageResponder`.

### Register + map

```csharp
var b = WebApplication.CreateBuilder();
b.Services.AddSingleton<AIAgent>(weatherAgent);          // handler ctor deps via DI
b.Services.AddA2AAgent<WeatherAgentHandler>(agentCard);  // generic over THandler
var app = b.Build();
app.Urls.Add("http://127.0.0.1:5247");                   // NOT WebHost.UseUrls(...)
app.MapA2A("/weather");                                   // JSON-RPC endpoint
app.MapWellKnownAgentCard(agentCard);                     // /.well-known/agent-card.json
await app.StartAsync();
```

Relevant extension signatures (`A2A.AspNetCore`):

```
IServiceCollection AddA2AAgent<THandler>(this IServiceCollection, AgentCard, Action<A2AServerOptions>? = null)
IEndpointConventionBuilder MapA2A(this IEndpointRouteBuilder, string path)            // JSON-RPC
IEndpointConventionBuilder MapHttpA2A(this IEndpointRouteBuilder, IA2ARequestHandler, AgentCard, string? path = null)  // HTTP REST
IEndpointConventionBuilder MapWellKnownAgentCard(this IEndpointRouteBuilder, AgentCard, string? path = null)
```

## Client — call an A2A agent over the wire

Namespace `A2A` (core) for discovery; `Microsoft.Agents.AI.A2A` provides the
`AIAgent` bridge extensions (also surfaced under namespace `A2A`).

```csharp
using var http = new HttpClient();
var resolver = new A2ACardResolver(new Uri(baseUrl), http);

AgentCard card = await resolver.GetAgentCardAsync();          // discovery
AIAgent overA2A = await resolver.GetAIAgentAsync(http);       // bridge -> AIAgent over the wire

// Make another agent delegate via A2A by attaching the proxy as a tool:
AIAgent coordinator = projectClient.AsAIAgent(
    deploymentName, instructions: "...", name: "CoordinatorAgent",
    tools: [overA2A.AsAIFunction()]);
```

Bridge extension signatures (`Microsoft.Agents.AI.A2A`):

```
Task<AIAgent> GetAIAgentAsync(this A2ACardResolver, HttpClient? = null, ILoggerFactory? = null, CancellationToken = default)
AIAgent       GetAIAgent(this A2AClient, string? id=null, string? name=null, string? description=null, string? displayName=null, ILoggerFactory? = null)
AIAgent       GetAIAgent(this AgentCard, HttpClient? = null, ILoggerFactory? = null)
```

Raw client (no Agent Framework) if you need it:

```
new A2AClient(Uri baseUrl, HttpClient? = null)
Task<SendMessageResponse> SendMessageAsync(SendMessageRequest, CancellationToken = default)
// + A2AClientExtensions.SendMessageAsync(this IA2AClient, string text, Role, string contextId, CancellationToken)
```

## AgentResponse / AIAgent (Foundry layer)

```
AIAgent.CreateSessionAsync() -> AgentSession
AIAgent.RunAsync(string message, AgentSession? session = null, ...) -> AgentResponse
AgentResponse.Text            // string answer
AIAgentExtensions.AsAIFunction(this AIAgent, AIFunctionFactoryOptions? = null, AgentSession? = null) -> AIFunction
```

## Reflect it yourself

```csharp
// load the package DLLs by path, then:
foreach (var t in asm.GetExportedTypes().OrderBy(t => t.FullName)) { /* print methods/props */ }
```

DLL locations:
`~/.nuget/packages/a2a/1.0.0-preview2/lib/net10.0/A2A.dll`,
`~/.nuget/packages/a2a.aspnetcore/1.0.0-preview2/lib/net10.0/A2A.AspNetCore.dll`,
`~/.nuget/packages/microsoft.agents.ai.a2a/1.5.0-preview.260507.1/lib/net9.0/Microsoft.Agents.AI.A2A.dll`.

using A2A;
using A2A.AspNetCore;
using A2aDemo;
using Azure.AI.Projects;
using Azure.AI.Projects.Agents;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

// =============================================================================
// Microsoft Foundry Agent Service - agent-to-agent (A2A) demo (.NET 10)
//
// This sample demonstrates the *open* A2A standard end to end on Azure Foundry.
// It targets **A2A protocol v1.0** (see A2aProtocol.cs for version/alignment notes).
// A server-side Foundry Prompt agent is published behind a genuine A2A endpoint
// (agent card + HTTP/JSON-RPC), and a coordinator then calls it OVER THE A2A
// PROTOCOL rather than through in-process composition.
//
//   * WeatherPromptAgent  - a *Prompt agent*: a server-side agent created in the
//                           Foundry project via AgentAdministrationClient. It is
//                           published behind an A2A endpoint (agent card served at
//                           /.well-known, JSON-RPC at /weather) using the A2A.NET
//                           SDK, so other A2A clients can discover and call it.
//                           Here the Prompt agent acts as an A2A *server*.
//   * CoordinatorAgent    - an in-process Agent Framework agent that delegates to
//                           the specialist by calling its *A2A endpoint*. The A2A
//                           card is resolved into an AIAgent with the A2A bridge
//                           (resolver.GetAIAgentAsync), so the coordinator's tool
//                           call travels over the A2A wire (acts as an A2A client).
//   * HostedConciergeAgent - a *Hosted agent*: one you BUILD in code with the
//                           Agent Framework (a model deployment plus a LOCAL C#
//                           tool), as opposed to the declarative Prompt agent. It
//                           is given the weather-over-A2A tool, so it ITSELF calls
//                           the Prompt agent over A2A (A2A *client*), and it is
//                           also published behind its own A2A endpoint (/concierge)
//                           so the caller invokes it over the wire (A2A *server*).
//                           One call exercises a two-hop chain:
//                           caller -> (A2A) -> HostedConciergeAgent -> (A2A) -> WeatherPromptAgent.
//
// The single run proves protocol-level A2A: agent-card discovery + JSON-RPC calls
// between agents, with the Hosted agent acting as both an A2A client and an A2A
// server. For comparison, the run also shows the *in-process* function-composition
// path (weatherAgent.AsAIFunction()) and labels it clearly as NOT A2A - it only
// mimics the call-and-return semantics locally.
// See README.md for the mapping to the announcement and the open standard.
// =============================================================================

string endpoint = Environment.GetEnvironmentVariable("AZURE_AI_PROJECT_ENDPOINT")
    ?? throw new InvalidOperationException(
        "AZURE_AI_PROJECT_ENDPOINT is not set. Run 'azd up' (or 'azd provision') to "
        + "provision the Foundry project, then load the values with 'azd env get-values'.");
string deploymentName = Environment.GetEnvironmentVariable("AZURE_AI_MODEL_DEPLOYMENT_NAME")
    ?? "gpt-4.1-mini";

// The question can be supplied as a command-line argument; otherwise a default
// is used that forces the coordinator to consult the specialist agent.
string question = args.Length > 0
    ? string.Join(' ', args)
    : "What is the weather like in Amsterdam?";

// Local address for the self-hosted A2A endpoint (override with A2A_LOCAL_PORT).
int localPort = int.TryParse(Environment.GetEnvironmentVariable("A2A_LOCAL_PORT"), out int p)
    ? p
    : 5247;
string a2aBaseUrl = $"http://127.0.0.1:{localPort}";
const string a2aAgentPath = "/weather";

// The Hosted agent is published on a second self-hosted endpoint (override with
// A2A_HOSTED_PORT), so the two agents are discovered and called independently.
int hostedPort = int.TryParse(Environment.GetEnvironmentVariable("A2A_HOSTED_PORT"), out int hp)
    ? hp
    : localPort + 1;
string hostedBaseUrl = $"http://127.0.0.1:{hostedPort}";
const string hostedAgentPath = "/concierge";

// DefaultAzureCredential uses the identity from 'azd auth login' / 'az login'.
var credential = new DefaultAzureCredential();
var projectUri = new Uri(endpoint);
AIProjectClient projectClient = new(projectUri, credential);

// ---------------------------------------------------------------------------
// 1. Prompt agent (server-side): create a persistent WeatherAgent in the
//    Foundry project. A "Prompt agent" is the project-hosted (declarative)
//    agent shape from the announcement; it is created and versioned through the
//    AgentAdministrationClient and lives in the project until deleted.
// ---------------------------------------------------------------------------
const string weatherAgentName = "WeatherPromptAgent";

AgentAdministrationClient adminClient = projectClient.GetProjectAgentsClient(projectUri);

var weatherDefinition = new DeclarativeAgentDefinition(deploymentName)
{
    Instructions = "You are a meteorologist. Give a brief, friendly weather report for "
        + "the requested location. If you do not have live data, provide a plausible "
        + "seasonal estimate and say so.",
};

ProjectsAgentVersion weatherVersion = adminClient
    .CreateAgentVersion(
        weatherAgentName,
        new ProjectsAgentVersionCreationOptions(weatherDefinition)
        {
            Description = "Specialist Prompt agent that answers weather questions.",
        })
    .Value;

Console.WriteLine($"Project endpoint : {endpoint}");
Console.WriteLine($"Model deployment : {deploymentName}");
Console.WriteLine($"Prompt agent     : {weatherVersion.Name} (version {weatherVersion.Version})");
Console.WriteLine($"User question    : {question}");
Console.WriteLine();

WebApplication? a2aHost = null;
WebApplication? hostedHost = null;
try
{
    // Bind the server-side Prompt agent as an in-process AIAgent so it can power
    // the A2A endpoint and be reused for the in-process comparison below.
    AIAgent weatherAgent = projectClient.AsAIAgent(weatherVersion);

    // -----------------------------------------------------------------------
    // 2. Publish the Prompt agent behind a genuine A2A endpoint. The A2A.NET
    //    SDK hosts an agent card (discovery) plus a JSON-RPC endpoint; the
    //    handler runs the Foundry agent for each incoming A2A message.
    // -----------------------------------------------------------------------
    AgentCard weatherCard = AgentCards.Weather($"{a2aBaseUrl}{a2aAgentPath}");

    WebApplicationBuilder webBuilder = WebApplication.CreateBuilder();
    webBuilder.Logging.ClearProviders(); // keep the demo console output clean
    webBuilder.Services.AddSingleton<AIAgent>(weatherAgent);
    webBuilder.Services.AddA2AAgent<FoundryAgentA2AHandler>(weatherCard);

    a2aHost = webBuilder.Build();
    a2aHost.Urls.Add(a2aBaseUrl);
    a2aHost.MapA2A(a2aAgentPath);
    a2aHost.MapWellKnownAgentCard(weatherCard);
    await a2aHost.StartAsync();

    Console.WriteLine($"WeatherPromptAgent is published over A2A (protocol v{A2aProtocol.Version}) at {a2aBaseUrl}");
    Console.WriteLine($"  agent card : {a2aBaseUrl}/.well-known/agent-card.json");
    Console.WriteLine($"  JSON-RPC   : {a2aBaseUrl}{a2aAgentPath}");
    Console.WriteLine();

    // -----------------------------------------------------------------------
    // 3. A2A path: discover the agent card and call the specialist over the
    //    wire. resolver.GetAIAgentAsync turns the remote A2A agent into an
    //    AIAgent whose invocations travel over HTTP/JSON-RPC, so attaching it
    //    as a tool makes the coordinator delegate via the A2A protocol.
    // -----------------------------------------------------------------------
    using var httpClient = new HttpClient();
    var resolver = new A2ACardResolver(new Uri(a2aBaseUrl), httpClient);

    AgentCard discoveredCard = await resolver.GetAgentCardAsync();
    Console.WriteLine(
        $"Discovered A2A agent card: {discoveredCard.Name} "
        + $"(protocol v{discoveredCard.SupportedInterfaces[0].ProtocolVersion}, "
        + $"skills: {string.Join(", ", discoveredCard.Skills.Select(s => s.Id))})");

    AIAgent weatherOverA2A = await resolver.GetAIAgentAsync(httpClient);

    AIAgent coordinatorViaA2A = projectClient.AsAIAgent(
        deploymentName,
        instructions: $"You are a helpful coordinator. When asked about the weather, call the {weatherAgentName} tool and summarize its answer for the user.",
        name: "CoordinatorAgent",
        tools: [weatherOverA2A.AsAIFunction()]);

    Console.WriteLine("CoordinatorAgent is delegating to WeatherPromptAgent over the A2A protocol (HTTP + JSON-RPC)...");
    AgentSession a2aSession = await coordinatorViaA2A.CreateSessionAsync();
    AgentResponse a2aResponse = await coordinatorViaA2A.RunAsync(question, a2aSession);
    Console.WriteLine();
    Console.WriteLine($"CoordinatorAgent (via A2A): {a2aResponse.Text}");
    Console.WriteLine();

    // -----------------------------------------------------------------------
    // 4. Hosted agent in BOTH A2A roles. A "Hosted agent" is one you BUILD in
    //    code with the Agent Framework (here: a model deployment plus a LOCAL C#
    //    tool, PackingTips), as opposed to the declarative Prompt agent created
    //    above via AgentAdministrationClient. It is given the weather-over-A2A
    //    tool, so when it runs it ITSELF calls the Prompt agent over A2A (acting
    //    as an A2A *client*). It is also published behind its own A2A endpoint,
    //    so the top-level caller below invokes it over the wire (acting as an
    //    A2A *server*). One call therefore exercises a two-hop A2A chain:
    //    caller -> (A2A) -> HostedConciergeAgent -> (A2A) -> WeatherPromptAgent.
    // -----------------------------------------------------------------------
    AIAgent hostedConciergeAgent = projectClient.AsAIAgent(
        deploymentName,
        instructions: "You are a travel concierge. When asked about a trip, first call the "
            + $"{weatherAgentName} tool to get the weather, then call the PackingTips tool with "
            + "that weather, and reply with a short combined recommendation (weather + what to pack).",
        name: "HostedConciergeAgent",
        tools: [weatherOverA2A.AsAIFunction(), AIFunctionFactory.Create(LocalTools.PackingTips)]);

    AgentCard conciergeCard = AgentCards.Concierge($"{hostedBaseUrl}{hostedAgentPath}");

    WebApplicationBuilder hostedBuilder = WebApplication.CreateBuilder();
    hostedBuilder.Logging.ClearProviders();
    hostedBuilder.Services.AddSingleton<AIAgent>(hostedConciergeAgent);
    hostedBuilder.Services.AddA2AAgent<FoundryAgentA2AHandler>(conciergeCard);

    hostedHost = hostedBuilder.Build();
    hostedHost.Urls.Add(hostedBaseUrl);
    hostedHost.MapA2A(hostedAgentPath);
    hostedHost.MapWellKnownAgentCard(conciergeCard);
    await hostedHost.StartAsync();

    Console.WriteLine($"HostedConciergeAgent is published over A2A (protocol v{A2aProtocol.Version}) at {hostedBaseUrl}");
    Console.WriteLine($"  agent card : {hostedBaseUrl}/.well-known/agent-card.json");
    Console.WriteLine($"  JSON-RPC   : {hostedBaseUrl}{hostedAgentPath}");
    Console.WriteLine();

    var hostedResolver = new A2ACardResolver(new Uri(hostedBaseUrl), httpClient);
    AgentCard discoveredConciergeCard = await hostedResolver.GetAgentCardAsync();
    Console.WriteLine(
        $"Discovered A2A agent card: {discoveredConciergeCard.Name} "
        + $"(protocol v{discoveredConciergeCard.SupportedInterfaces[0].ProtocolVersion}, "
        + $"skills: {string.Join(", ", discoveredConciergeCard.Skills.Select(s => s.Id))})");

    AIAgent conciergeOverA2A = await hostedResolver.GetAIAgentAsync(httpClient);

    string tripQuestion = $"I'm planning a trip. {question} Also, what should I pack?";
    Console.WriteLine(
        "Calling HostedConciergeAgent over A2A; it in turn calls WeatherPromptAgent over A2A "
        + "(caller -> concierge -> weather, all via HTTP + JSON-RPC)...");
    AgentSession conciergeSession = await conciergeOverA2A.CreateSessionAsync();
    AgentResponse conciergeResponse = await conciergeOverA2A.RunAsync(tripQuestion, conciergeSession);
    Console.WriteLine();
    Console.WriteLine(
        "HostedConciergeAgent (via A2A, acting as both A2A server and A2A client): "
        + conciergeResponse.Text);
    Console.WriteLine();

    // -----------------------------------------------------------------------
    // 5. Comparison only - NOT A2A. The same call-and-return semantics can be
    //    achieved in-process by attaching the Foundry agent directly as a
    //    function tool (weatherAgent.AsAIFunction()). No agent card, no A2A
    //    connection, and no endpoint is involved here; the call never leaves
    //    the process. This is what the previous version of the sample did.
    // -----------------------------------------------------------------------
    AIAgent coordinatorInProcess = projectClient.AsAIAgent(
        deploymentName,
        instructions: $"You are a helpful coordinator. When asked about the weather, call the {weatherAgentName} tool and summarize its answer for the user.",
        name: "CoordinatorAgent",
        tools: [weatherAgent.AsAIFunction()]);

    Console.WriteLine("For comparison (NOT A2A): the same coordinator delegating via in-process function composition...");
    AgentSession inProcessSession = await coordinatorInProcess.CreateSessionAsync();
    AgentResponse inProcessResponse = await coordinatorInProcess.RunAsync(question, inProcessSession);
    Console.WriteLine();
    Console.WriteLine($"CoordinatorAgent (in-process, not A2A): {inProcessResponse.Text}");
}
finally
{
    // Stop the self-hosted A2A endpoints.
    if (hostedHost is not null)
    {
        await hostedHost.StopAsync();
        await hostedHost.DisposeAsync();
    }

    if (a2aHost is not null)
    {
        await a2aHost.StopAsync();
        await a2aHost.DisposeAsync();
    }

    // Clean up the server-side Prompt agent so repeated runs don't accumulate
    // agents in the project. Guard the cleanup so a delete failure cannot mask
    // an exception thrown from the main flow above.
    try
    {
        adminClient.DeleteAgent(weatherAgentName);
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine(
            $"Warning: failed to delete Prompt agent '{weatherAgentName}': {ex.Message}");
    }
}

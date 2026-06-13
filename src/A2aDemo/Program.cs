using System.ComponentModel;
using Azure.AI.Projects;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

// =============================================================================
// Microsoft Foundry Agent Service - agent-to-agent (A2A) demo (.NET 10)
//
// This sample makes the A2A public-preview announcement concrete. It composes
// two Foundry "Hosted agents" that use the responses protocol and lets one
// delegate to the other:
//
//   * WeatherAgent  - a specialist agent that answers weather questions. It
//                     owns a local function tool (GetWeather).
//   * CoordinatorAgent - the top-level agent the user talks to. It is given the
//                     specialist as a tool via WeatherAgent.AsAIFunction(), so
//                     it can call the specialist agent and summarize the reply.
//
// When the coordinator calls the specialist, the specialist's answer flows back
// to the coordinator, which keeps control of the conversation. That call-and-
// return behavior is exactly what the A2A tool models in Foundry Agent Service.
// See README.md for the mapping to the announcement and the remote A2A pattern.
// =============================================================================

[Description("Get the current weather for a given location.")]
static string GetWeather([Description("The location to get the weather for.")] string location)
    => $"The weather in {location} is cloudy with a high of 15\u00b0C.";

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

// DefaultAzureCredential uses the identity from 'azd auth login' / 'az login'.
AIProjectClient projectClient = new(new Uri(endpoint), new DefaultAzureCredential());

// Specialist agent: owns the GetWeather function tool.
AITool weatherTool = AIFunctionFactory.Create(GetWeather);
AIAgent weatherAgent = projectClient.AsAIAgent(
    deploymentName,
    instructions: "You are a meteorologist. Answer weather questions using the GetWeather tool.",
    name: "WeatherAgent",
    tools: [weatherTool]);

// Coordinator agent: calls the specialist agent as a tool (agent-to-agent).
AIAgent coordinatorAgent = projectClient.AsAIAgent(
    deploymentName,
    instructions: "You are a helpful coordinator. When asked about the weather, "
        + "call the WeatherAgent tool and summarize its answer for the user.",
    name: "CoordinatorAgent",
    tools: [weatherAgent.AsAIFunction()]);

Console.WriteLine($"Project endpoint : {endpoint}");
Console.WriteLine($"Model deployment : {deploymentName}");
Console.WriteLine($"User question    : {question}");
Console.WriteLine();
Console.WriteLine("CoordinatorAgent is delegating to WeatherAgent via A2A...");
Console.WriteLine();

AgentSession session = await coordinatorAgent.CreateSessionAsync();
AgentResponse response = await coordinatorAgent.RunAsync(question, session);

Console.WriteLine($"CoordinatorAgent: {response}");

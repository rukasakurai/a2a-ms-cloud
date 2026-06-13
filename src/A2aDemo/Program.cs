using Azure.AI.Projects;
using Azure.AI.Projects.Agents;
using Azure.Identity;
using Microsoft.Agents.AI;

// =============================================================================
// Microsoft Foundry Agent Service - agent-to-agent (A2A) demo (.NET 10)
//
// This sample demonstrates Microsoft Agent Framework agent-as-function
// delegation with the same call-and-return semantics as the A2A tool. It pairs a
// server-side Prompt agent (one of the announcement's two agent shapes) with an
// in-process coordinator:
//
//   * WeatherAgent     - a *Prompt agent*: a server-side agent created in the
//                        Foundry project via AgentAdministrationClient. It is
//                        persisted in the project (a name plus versions) and
//                        answers weather questions.
//   * CoordinatorAgent - a *Responses Agent*: composed in-process with the
//                        Microsoft Agent Framework. No server-side resource is
//                        created, so it is not the announcement's server-side
//                        Hosted agent shape. The Prompt agent is attached to it
//                        as a tool via AsAIFunction(), so the coordinator
//                        delegates to it and summarizes the reply.
//
// When the in-process coordinator calls the Prompt specialist, the specialist's
// answer flows back to the coordinator, which keeps control of the conversation.
// This call-and-return behavior mirrors the A2A tool in Foundry Agent Service
// but uses in-process function-tool composition, not protocol-level A2A.
// See README.md for the distinction and the remote A2A path.
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

try
{
    // Bind the server-side Prompt agent as an in-process AIAgent so it can be
    // invoked directly and reused as a tool.
    AIAgent weatherAgent = projectClient.AsAIAgent(weatherVersion);

    // -----------------------------------------------------------------------
    // 2. Responses Agent (in-process): compose the CoordinatorAgent with the
    //    Microsoft Agent Framework. No server-side resource is created; model,
    //    instructions, and tools are provided at runtime. The Prompt agent is
    //    attached as a tool via AsAIFunction(), so the coordinator can call it
    //    (agent-as-function delegation).
    // -----------------------------------------------------------------------
    AIAgent coordinatorAgent = projectClient.AsAIAgent(
        deploymentName,
        instructions: $"You are a helpful coordinator. When asked about the weather, call the {weatherAgentName} tool and summarize its answer for the user.",
        name: "CoordinatorAgent",
        tools: [weatherAgent.AsAIFunction()]);

    Console.WriteLine("CoordinatorAgent (Responses Agent) is delegating to WeatherPromptAgent (Prompt) via agent-as-function composition...");
    Console.WriteLine();

    AgentSession session = await coordinatorAgent.CreateSessionAsync();
    AgentResponse response = await coordinatorAgent.RunAsync(question, session);

    Console.WriteLine($"CoordinatorAgent: {response}");
}
finally
{
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

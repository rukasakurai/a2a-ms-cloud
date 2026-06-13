using A2A;
using Microsoft.Agents.AI;

namespace A2aDemo;

// =============================================================================
// Server side of the A2A protocol.
//
// WeatherAgentHandler implements A2A's IAgentHandler so the Foundry Prompt agent
// can be exposed behind a real A2A endpoint. When an A2A client sends a message
// (over HTTP + JSON-RPC), ExecuteAsync runs the wrapped Foundry agent and writes
// the answer back through the A2A event queue. This is genuine protocol-level
// A2A: the caller and callee communicate over the wire via agent cards and the
// A2A message format, not through in-process function composition.
// =============================================================================
internal sealed class WeatherAgentHandler(AIAgent agent) : IAgentHandler
{
    private readonly AIAgent _agent = agent;

    public async Task ExecuteAsync(
        RequestContext context,
        AgentEventQueue eventQueue,
        CancellationToken cancellationToken)
    {
        var responder = new MessageResponder(eventQueue, context.ContextId);

        AgentSession session = await _agent.CreateSessionAsync();
        AgentResponse response = await _agent.RunAsync(context.UserText ?? string.Empty, session);

        await responder.ReplyAsync(response.Text ?? string.Empty, cancellationToken);
    }

    public Task CancelAsync(
        RequestContext context,
        AgentEventQueue eventQueue,
        CancellationToken cancellationToken) => Task.CompletedTask;

    // -------------------------------------------------------------------------
    // The agent card is what makes this agent discoverable over A2A. An A2A
    // client fetches it from the well-known endpoint to learn the agent's name,
    // skills, supported modes, and the JSON-RPC URL to call.
    // -------------------------------------------------------------------------
    public static AgentCard BuildAgentCard(string jsonRpcUrl) => new()
    {
        Name = "WeatherPromptAgent",
        Description = "Specialist agent that answers weather questions.",
        Version = "1.0.0",
        DefaultInputModes = ["text/plain"],
        DefaultOutputModes = ["text/plain"],
        Capabilities = new AgentCapabilities { Streaming = false },
        SupportedInterfaces =
        [
            new AgentInterface
            {
                Url = jsonRpcUrl,
                ProtocolBinding = A2aProtocol.JsonRpcBinding,
                ProtocolVersion = A2aProtocol.Version,
            },
        ],
        Skills =
        [
            new A2A.AgentSkill
            {
                Id = "weather_report",
                Name = "Weather report",
                Description = "Provides a brief weather report for a requested location.",
                Tags = ["weather", "forecast"],
                Examples =
                [
                    "What is the weather like in Amsterdam?",
                    "Will I need an umbrella in Seattle?",
                ],
            },
        ],
    };
}

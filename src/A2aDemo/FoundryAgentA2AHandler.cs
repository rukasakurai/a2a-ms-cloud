using A2A;
using Microsoft.Agents.AI;

namespace A2aDemo;

// =============================================================================
// Server side of the A2A protocol, shared by every Foundry agent this sample
// publishes behind an A2A endpoint.
//
// The handler wraps an AIAgent and, for each inbound A2A message (over HTTP +
// JSON-RPC), runs that agent and writes the answer back through the A2A event
// queue. The execution is identical regardless of which agent is wrapped, so the
// same handler serves both the declarative **Prompt agent** and the code-built
// **Hosted agent**. The per-agent discovery metadata (the agent card) is
// registered separately for each host (see AgentCards), keeping this handler
// agent-agnostic. This is genuine protocol-level A2A: caller and callee
// communicate over the wire via agent cards and the A2A message format, not
// through in-process function composition.
// =============================================================================
internal sealed class FoundryAgentA2AHandler(AIAgent agent) : IAgentHandler
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
}
